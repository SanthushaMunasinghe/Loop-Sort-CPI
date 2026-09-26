using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using MessagePipe;
using UnityEngine;
using VContainer;

/// <summary>
/// Drives a completed shopping cart out of the level: once its checkmark pops it waits Reverse
/// Delay, backs straight out of its row over Reverse Duration, then turns and rides this side's
/// path to whichever despawn point is closer over Leave Duration — and just parks there, out of
/// view. Nothing is disabled or returned to a pool.
///
/// Hooks CarrierBackClosedMessage, same as EmptyCarrierRowExit. A cart has no lid renderers, so
/// Carrier.ApplyCloseBackMotion returns at once and that message lands the same frame as the
/// checkmark.
///
/// Each side's path is its authored waypoints, in travel order, with the cart's reversed spot put
/// in front and the despawn point on the end, run through a centripetal Catmull-Rom so the cart
/// turns smoothly instead of snapping corner to corner. Only XZ is used — the cart keeps its own
/// height the whole way.
/// </summary>
public sealed class ShoppingCartExit : GameBehaviourBase
{
    private const int SamplesPerSegment = 16;

    [Header("Despawn Points")]
    [SerializeField] private Transform _leftDespawnPoint;
    [SerializeField] private Transform _rightDespawnPoint;

    [Header("Paths")]
    [Tooltip("Waypoints from the aisle behind the carts to Left Despawn Point, in travel order. The " +
             "cart's reversed spot is put in front and Left Despawn Point on the end at run time.")]
    [SerializeField] private List<Transform> _leftPath = new();

    [Tooltip("Waypoints from the aisle behind the carts to Right Despawn Point, in travel order. The " +
             "cart's reversed spot is put in front and Right Despawn Point on the end at run time.")]
    [SerializeField] private List<Transform> _rightPath = new();

    [Header("Motion")]
    [Tooltip("Seconds between the checkmark popping and the cart starting to reverse.")]
    [Min(0f)]
    [SerializeField] private float _reverseDelay = .25f;

    [Tooltip("How far the cart backs out of its row before turning. Needs to be at least the cart's " +
             "own length, or it clips its neighbours when it turns — carts sit ~0.25 apart.")]
    [Min(0f)]
    [SerializeField] private float _reverseDistance = 6.3f;

    [Min(0.01f)]
    [SerializeField] private float _reverseDuration = 1f;

    [Tooltip("Seconds from the end of the reverse to reaching the despawn point.")]
    [Min(0.01f)]
    [SerializeField] private float _leaveDuration = 1f;

    [Tooltip("How quickly the cart's facing catches up to the direction it's heading while leaving, " +
             "per unit travelled rather than per second — at Leave Duration's speeds a time-based " +
             "lag would swing the cart's tail into the shelves on every corner. Higher is snappier.")]
    [Min(0.01f)]
    [SerializeField] private float _turnRate = 2.5f;

    [Inject] private SceneScope _sceneScope;
    [Inject] private ISubscriber<CarrierBackClosedMessage> _carrierBackClosedSub;

    private readonly HashSet<Carrier> _exiting = new();

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        base.BuildMessages(bag);

        _carrierBackClosedSub.Subscribe(OnCarrierBackClosed).AddTo(bag);
    }

    private void OnCarrierBackClosed(CarrierBackClosedMessage m)
    {
        if (!m.Carrier.IsShoppingCart) return;
        if (!_sceneScope.IsRegisteredCarrier(m.Carrier)) return;
        if (!_exiting.Add(m.Carrier)) return;

        RunExit(m.Carrier).Forget();
    }

    private async UniTaskVoid RunExit(Carrier cart)
    {
        var cartT = cart.transform;
        var startPosition = cartT.position;
        var y = startPosition.y;

        if (!TryPickSide(startPosition, out var despawnPoint, out var waypoints))
        {
            Debug.LogWarning($"<b>{nameof(ShoppingCartExit)}</b>: {name} has no despawn point assigned, " +
                             $"so {cart.name} stays where it is.", this);
            return;
        }

        await UniTask.Delay(System.TimeSpan.FromSeconds(_reverseDelay), cancellationToken: ReturnToken);

        // -Z is the cart's basket end (see EmptyCarrierRowExit), so backing out is along +forward.
        var reverseDirection = Flatten(cartT.forward).normalized;
        var reverseEnd = startPosition + reverseDirection * _reverseDistance;

        await LMotion.Create(startPosition, reverseEnd, _reverseDuration)
            .WithEase(Ease.InOutSine)
            .BindToPosition(cartT)
            .AddTo(this)
            .ToUniTask(ReturnToken);

        using var pooled = UnityEngine.Pool.ListPool<Vector3>.Get(out var controlPoints);
        controlPoints.Add(Flatten(reverseEnd));
        foreach (var waypoint in waypoints)
            if (waypoint != null) controlPoints.Add(Flatten(waypoint.position));
        controlPoints.Add(Flatten(despawnPoint.position));

        // Phantom start point toward the row the cart just backed out of, so the curve leaves the
        // reversed spot still easing backward and swings round into the turn — rather than pivoting
        // on the spot toward the first waypoint.
        var firstLeg = (controlPoints[1] - controlPoints[0]).magnitude;
        var phantomStart = controlPoints[0] - reverseDirection * (firstLeg * .5f);

        using var pooledPath = UnityEngine.Pool.ListPool<Vector3>.Get(out var path);
        using var pooledLengths = UnityEngine.Pool.ListPool<float>.Get(out var lengths);
        BuildPath(controlPoints, phantomStart, path, lengths);
        var totalLength = lengths[^1];
        var travelled = 0f;

        await LMotion.Create(0f, 1f, _leaveDuration)
            .WithEase(Ease.InOutSine)
            .Bind(u =>
            {
                var distance = u * totalLength;
                var position = SamplePath(path, lengths, distance, out var direction);
                position.y = y;
                cartT.position = position;

                var step = Mathf.Abs(distance - travelled);
                travelled = distance;
                if (direction.sqrMagnitude < 0.00001f) return;

                // Basket end (-Z) leads, so it's -direction the cart's forward has to line up with.
                var targetRotation = Quaternion.LookRotation(-direction.normalized);
                var t = 1f - Mathf.Exp(-_turnRate * step);
                cartT.rotation = Quaternion.Slerp(cartT.rotation, targetRotation, t);
            })
            .AddTo(this)
            .ToUniTask(ReturnToken);
    }

    private bool TryPickSide(Vector3 from, out Transform despawnPoint, out List<Transform> waypoints)
    {
        var left = _leftDespawnPoint;
        var right = _rightDespawnPoint;

        var useLeft = left != null && (right == null ||
                                       (left.position - from).sqrMagnitude <= (right.position - from).sqrMagnitude);

        despawnPoint = useLeft ? left : right;
        waypoints = useLeft ? _leftPath : _rightPath;
        return despawnPoint != null;
    }

    /// <summary>
    /// Densely samples a centripetal Catmull-Rom through controlPoints into path, with lengths holding
    /// each sample's running arc length, so the cart can be moved at an even pace along it.
    /// </summary>
    private static void BuildPath(List<Vector3> controlPoints, Vector3 phantomStart, List<Vector3> path, List<float> lengths)
    {
        var count = controlPoints.Count;
        var phantomEnd = controlPoints[count - 1] + (controlPoints[count - 1] - controlPoints[count - 2]);

        path.Add(controlPoints[0]);
        lengths.Add(0f);

        for (var i = 0; i < count - 1; i++)
        {
            var p0 = i == 0 ? phantomStart : controlPoints[i - 1];
            var p1 = controlPoints[i];
            var p2 = controlPoints[i + 1];
            var p3 = i + 2 < count ? controlPoints[i + 2] : phantomEnd;

            for (var s = 1; s <= SamplesPerSegment; s++)
            {
                var point = CentripetalCatmullRom(p0, p1, p2, p3, s / (float)SamplesPerSegment);
                lengths.Add(lengths[^1] + (point - path[^1]).magnitude);
                path.Add(point);
            }
        }
    }

    private static Vector3 SamplePath(List<Vector3> path, List<float> lengths, float distance, out Vector3 direction)
    {
        var last = path.Count - 1;
        if (distance >= lengths[last])
        {
            direction = path[last] - path[last - 1];
            return path[last];
        }

        var i = 1;
        while (i < last && lengths[i] < distance) i++;

        var segmentLength = lengths[i] - lengths[i - 1];
        var t = segmentLength > 0f ? (distance - lengths[i - 1]) / segmentLength : 0f;
        direction = path[i] - path[i - 1];
        return Vector3.Lerp(path[i - 1], path[i], t);
    }

    /// <summary>Barry-Goldman evaluation with alpha 0.5 — no cusps or overshoot on unevenly spaced
    /// points, which matters here since the reversed spot can sit right next to the first waypoint.</summary>
    private static Vector3 CentripetalCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
    {
        var t0 = 0f;
        var t1 = t0 + Knot(p0, p1);
        var t2 = t1 + Knot(p1, p2);
        var t3 = t2 + Knot(p2, p3);
        var t = Mathf.Lerp(t1, t2, u);

        var a1 = Blend(p0, p1, t0, t1, t);
        var a2 = Blend(p1, p2, t1, t2, t);
        var a3 = Blend(p2, p3, t2, t3, t);
        var b1 = Blend(a1, a2, t0, t2, t);
        var b2 = Blend(a2, a3, t1, t3, t);
        return Blend(b1, b2, t1, t2, t);

        static float Knot(Vector3 a, Vector3 b) => Mathf.Max(Mathf.Sqrt((b - a).magnitude), 0.0001f);

        static Vector3 Blend(Vector3 a, Vector3 b, float ta, float tb, float t) =>
            (tb - t) / (tb - ta) * a + (t - ta) / (tb - ta) * b;
    }

    private static Vector3 Flatten(Vector3 v) => new(v.x, 0f, v.z);

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        DrawSide(_leftPath, _leftDespawnPoint, new Color(1f, .6f, .1f));
        DrawSide(_rightPath, _rightDespawnPoint, new Color(.2f, .8f, 1f));
    }

    private static void DrawSide(List<Transform> waypoints, Transform despawnPoint, Color color)
    {
        Gizmos.color = color;
        Vector3? previous = null;
        foreach (var waypoint in waypoints)
        {
            if (waypoint == null) continue;
            Gizmos.DrawWireSphere(waypoint.position, .4f);
            if (previous.HasValue) Gizmos.DrawLine(previous.Value, waypoint.position);
            previous = waypoint.position;
        }

        if (despawnPoint == null) return;
        Gizmos.DrawWireCube(despawnPoint.position, Vector3.one);
        if (previous.HasValue) Gizmos.DrawLine(previous.Value, despawnPoint.position);
    }
#endif
}

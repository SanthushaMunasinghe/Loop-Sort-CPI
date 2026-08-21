using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Dreamteck;
using Dreamteck.Splines;
using LitMotion;
using LitMotion.Extensions;
using MessagePipe;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

/// <summary>
/// Makes an Empty Carrier Row's front-to-back hand-off physical.
///
/// SceneScope.FindCompatibleEmptyCarrier/GetActiveRowCarrier already hand a row from one carrier to
/// the next the moment the front one fills up, purely by skipping full carriers on every lookup — by
/// design, nothing is disabled, moved or removed for that to happen. This component adds the visible
/// side of the same event: once the row's current front carrier's lid finishes closing, it drives
/// away along this row's own spline while the carriers behind it slide forward to close the gap, and
/// the new front carrier is only unlocked for transfer once that settles — so nothing routes into a
/// carrier that is still mid-slide.
///
/// Attach to an Empty Carrier Row's parent transform (the same object the row's carriers are already
/// parented under, see LevelSandboxEditor's row generator) alongside a SplineComputer describing this
/// row's exit path. Row order is read from this transform's children directly, in hierarchy order —
/// the same order the generator created them in — so this needs nothing from SceneScope's own
/// EmptyCarrierRows list beyond the shared duration fields.
/// </summary>
public sealed class EmptyCarrierRowExit : GameBehaviourBase
{
    [Tooltip("This row's exit path. Auto-filled from this GameObject.")]
    [SerializeField] private SplineComputer _splineComputer;

    [Tooltip("Spline point index the initial lerp-in ends at — where the exiting carrier joins the " +
             "spline before riding it out to the last point. We don't always want to join at the " +
             "very first point, so this is set manually per row. Defaults to the 2nd point (index 1).")]
    [Min(0)]
    [SerializeField] private int _initialPointIndex = 1;

    [Tooltip("Spline point index that, once the exiting carrier reaches it, starts the remaining " +
             "carriers sliding forward — while the exiting carrier keeps travelling on to the last " +
             "point. Initial Point Index (not this) is always where the initial lerp-in ends.")]
    [Min(1)]
    [SerializeField] private int _shiftTriggerPointIndex = 4;

    [Inject] private SceneScope _sceneScope;

    [Inject] private ISubscriber<CarrierCompleteMessage> _carrierCompleteSub;
    [Inject] private ISubscriber<CarrierBackClosedMessage> _carrierBackClosedSub;

    private readonly CompositeMotionHandle _exitMotions = new();
    private readonly CompositeMotionHandle _shiftMotions = new();

    private readonly List<Carrier> _carriers = new();
    private readonly List<Vector3> _rowSeatPositions = new();
    private int _exitedCount;

    private void Reset()
    {
        _splineComputer = GetComponent<SplineComputer>();
    }

    protected override void Awake()
    {
        base.Awake();

        if (_splineComputer == null) _splineComputer = GetComponent<SplineComputer>();

        CaptureRow();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        base.BuildMessages(bag);

        _carrierCompleteSub.Subscribe(OnCarrierComplete).AddTo(bag);
        _carrierBackClosedSub.Subscribe(OnCarrierBackClosed).AddTo(bag);
    }

    // Row order is this transform's child order, matching how the row was generated. Seat positions
    // are captured once here rather than read live later, so a second completion shifts relative to
    // the ORIGINAL row layout rather than compounding off wherever a carrier currently sits — same
    // reasoning as Carrier.Motion.cs's CaptureGroupSlideOriginals.
    private void CaptureRow()
    {
        _carriers.Clear();
        _rowSeatPositions.Clear();
        _exitedCount = 0;

        for (var i = 0; i < transform.childCount; i++)
        {
            if (!transform.GetChild(i).TryGetComponent<Carrier>(out var carrier)) continue;
            _carriers.Add(carrier);
            _rowSeatPositions.Add(carrier.transform.position);
        }
    }

    // CarrierCompleteMessage fires the instant a carrier fills — synchronously, well before its lid
    // starts closing — and SceneScope.GetActiveRowCarrier only ever checks IsFull()/IsSink(), not
    // whether anything is mid-animation. So the next carrier has to be locked right here, not when
    // RunExit's own animation starts: otherwise there's a real window, from this message to
    // CarrierBackClosedMessage arriving, where a block could already route into the next carrier
    // before its lid has even closed, let alone before it has shifted into the front seat.
    private void OnCarrierComplete(CarrierCompleteMessage m)
    {
        if (_exitedCount >= _carriers.Count) return;
        if (m.Carrier != _carriers[_exitedCount]) return;

        var nextIndex = _exitedCount + 1;
        if (nextIndex < _carriers.Count) _carriers[nextIndex].DisableTransfer(gameObject);
    }

    private void OnCarrierBackClosed(CarrierBackClosedMessage m)
    {
        if (_splineComputer == null) return;
        if (_exitedCount >= _carriers.Count) return;
        if (m.Carrier != _carriers[_exitedCount]) return;

        RunExit(m.Carrier).Forget();
    }

    // The next carrier is already locked by OnCarrierComplete by the time this runs — this only
    // lerps the completed carrier onto the spline at the initial point, then rides the curve to the last
    // point, firing the shift the moment it passes _shiftTriggerPointIndex rather than waiting for
    // the full journey to finish.
    private async UniTaskVoid RunExit(Carrier exiting)
    {
        if (_splineComputer.pointCount < 2)
        {
            Debug.LogWarning($"<b>{nameof(EmptyCarrierRowExit)}</b>: {name}'s spline needs at least " +
                             "2 points to exit onto.", this);
            return;
        }

        _exitMotions.Cancel();

        var exitingT = exiting.transform;
        var startPosition = exitingT.position;
        var startY = startPosition.y;
        var lastPointIndex = _splineComputer.pointCount - 1;

        // Clamped short of the last point, never onto or past it: an initial index sitting on the
        // last point would leave nothing for the spline-follow phase to ride.
        var initialPointIndex = Mathf.Clamp(_initialPointIndex, 0, Mathf.Max(0, lastPointIndex - 1));
        if (initialPointIndex != _initialPointIndex)
        {
            Debug.LogWarning($"<b>{nameof(EmptyCarrierRowExit)}</b>: {name}'s spline only has " +
                             $"{_splineComputer.pointCount} points, so Initial Point Index " +
                             $"{_initialPointIndex} was clamped to {initialPointIndex}.", this);
        }

        // Clamped a point short of the last one, never onto it: a trigger index sitting exactly on
        // the last point degenerates to "reached only once the exit is basically already over,"
        // which visually reads as the shift never happening at all, on any spline short enough for
        // the authored index to need clamping in the first place. Also kept after the initial point,
        // since a trigger at or before it would fire before the spline-follow phase even starts.
        var triggerPointIndex = Mathf.Clamp(_shiftTriggerPointIndex, initialPointIndex + 1,
            Mathf.Max(initialPointIndex + 1, lastPointIndex - 1));
        if (triggerPointIndex != _shiftTriggerPointIndex)
        {
            Debug.LogWarning($"<b>{nameof(EmptyCarrierRowExit)}</b>: {name}'s spline only has " +
                             $"{_splineComputer.pointCount} points, so Shift Trigger Point Index " +
                             $"{_shiftTriggerPointIndex} was clamped to {triggerPointIndex}. Add more " +
                             "points to the row's spline if you want the shift to start later.", this);
        }

        var initialPointPosition = _splineComputer.EvaluatePosition(initialPointIndex);
        var initialPointPercent = _splineComputer.GetPointPercent(initialPointIndex);
        var triggerPercent = _splineComputer.GetPointPercent(triggerPointIndex);

        // First portion of the tween is a straight lerp onto the spline at the initial point; the rest rides
        // the curve itself from there to the last point. Both phases share one easing curve over one
        // duration so the hand-off from "lerp" to "follow" has no visible seam.
        const float lerpInPortion = .15f;
        var shiftStarted = false;
        var previousPosition = startPosition;

        await LMotion.Create(0f, 1f, _sceneScope.EmptyCarrierExitDuration)
            .WithEase(Ease.InOutSine)
            .Bind(u =>
            {
                Vector3 position;
                if (u < lerpInPortion)
                {
                    position = Vector3.Lerp(startPosition, initialPointPosition, u / lerpInPortion);
                }
                else
                {
                    var splinePercent = DMath.Lerp(initialPointPercent, 1.0,
                        (u - lerpInPortion) / (1f - lerpInPortion));
                    position = _splineComputer.EvaluatePosition(splinePercent);

                    if (!shiftStarted && splinePercent >= triggerPercent)
                    {
                        shiftStarted = true;
                        RunShift().Forget();
                    }
                }

                position.y = startY;
                exitingT.position = position;

                // -Z is this carrier's modelled front, so it's -direction that needs to line up with
                // where it's actually headed. Direction is taken from the Y-locked positions
                // themselves, so this only ever yaws — no pitch from the spline's own vertical shape.
                var direction = position - previousPosition;
                if (direction.sqrMagnitude > 0.00001f)
                    exitingT.rotation = Quaternion.LookRotation(-direction.normalized);
                previousPosition = position;
            })
            .AddTo(this)
            .AddTo(_exitMotions)
            .ToUniTask(ReturnToken);
    }

    // Slides every remaining carrier one seat closer to the front, then unlocks the new front
    // carrier for transfer — the only thing that ungates it, since it was locked the moment the
    // previous carrier completed.
    private async UniTaskVoid RunShift()
    {
        if (_sceneScope == null)
        {
            Debug.LogError($"<b>{nameof(EmptyCarrierRowExit)}</b>: {name} has no injected SceneScope, " +
                           "cannot shift.", this);
            return;
        }

        _shiftMotions.Cancel();

        var duration = _sceneScope.GroupSlideDuration;
        using var pooled = ListPool<UniTask>.Get(out var tasks);

        for (var seat = 0; _exitedCount + 1 + seat < _carriers.Count; seat++)
        {
            var carrierT = _carriers[_exitedCount + 1 + seat].transform;
            var target = _rowSeatPositions[seat];
            target.y = carrierT.position.y;

            tasks.Add(LMotion.Create(carrierT.position, target, duration)
                .BindToPosition(carrierT)
                .AddTo(this)
                .AddTo(_shiftMotions)
                .ToUniTask(ReturnToken));
        }

        Debug.Log($"<b>{nameof(EmptyCarrierRowExit)}</b>: {name} shifting {tasks.Count} carrier(s) " +
                 $"over {duration:F2}s.", this);

        await UniTask.WhenAll(tasks);

        _exitedCount++;
        if (_exitedCount < _carriers.Count) _carriers[_exitedCount].EnableTransfer(gameObject);
    }
}

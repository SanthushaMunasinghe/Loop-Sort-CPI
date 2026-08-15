using Dreamteck.Splines;
using LitMotion;
using LitMotion.Extensions;
using MessagePipe;
using Scellecs.Morpeh;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public sealed class CameraBoundsSystem : SystemBase
{
    [Inject] private CameraConfig _config;
    [Inject] private CinemachineTargetGroup _targetGroup;
    [Inject] private CinemachineGroupFraming _groupFraming;
    [Inject] private SplineComputer _splineComputer;
    [Inject] private LevelSheet.Level _level;

    [Inject] private ISubscriber<CameraUpdateBoundsMessage> _cameraUpdateBoundsSub;

    [Inject] private IPublisher<CameraUpdateCompleteMessage> _cameraUpdateCompletePub;

    private Transform _targetGroupParent;
    private Transform _top;
    private Transform _bottom;
    private Transform _left;
    private Transform _right;
    private bool _isBoundsInitialized;

    private Filter _filter;
    private Stash<BehaviourView<Carrier>> _carriers;

    public override void OnAwake()
    {
        base.OnAwake();

        _filter = World.Filter.With<BehaviourView<Carrier>>().With<Enabled>().Build();
        _carriers = World.GetStash<BehaviourView<Carrier>>();

        CreateTargetGroup();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _cameraUpdateBoundsSub.Subscribe(OnCameraUpdateBounds).AddTo(bag);
    }

    private void OnCameraUpdateBounds(CameraUpdateBoundsMessage m)
    {
        CalculateCameraBounds();
    }

    private void CalculateCameraBounds()
    {
        SetGroupFramingSettings();

        using var p = ListPool<Vector3>.Get(out var points);

        for (var i = 0; i < _splineComputer.pointCount; i++)
        {
            var splinePoint = _splineComputer.GetPoint(i);
            var project = _splineComputer.Project(splinePoint.position);
            points.Add(project.position);
        }

        foreach (var entity in _filter)
        {
            ref var view = ref _carriers.Get(entity);
            var carrier = view.Behaviour;
            var carrierT = carrier.transform;
            points.Add(carrierT.position);
            points.Add(carrier.FrontPoint.position);
        }

        var (left, right, top, bottom) = (100f, -100f, -100f, 100f);
        foreach (var point in points)
        {
            if (left > point.x) left = point.x;
            if (right < point.x) right = point.x;
            if (top < point.z) top = point.z;
            if (bottom > point.z) bottom = point.z;
        }

        var topPosition = Vector3.forward * top + Vector3.right * (left + right) / 2f;
        topPosition += Vector3.forward * _config.TopOffset;
        var bottomPosition = Vector3.forward * bottom + Vector3.right * (left + right) / 2f;
        bottomPosition += Vector3.back * _config.BottomOffset;
        var leftPosition = Vector3.right * left + Vector3.forward * (top + bottom) / 2f;
        leftPosition += Vector3.left * _config.LeftOffset;
        var rightPosition = Vector3.right * right + Vector3.forward * (top + bottom) / 2f;
        rightPosition += Vector3.right * _config.RightOffset;

        if (!_isBoundsInitialized)
        {
            _isBoundsInitialized = true;
            _top.position = topPosition;
            _bottom.position = bottomPosition;
            _left.position = leftPosition;
            _right.position = rightPosition;
        }
        else
        {
            const float duration = .4f;
            LMotion.Create(_top.position, topPosition, duration)
                .BindToPosition(_top)
                .ToUniTask(SceneLoadToken);
            LMotion.Create(_bottom.position, bottomPosition, duration)
                .BindToPosition(_bottom)
                .ToUniTask(SceneLoadToken);
            LMotion.Create(_left.position, leftPosition, duration)
                .BindToPosition(_left)
                .ToUniTask(SceneLoadToken);
            LMotion.Create(_right.position, rightPosition, duration)
                .BindToPosition(_right)
                .ToUniTask(SceneLoadToken);
        }

        _targetGroup.DoUpdate();

        _cameraUpdateCompletePub.Publish(new CameraUpdateCompleteMessage
        {
            Center = (_top.position + _bottom.position + _left.position + _right.position) / 4f,
            Top = topPosition,
            Bottom = bottomPosition,
            Left = leftPosition,
            Right = rightPosition
        });
    }

    private void SetGroupFramingSettings()
    {
        var splineRef = _level.Spline.Ref;
        _groupFraming.CenterOffset = splineRef.CameraOffset != Vector2.zero ? splineRef.CameraOffset : _config.CenterOffset;
        _groupFraming.FramingSize = _config.FramingSize;
    }

    private void CreateTargetGroup()
    {
        _targetGroupParent = new GameObject("Cinemachine Target Group Members").transform;
        _top = CreateTarget("Top");
        _bottom = CreateTarget("Bottom");
        _left = CreateTarget("Left");
        _right = CreateTarget("Right");
    }

    private Transform CreateTarget(string targetName)
    {
        var t = new GameObject(targetName).transform;
        t.parent = _targetGroupParent;
        _targetGroup.AddMember(t, 1f, 1f);
        return t;
    }
}

public struct CameraUpdateBoundsMessage
{
}

public struct CameraUpdateCompleteMessage
{
    public Vector3 Center;
    public Vector3 Top;
    public Vector3 Bottom;
    public Vector3 Left;
    public Vector3 Right;
}
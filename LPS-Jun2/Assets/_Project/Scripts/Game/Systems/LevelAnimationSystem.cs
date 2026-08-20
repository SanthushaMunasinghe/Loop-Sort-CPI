using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Dreamteck.Splines;
using LitMotion;
using LitMotion.Extensions;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine;
using VContainer;

public sealed class LevelAnimationSystem : SystemBase
{
    [Inject] private ISubscriber<CameraUpdateCompleteMessage> _cameraUpdateCompleteSub;
    [Inject] private ISubscriber<LevelWinMessage> _levelWinSub;

    [Inject] private IPublisher<LevelAnimationEnterStartMessage> _levelAnimationEnterStartPub;
    [Inject] private IPublisher<LevelAnimationEnterCompleteMessage> _levelAnimationEnterCompletePub;
    [Inject] private IPublisher<LevelAnimationCarrierCompleteMessage> _levelAnimationCarrierCompletePub;

    [Inject] private SplineMesh _splineMesh;
    [Inject] private ConveyorArrowSystem _conveyorArrowSystem;
    [Inject] private ConveyorEndMeshSystem _conveyorEndMeshSystem;
    [Inject] private PrefabModule _prefabModule;
    [Inject] private Particles _particles;
    [Inject] private InteractionModule _interactionModule;
    [Inject] private Monitors _monitors;
    [Inject] private CameraConfig _cameraConfig;
    [Inject] private RemoteConfigModule _remoteConfigModule;

    private Filter _carrierFilter;
    private Stash<BehaviourView<Carrier>> _carriers;
    private Stash<Enabled> _enabledEntities;

    private ParticleSystem _conveyorTrail;
    private bool _isLevelEntered;
    private LevelAnimationConfig _levelAnimationConfig;
    private CameraUpdateCompleteMessage _cameraMessage;

    private readonly List<UniTask> _tasks = new();

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        base.BuildMessages(bag);

        _cameraUpdateCompleteSub.Subscribe(OnCameraUpdateComplete).AddTo(bag);
        _levelWinSub.Subscribe(OnLevelWin).AddTo(bag);
    }

    public override void OnAwake()
    {
        base.OnAwake();

        _carrierFilter = World.Filter.With<BehaviourView<Carrier>>().Without<BehaviourView<TunnelCarrier>>().Build();
        _carriers = World.GetStash<BehaviourView<Carrier>>();
        _enabledEntities = World.GetStash<Enabled>();

        _levelAnimationConfig = _remoteConfigModule.GetDataClassNew<LevelAnimationConfig>();
    }

    private void OnCameraUpdateComplete(CameraUpdateCompleteMessage m)
    {
        if (_isLevelEntered) return;
        _isLevelEntered = true;

        _cameraMessage = m;

        if (_levelAnimationConfig.Enter)
            HandleLevelEnterAnimation();
        else
            _levelAnimationEnterCompletePub.Publish(new LevelAnimationEnterCompleteMessage());
    }

    private async UniTaskVoid HandleLevelEnterAnimation()
    {
        _interactionModule.EnableRestriction(this);
        _monitors.DisableRaycaster(this);
        _levelAnimationEnterStartPub.Publish(new LevelAnimationEnterStartMessage());

        try
        {
            await ApplyLevelEnter();
        }
        catch
        {
        }

        _levelAnimationEnterCompletePub.Publish(new LevelAnimationEnterCompleteMessage());
        _interactionModule.DisableRestriction(this);
        _monitors.RestoreRaycaster(this);
    }

    private void OnLevelWin(LevelWinMessage m)
    {
        if (_levelAnimationConfig.Exit)
            ApplyLevelExit();
    }

    private void ApplyMoveMotion(Carrier carrier, Vector3 from, Vector3 to)
    {
        carrier.TryGetComponent<ILevelAnimationListener>(out var listener);
        listener?.OnLevelAnimationStart();

        var t = carrier.transform;
        var right = t.right;

        var toDirection = (to - from).normalized;
        var rightDot = Vector3.Dot(t.right, toDirection);
        var forwardDot = Vector3.Dot(t.forward, toDirection);
        var originalDirection = t.forward;
        var forwardMove = forwardDot > 0;
        var carrierDirection = forwardMove ? originalDirection : -originalDirection;

        var distance = Vector3.Distance(from, to);
        var duration = distance * .04f;
        var motion = LMotion.Create(0f, 1f, duration)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithOnComplete(() =>
            {
                listener?.OnLevelAnimationComplete();
                t.position = to;
                carrier.ApplyOpenBackMotion();
                ApplyPostMoveMotion(carrier, originalDirection, carrierDirection);
            })
            .Bind(x =>
            {
                var lerpPosition = Vector3.Lerp(from, to, x);
                lerpPosition += 1.5f * right * rightDot * Mathf.PI * x * Mathf.Sin(x * Mathf.PI);

                var moveDirection = (lerpPosition - t.position).normalized;
                var signedAngle = Vector3.SignedAngle(carrierDirection, moveDirection, Vector3.up);
                var lookAt = Quaternion.AngleAxis(signedAngle, Vector3.up) * originalDirection;

                var currentCarrierDir = forwardMove ? t.forward : -t.forward;
                var moveAngle = Vector3.SignedAngle(moveDirection, currentCarrierDir, Vector3.up) * 40f;
                moveAngle *= forwardMove ? -1f : 1f;
                moveAngle = Mathf.Clamp(moveAngle, -40f, 40f);
                carrier.LeftTire.localRotation = Quaternion.AngleAxis(moveAngle, Vector3.up);
                carrier.RightTire.localRotation = Quaternion.AngleAxis(moveAngle + 180f, Vector3.up);

                t.forward = lookAt;
                t.position = lerpPosition;
            })
            .AddTo(carrier);

        var task = motion.ToUniTask();
        _tasks.Add(task);
    }

    private void ApplyPostMoveMotion(Carrier carrier, Vector3 originalDirection, Vector3 carrierDirection)
    {
        var t = carrier.transform;
        carrier.LeftTire.localRotation = Quaternion.AngleAxis(0, Vector3.up);
        carrier.RightTire.localRotation = Quaternion.AngleAxis(180, Vector3.up);

        {
            var originalRotation = Quaternion.LookRotation(originalDirection, Vector3.up);
            var cross = Vector3.Cross(t.up, carrierDirection);

            var to = Quaternion.AngleAxis(2, cross) * originalRotation;
            LMotion.Create(t.rotation, to, .2f)
                .BindToRotation(t)
                .AddTo(carrier);
            var to2 = Quaternion.AngleAxis(-2, cross) * originalRotation;
            LMotion.Create(to, to2, .2f)
                .WithDelay(.2f)
                .BindToRotation(t)
                .AddTo(carrier);
            LMotion.Create(to2, originalRotation, .2f)
                .WithDelay(.4f)
                .BindToRotation(t)
                .AddTo(carrier);
        }
        {
            var from = t.position;
            var to = from + carrierDirection * .3f;
            LMotion.Create(from, to, .2f)
                .WithEase(Ease.OutQuad)
                .BindToPosition(t)
                .AddTo(carrier);
            LMotion.Create(to, from, .2f)
                .WithEase(Ease.OutQuad)
                .WithDelay(.2f)
                .BindToPosition(t)
                .AddTo(carrier);
        }
    }

    private Vector3 GetPositionOutOfCameraBounds(Transform t)
    {
        var newPosition = t.position;
        var currentPosition = t.position;
        var forward = t.forward;
        var right = t.right;

        var verticalDistance = Vector3.Distance(_cameraMessage.Top, _cameraMessage.Bottom) / 2f;
        verticalDistance += _cameraConfig.TopOffset + _cameraConfig.BottomOffset + 1.5f;
        var horizontalDistance = Vector3.Distance(_cameraMessage.Left, _cameraMessage.Right) / 2f;
        horizontalDistance += _cameraConfig.LeftOffset + _cameraConfig.RightOffset + 5f;

        var zAlignment = Vector3.Dot(forward, Vector3.forward);
        var xAlignment = Vector3.Dot(forward, Vector3.right);

        var absZ = Mathf.Abs(zAlignment);
        var absX = Mathf.Abs(xAlignment);
        var distance = absZ >= absX ? verticalDistance : horizontalDistance;

        var toCenter = _cameraMessage.Center - currentPosition;
        var sign = Vector3.Dot(toCenter, forward) < 0f ? 1f : -1f;

        newPosition += forward * (distance * sign);

        var circleDot = Vector3.Dot(toCenter, right);
        var circleOffset = circleDot * absZ >= absX ? 3f : 1f;
        newPosition -= right * (circleDot * circleOffset);

        return newPosition;
    }

    private async UniTask ApplyLevelEnter()
    {
        var splineLength = _splineMesh.CalculateLength();
        _splineMesh.clipTo = 0f;

        var arrows = _conveyorArrowSystem.Arrows;
        foreach (var arrow in arrows)
        {
            var t = arrow.transform;
            t.localScale = Vector3.zero;
        }

        var endMeshes = _conveyorEndMeshSystem.EndMeshes;
        foreach (var endMesh in endMeshes)
        {
            var t = endMesh.transform;
            t.localScale = Vector3H.AlmostZero;
        }

        _tasks.Clear();
        foreach (var entity in _carrierFilter)
        {
            Carrier carrier = _carriers.Get(entity);

            var t = carrier.transform;
            var to = t.position;
            t.position = GetPositionOutOfCameraBounds(t);
            var from = t.position;

            if (_enabledEntities.Has(entity))
            {
                carrier.ApplyCloseBackMotion(immediate: true).Forget();
                ApplyMoveMotion(carrier, from, to);
            }
            else
            {
                carrier.transform.position = to;
            }
        }

        await UniTask.WhenAll(_tasks).AttachExternalCancellation(SceneLoadToken);

        _levelAnimationCarrierCompletePub.Publish(new LevelAnimationCarrierCompleteMessage());

        _conveyorTrail = _prefabModule.Rent(_particles.ConveyorTrail);

        var duration = splineLength * .0275f;
        await LMotion.Create(0f, 1f, duration)
            .Bind(x =>
            {
                _splineMesh.clipTo = x;
                var position = _splineMesh.EvaluatePosition(.925f);
                position.y -= .5f;
                _conveyorTrail.transform.position = position;
            })
            .ToUniTask(SceneLoadToken);

        foreach (var arrow in arrows)
        {
            var t = arrow.transform;
            LMotion.Create(Vector3H.AlmostZero, Vector3.one, .5f)
                .BindToLocalScale(t)
                .ToUniTask(SceneLoadToken);
        }

        foreach (var endMesh in endMeshes)
        {
            var t = endMesh.transform;
            LMotion.Create(Vector3H.AlmostZero, Vector3.one, .5f)
                .WithEase(Ease.InOutBack)
                .BindToLocalScaleNonNegative(t)
                .ToUniTask(SceneLoadToken);
            var to = t.position;
            var from = t.position - t.forward;
            LMotion.Create(from, to, .2f)
                .WithEase(Ease.OutBack)
                .WithDelay(.4f, skipValuesDuringDelay: false)
                .BindToPosition(t)
                .ToUniTask(SceneLoadToken);
        }
    }

    private async UniTaskVoid ApplyLevelExit()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(1f));

        var arrows = _conveyorArrowSystem.Arrows;
        foreach (var arrow in arrows)
        {
            var t = arrow.transform;
            LMotion.Create(Vector3.one, Vector3H.AlmostZero, .5f)
                .BindToLocalScale(t)
                .ToUniTask(SceneLoadToken);
        }

        var endMeshes = _conveyorEndMeshSystem.EndMeshes;
        foreach (var endMesh in endMeshes)
        {
            var t = endMesh.transform;
            var from = t.position;
            var to = t.position - t.forward;
            LMotion.Create(from, to, .2f)
                .WithEase(Ease.OutBack)
                .BindToPosition(t)
                .ToUniTask(SceneLoadToken);
            LMotion.Create(Vector3.one, Vector3H.AlmostZero, .5f)
                .WithEase(Ease.InOutBack)
                .WithDelay(.1f, skipValuesDuringDelay: false)
                .BindToLocalScaleNonNegative(t)
                .ToUniTask(SceneLoadToken);
        }

        _conveyorTrail ??= _prefabModule.Rent(_particles.ConveyorTrail);

        var splineLength = _splineMesh.CalculateLength();
        var duration = splineLength * .035f;
        await LMotion.Create(1f, 0f, duration)
            .Bind(x =>
            {
                _splineMesh.clipTo = x;
                var position = _splineMesh.EvaluatePosition(.925f);
                position.y -= .5f;
                _conveyorTrail.transform.position = position;
            })
            .ToUniTask(SceneLoadToken);

        foreach (var entity in _carrierFilter)
        {
            if (!_enabledEntities.Has(entity)) continue;
            Carrier carrier = _carriers.Get(entity);
            var t = carrier.transform;
            var from = t.position;
            var to = GetPositionOutOfCameraBounds(t);
            ApplyMoveMotion(carrier, from, to);
        }
    }
}

public struct LevelAnimationEnterStartMessage
{
}

public struct LevelAnimationEnterCompleteMessage
{
}

public struct LevelAnimationCarrierCompleteMessage
{
}

public interface ILevelAnimationListener
{
    public void OnLevelAnimationStart();
    public void OnLevelAnimationComplete();
}
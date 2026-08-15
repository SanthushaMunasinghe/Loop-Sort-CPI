using System;
using System.Threading;
using Coffee.UIEffects;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using MessagePipe;
using UnityEngine;
using VContainer;

public sealed class TransitionMonitor : MonitorBase
{
    [SerializeField] private float FadeDuration;
    [SerializeField] private float EffectDuration;
    [SerializeField] private float Delay;

    [Inject] private RaycastBlockerMonitor _raycastBlockerMonitor;
    [Inject] private InteractionModule _interactionModule;
    [Inject] private SceneModule _sceneModule;

    [Inject] private IAsyncSubscriber<ScenePreLoadMessage> _scenePreLoad;
    [Inject] private IAsyncSubscriber<ScenePostLoadMessage> _scenePostLoad;

    private UIEffect _effect;
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _effect = GetComponentInChildren<UIEffect>();
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    public override void OnActivated()
    {
        base.OnActivated();

        _raycastBlockerMonitor.Block(this);
        _interactionModule.EnableRestriction(this);
    }

    public override void OnDeactivated()
    {
        base.OnDeactivated();

        _raycastBlockerMonitor.Unblock(this);
        _interactionModule.DisableRestriction(this);
    }

    public override void Setup()
    {
        base.Setup();

        _scenePreLoad.Subscribe(OnScenePreLoad);
        _scenePostLoad.Subscribe(OnScenePostLoad);
    }

    public async UniTask Open(CancellationToken token)
    {
        await UniTask.NextFrame(cancellationToken: token);
        await UniTask.Delay(TimeSpan.FromSeconds(Delay), cancellationToken: token);
        await LMotion.Create(0f, 1f, EffectDuration)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToTransitionRate(_effect)
            .ToUniTask(token);
        await LMotion.Create(1f, 0f, EffectDuration)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAlpha(_canvasGroup)
            .ToUniTask(token);

        gameObject.SetActive(false);
    }

    public async UniTask Close(CancellationToken token)
    {
        gameObject.SetActive(true);

        await UniTask.NextFrame(cancellationToken: token);
        await LMotion.Create(0f, 1f, FadeDuration)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAlpha(_canvasGroup)
            .ToUniTask(token);
        await LMotion.Create(1f, 0f, EffectDuration)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToTransitionRate(_effect)
            .ToUniTask(token);
    }

    private UniTask OnScenePreLoad(ScenePreLoadMessage m, CancellationToken token)
    {
        if (_sceneModule.Container != null)
        {
            var levelTransition = _sceneModule.Container.Resolve<LevelTransitionData>();
            if (levelTransition.Exit) return default;
        }

        return Close(token);
    }

    private UniTask OnScenePostLoad(ScenePostLoadMessage m, CancellationToken token)
    {
        if (_sceneModule.Container != null)
        {
            var levelTransition = _sceneModule.Container.Resolve<LevelTransitionData>();
            if (levelTransition.Enter)
            {
                gameObject.SetActive(false);
                return default;
            }
        }

        return Open(token);
    }
}
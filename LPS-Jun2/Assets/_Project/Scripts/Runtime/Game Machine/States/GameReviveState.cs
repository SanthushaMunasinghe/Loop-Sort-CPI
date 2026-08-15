using System;
using Cysharp.Threading.Tasks;
using VContainer;

public sealed class GameReviveState : GameStateBase
{
    [Inject] private Monitors _monitors;
    [Inject] private SceneModule _sceneModule;
    [Inject] private HapticModule _hapticModule;
    [Inject] private AudioModule _audioModule;
    [Inject] private InteractionModule _interactionModule;
    [Inject] private TimeModule _timeModule;
    [Inject] private RemoteConfigModule _remoteConfigModule;

    private GiveUpConfig _giveUpConfig;
    private PaceConfig _paceConfig;

    public override void Init()
    {
        base.Init();

        _giveUpConfig = _remoteConfigModule.GetDataClassNew<GiveUpConfig>();
        _paceConfig = _remoteConfigModule.GetDataClassNew<PaceConfig>();
    }

    public override void OnEnter()
    {
        base.OnEnter();

        OnEnterAsync();
    }

    private async UniTaskVoid OnEnterAsync()
    {
        _interactionModule.EnableRestriction();

        await UniTask.Delay(TimeSpan.FromSeconds(1f));

        _audioModule.GetPlayer().Play(_audioModule.Sounds.Lose);
        _hapticModule.PlayWarning();

        if (!_paceConfig.SlowerWhenGiveUp)
            await _timeModule.AdjustTimeScale(0f, .6f, .2f);

        if (_giveUpConfig.ShowInLose)
        {
            var playingMonitor = _monitors.Get<PlayingMonitor>();
            var isGiveUp = await playingMonitor.StartGiveUpFlow();
            if (!isGiveUp) return;
        }

        var reviveSystem = _sceneModule.Container.Resolve<ReviveSystem>();

        var canRevive = reviveSystem.CanRevive();
        if (canRevive)
            _monitors.Activate<ReviveMonitor>();
        else
            Machine.RequestStateChange<GameLoseState>();
    }

    public override void OnExit()
    {
        base.OnExit();

        _interactionModule.DisableRestriction();
    }
}

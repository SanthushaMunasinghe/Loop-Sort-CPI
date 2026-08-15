using System.Collections.Generic;
using MessagePipe;
using StatefulUI.Runtime.Core;
using StatefulUISupport.Scripts.Components;
using VContainer;

public sealed class ReviveMultiplePageVariant : MonitorVariantBase
{
    [Inject] private EconomyModule _economyModule;
    [Inject] private EconomyMonitor _economyMonitor;
    [Inject] private HapticModule _hapticModule;
    [Inject] private GameMachine _gameMachine;
    [Inject] private SheetContainer _sheetContainer;
    [Inject] private SceneModule _sceneModule;
    [Inject] private RemoteConfigModule _remoteConfigModule;

    [Inject] private IPublisher<ReviveMessage> _revivePub;

    private SceneScope _sceneScope;
    private StatefulComponent _reasonView;
    private StatefulComponent _continueView;
    private StatefulComponent _offerView;
    private LoseReason _loseReason;
    private RewardedAdsConfig _rewardedAdsConfig;

    private readonly Queue<StateRole> _continueViewStates = new();

    public override void Setup()
    {
        base.Setup();

        _reasonView = GetInnerComponent(InnerComponentRole.Reason);
        _continueView = GetInnerComponent(InnerComponentRole.Continue);
        _offerView = _continueView.GetInnerComponent(InnerComponentRole.Offer);

        _reasonView.SetButtonListener(ButtonRole.Revive, OnReviveClicked);
        _reasonView.SetButtonListener(ButtonRole.GiveUp, OnReasonGiveUpClicked);
        _reasonView.SetButtonListener(ButtonRole.RV, OnRVClicked);
        _continueView.SetButtonListener(ButtonRole.Revive, OnReviveClicked);
        _continueView.SetButtonListener(ButtonRole.Close, OnContinueGiveUpClicked);
        _continueView.SetButtonListener(ButtonRole.RV, OnRVClicked);

        _rewardedAdsConfig = _remoteConfigModule.GetDataClassNew<RewardedAdsConfig>();
    }

    public override void OnActivated()
    {
        base.OnActivated();

        SetState(StateRole.Reason);
        var loseSystem = _sceneModule.Container.Resolve<LoseSystem>();
        _loseReason = loseSystem.GetLoseReason();
        var reasonState = _loseReason == LoseReason.ConveyorFull ? StateRole.Truck : StateRole.Ice;
        _reasonView.SetState(StateRole.Reset);
        _reasonView.SetState(reasonState);

        _sceneScope = _sceneModule.Scope;

        UpdateContinueResultView();
    }

    private void OnReviveClicked()
    {
        _revivePub.Publish(new ReviveMessage { LoseReason = _loseReason });
        _gameMachine.RequestStateChange<GamePlayingState>();
    }

    private void OnRVClicked()
    {
        _revivePub.Publish(new ReviveMessage { LoseReason = _loseReason, RewardedVideo = true });
        _gameMachine.RequestStateChange<GamePlayingState>();
    }

    private void OnReasonGiveUpClicked()
    {
        SetState(StateRole.Continue);
    }

    private void OnContinueGiveUpClicked()
    {
        if (_continueViewStates.TryDequeue(out var state))
        {
            _continueView.SetState(state);
            return;
        }

        _gameMachine.RequestStateChange<GameLoseState>();
    }

    private void UpdateContinueResultView()
    {
        _continueView.SetState(StateRole.Reset);

        if (_continueViewStates.TryDequeue(out var firstContinueState))
            _continueView.SetState(firstContinueState);
    }
}

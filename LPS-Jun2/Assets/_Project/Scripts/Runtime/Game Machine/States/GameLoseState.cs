using System.Collections.Generic;
using MessagePipe;
using VContainer;

public sealed class GameLoseState : GameStateBase
{
    [Inject] private Monitors _monitors;
    [Inject] private InteractionModule _interactionModule;
    [Inject] private EconomyModule _economyModule;
    [Inject] private SceneModule _sceneModule;
    [Inject] private TimeModule _timeModule;

    [Inject] private IPublisher<LevelLoseMessage> _levelLosePub;

    public override void OnEnter()
    {
        base.OnEnter();

        _timeModule.AdjustTimeScale(0f, .6f);

        Prefs.WinStreak.Value = 0;

        _levelLosePub.Publish(new LevelLoseMessage());

        var sceneResolver = _sceneModule.Container;
        var winSystem = sceneResolver.Resolve<WinSystem>();
        var remainingCarrierCount = winSystem.RemainingCarrierCount();

        var loseData = new Dictionary<string, object>
        {
            { "golds", _economyModule.GetAmount(Item.Gold) },
            { "remaining_carrier_count", remainingCarrierCount }
        };

        _interactionModule.EnableRestriction();
        _economyModule.TrackItems();

        _monitors.Activate<LoseMonitor>();
    }

    public override void OnExit()
    {
        base.OnExit();

        _interactionModule.DisableRestriction();
    }
}

public struct LevelLoseMessage
{
}

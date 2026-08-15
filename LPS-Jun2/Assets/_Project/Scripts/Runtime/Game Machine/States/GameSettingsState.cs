using VContainer;

public sealed class GameSettingsState : GameStateBase
{
    [Inject] private Monitors _monitors;
    [Inject] private TimeModule _timeModule;

    public override void OnEnter()
    {
        base.OnEnter();

        _timeModule.Pause();
        _monitors.Activate<SettingsPopupMonitor>();
        _monitors.Get<SettingsPopupMonitor>().SetState(StateRole.Buttons);
    }

    public override void OnExit()
    {
        base.OnExit();

        _timeModule.Resume();
    }
}
using VContainer;

public sealed class GamePlayingState : GameStateBase
{
    [Inject] private Monitors _monitors;
    [Inject] private TimeModule _timeModule;
    [Inject] private AudioModule _audioModule;
    [Inject] private InteractionModule _interactionModule;

    public override void OnEnter()
    {
        base.OnEnter();

        _timeModule.Resume();
        _monitors.Activate<PlayingMonitor>(NoTransition.Instance);

        _audioModule.UnPauseMusic();

        var level = Prefs.Level.Value + 1;
        if (level == 1 || level == 2)
        {
            _monitors.DisableRaycaster(this);
            _interactionModule.EnableRestriction(this);
        }
    }

    public override void OnExit()
    {
        base.OnExit();

        _audioModule.PauseMusic();

        var level = Prefs.Level.Value + 1;
        if (level == 1 || level == 2)
        {
            _monitors.RestoreRaycaster(this);
            _interactionModule.DisableRestriction(this);
        }
    }
}
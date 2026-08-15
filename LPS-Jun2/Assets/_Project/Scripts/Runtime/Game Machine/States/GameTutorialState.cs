using VContainer;

public sealed class GameTutorialState : GameStateBase
{
    [Inject] private TutorialModule _tutorialModule;
    [Inject] private Monitors _monitors;
    [Inject] private InteractionModule _interactionModule;
    [Inject] private AudioModule _audioModule;

    public override void OnEnter()
    {
        base.OnEnter();

        _interactionModule.EnableRestriction(this);
        _monitors.DisableRaycaster(this);
        _monitors.DisableDragging(this);

        _audioModule.UnPauseMusic();
    }

    public override void OnExit()
    {
        base.OnExit();

        _tutorialModule.Cancel();

        _interactionModule.DisableRestriction(this);
        _monitors.RestoreRaycaster(this);
        _monitors.RestoreDragging(this);

        _monitors.Get<HighlightMonitor>().ClearMask();

        _audioModule.PauseMusic();
    }
}
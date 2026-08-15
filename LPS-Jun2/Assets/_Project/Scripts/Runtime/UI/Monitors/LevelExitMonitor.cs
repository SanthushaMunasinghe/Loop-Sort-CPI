using System;
using VContainer;

public sealed class LevelExitMonitor : MonitorBase
{
    [Inject] private GameMachine _gameMachine;
    [Inject] private SceneModule _sceneModule;

    public override void Setup()
    {
        base.Setup();

        SetButtonListener(ButtonRole.Confirm, OnConfirmClicked);
        SetButtonListener(ButtonRole.Close, OnCloseClicked);
    }

    private void OnConfirmClicked()
    {
        Prefs.WinStreak.Value = 0;
    }

    private void OnCloseClicked()
    {
        _gameMachine.RequestStateChange(nameof(GamePlayingState));
    }

    public void SetConfirmCallback(Action callback)
    {
    }
}

using System.Collections.Generic;
using VContainer;

public sealed class SettingsPopupMonitor : MonitorBase
{
    [Inject] private SettingsModule _settingsModule;
    [Inject] private GameMachine _gameMachine;
    [Inject] private SceneModule _sceneModule;

    private ToggleElement _soundElement;
    private ToggleElement _musicElement;
    private ToggleElement _hapticElement;
    private ToggleElement _colorBlindElement;

    public override void Setup()
    {
        base.Setup();

        SetButtonListener(ButtonRole.Close, OnCloseClicked);
        SetButtonListener(ButtonRole.Home, OnHomeClicked);
        SetButtonListener(ButtonRole.Restart, OnRestartClicked);

        _soundElement = GetObject(ObjectRole.Sound).GetComponent<ToggleElement>();
        _musicElement = GetObject(ObjectRole.Music).GetComponent<ToggleElement>();
        _hapticElement = GetObject(ObjectRole.Haptic).GetComponent<ToggleElement>();
        _colorBlindElement = GetObject(ObjectRole.ColorBlind).GetComponent<ToggleElement>();

        _soundElement.AddSubscribe(isActive => _settingsModule.SetSound(isActive));
        _musicElement.AddSubscribe(isActive => _settingsModule.SetMusic(isActive));
        _hapticElement.AddSubscribe(isActive => _settingsModule.SetHaptic(isActive));
        _colorBlindElement.AddSubscribe(isActive => _settingsModule.SetColorBlind(isActive));
    }

    public override void OnActivated()
    {
        base.OnActivated();

        _soundElement.Initialize(_settingsModule.IsSoundActive());
        _musicElement.Initialize(_settingsModule.IsMusicActive());
        _colorBlindElement.Initialize(_settingsModule.IsColorBlindActive());
    }

    private void OnCloseClicked()
    {
        Monitors.Deactivate(this);
        if (_gameMachine.ActiveStateName == nameof(GameSettingsState))
            _gameMachine.RequestStateChange(nameof(GamePlayingState));
    }

    private void OnHomeClicked()
    {
        ExitToStart();
    }

    private void OnRestartClicked()
    {
        ExitToStart();
    }

    private void ExitToStart()
    {
        Prefs.WinStreak.Value = 0;
        _gameMachine.RequestStateChange<GameStartState>();
    }
}

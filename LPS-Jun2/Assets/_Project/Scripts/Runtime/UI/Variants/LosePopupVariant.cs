using VContainer;

public sealed class LosePopupVariant : MonitorVariantBase
{
    [Inject] private GameMachine _gameMachine;
    [Inject] private SceneModule _sceneModule;
    [Inject] private SheetContainer _sheetContainer;

    public override void Setup()
    {
        base.Setup();

        SetButtonListener(ButtonRole.TryAgain, OnCloseClicked);
        SetButtonListener(ButtonRole.Close, OnCloseClicked);
    }

    public override void OnActivated()
    {
        base.OnActivated();

        var themeType = _sceneModule.Container.Resolve<ThemeType>();

        SetState(StateRole.Reset);
        var customState = themeType switch
        {
            ThemeType.Hard => StateRole.Hard,
            ThemeType.SuperHard => StateRole.SuperHard,
            _ => StateRole.None
        };
        if (customState != StateRole.None) SetState(customState);

        SetFormattedText(TextRole.CurrentLevel, (Prefs.Level.Value + 1).ToString());

        var rewardConstant = themeType switch
        {
            ThemeType.Default => GameConstant.WinReward,
            ThemeType.Hard => GameConstant.HardLevelReward,
            ThemeType.SuperHard => GameConstant.SuperHardLevelReward,
            _ => GameConstant.WinReward
        };
        var reward = _sheetContainer.Constants.GetInt(rewardConstant);
        SetText(TextRole.Reward, reward.ToString());
    }

    private void OnCloseClicked()
    {
        _gameMachine.RequestStateChange<GameStartState>();
    }
}

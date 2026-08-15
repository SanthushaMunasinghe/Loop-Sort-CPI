using MessagePipe;
using VContainer;

public sealed class ReviveSinglePageVariant : MonitorVariantBase
{
    [Inject] private EconomyModule _economyModule;
    [Inject] private EconomyMonitor _economyMonitor;
    [Inject] private HapticModule _hapticModule;
    [Inject] private GameMachine _gameMachine;
    [Inject] private SheetContainer _sheetContainer;
    [Inject] private SceneModule _sceneModule;

    private SceneScope _sceneScope;

    private readonly PlayerPrefsInt _level = Prefs.Level;

    [Inject] private IPublisher<ReviveMessage> _revivePub;

    public override void Setup()
    {
        base.Setup();

        SetButtonListener(ButtonRole.Revive, OnReviveClicked);
        SetButtonListener(ButtonRole.GiveUp, OnGiveUpClicked);
    }

    public override void OnActivated()
    {
        base.OnActivated();

        _sceneScope = _sceneModule.Scope;

        SetText(TextRole.CurrentLevel, $"Level {(_level.Value + 1).ToString()}");

        GetContainer(ContainerRole.Heart).Clear();
    }

    private void OnReviveClicked()
    {
        _revivePub.Publish(new ReviveMessage());
        _gameMachine.RequestStateChange<GamePlayingState>();
    }

    private void OnGiveUpClicked()
    {
        _gameMachine.RequestStateChange<GameLoseState>();
    }
}

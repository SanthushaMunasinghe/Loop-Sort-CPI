using MessagePipe;
using VContainer;

public sealed class TutorialSystem : SystemBase
{
    [Inject] private TutorialModule _tutorialModule;
    [Inject] private SheetContainer _sheetContainer;
    [Inject] private RemoteConfigModule _remoteConfigModule;

    [Inject] private ISubscriber<LevelTransitionCompleteMessage> _levelTransitionCompleteSub;
    [Inject] private ISubscriber<LevelAnimationEnterCompleteMessage> _levelAnimationEnterCompleteSub;

    private LevelAnimationConfig _levelAnimationConfig;

    public override void OnAwake()
    {
        base.OnAwake();

        _levelAnimationConfig = _remoteConfigModule.GetDataClassNew<LevelAnimationConfig>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _levelTransitionCompleteSub.Subscribe(OnLevelTransitionComplete).AddTo(bag);
        _levelAnimationEnterCompleteSub.Subscribe(OnLevelAnimationEnterComplete).AddTo(bag);
    }

    private void OnLevelTransitionComplete(LevelTransitionCompleteMessage m)
    {
        if (!_levelAnimationConfig.Enter) HandleTutorial();
    }

    private void OnLevelAnimationEnterComplete(LevelAnimationEnterCompleteMessage m)
    {
        if (_levelAnimationConfig.Enter) HandleTutorial();
    }

    private void HandleTutorial()
    {
        var level = Prefs.Level.Value + 1;
        if (level == 1) _tutorialModule.TryPlay<FirstLevelTutorial>();
        else if (level == 2) _tutorialModule.TryPlay<SecondLevelTutorial>();
        else if (_sheetContainer.Features.TryGetUnlockedFeature(level, out var unlockedFeature))
        {
            _tutorialModule.TryPlay<FeatureTutorial>(builder =>
            {
                builder.RegisterInstance(unlockedFeature);
            });
        }
    }
}
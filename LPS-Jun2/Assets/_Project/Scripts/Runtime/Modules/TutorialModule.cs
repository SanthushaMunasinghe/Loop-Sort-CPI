using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;

public sealed class TutorialModule : ModuleBase
{
    [Inject] private Monitors _monitors;
    [Inject] private GameMachine _gameMachine;

    private bool _playing;
    private CancellationTokenSource _playCts;

    public async UniTask<bool> TryPlay<TTutorial>(Action<IContainerBuilder> installation = null, int playCount = 1)
        where TTutorial : TutorialBase
    {
        if (_playing) return false;

        var tutorial = _monitors.Get<TTutorial>();

        if (installation != null)
        {
            var tutorialScope = LifetimeScope.Create(installation, name: typeof(TTutorial).Name + "Scope");
            tutorial.SetCustomScope(tutorialScope);
        }

        var saveKey = tutorial.GetSaveKey();
        var played = new PlayerPrefsInt(saveKey);
        if (played.Value >= playCount) return false;

        _playing = true;
        _monitors.Additive<TTutorial>();
        _gameMachine.RequestStateChange<GameTutorialState>();
        await UniTask.NextFrame();
        _playCts = new CancellationTokenSource();
        var tutorialDone = await tutorial.Play().AttachExternalCancellation(_playCts.Token).SuppressCancellationThrow();
        _playCts = null;
        _gameMachine.RequestStateChange<GamePlayingState>();
        _monitors.Deactivate<TTutorial>();
        _playing = false;

        if (!tutorialDone.Result) return false;
        played.Set(played.Value + 1);
        return true;
    }

    public void Cancel()
    {
        _playCts?.Cancel();
    }
}
using Cysharp.Threading.Tasks;
using LitMotion;
using MessagePipe;
using Unity.Cinemachine;
using VContainer;

public sealed class CameraTransitionSystem : SystemBase
{
    [Inject] private GameMachine _gameMachine;
    [Inject] private CinemachineGroupFraming _groupFraming;
    [Inject] private LevelTransitionData _levelTransitionData;

    [Inject] private ISubscriber<CameraUpdateCompleteMessage> _cameraUpdateCompleteSub;
    [Inject] private ISubscriber<LevelWinMessage> _levelWinSub;

    [Inject] private IPublisher<LevelTransitionCompleteMessage> _levelTransitionCompletePub;

    private bool _levelEnter;
    private bool _levelExit;

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _cameraUpdateCompleteSub.Subscribe(OnCameraUpdateComplete).AddTo(bag);
        _levelWinSub.Subscribe(OnLevelWin).AddTo(bag);
    }

    private void OnCameraUpdateComplete(CameraUpdateCompleteMessage m)
    {
        HandleLevelEnter();
    }

    private void OnLevelWin(LevelWinMessage m)
    {
        HandleLevelExit();
    }

    private async UniTaskVoid HandleLevelEnter()
    {
        if (_levelEnter) return;
        _levelEnter = true;

        if (_levelTransitionData.Enter)
        {
            var originalCenterOffset = _groupFraming.CenterOffset;
            var from = originalCenterOffset.AddX(1f);
            var to = originalCenterOffset;
            await LMotion.Create(from, to, .5f)
                .WithEase(Ease.OutSine)
                .WithDelay(.05f, skipValuesDuringDelay: false)
                .BindToCenterOffset(_groupFraming)
                .ToUniTask(SceneLoadToken);
        }

        await UniTask.NextFrame();

        _levelTransitionCompletePub.Publish(new LevelTransitionCompleteMessage());
    }

    private async UniTaskVoid HandleLevelExit()
    {
        if (_levelExit) return;
        _levelExit = true;

        if (_levelTransitionData.Exit)
        {
            var originalCenterOffset = _groupFraming.CenterOffset;
            var from = originalCenterOffset;
            var to = originalCenterOffset.AddX(-1f);
            await LMotion.Create(from, to, .5f)
                .WithEase(Ease.InOutSine)
                .WithDelay(1.5f)
                .BindToCenterOffset(_groupFraming)
                .ToUniTask(SceneLoadToken);
            _gameMachine.RequestStateChange<GameStartState>();
        }
    }
}

public struct LevelTransitionCompleteMessage
{
}
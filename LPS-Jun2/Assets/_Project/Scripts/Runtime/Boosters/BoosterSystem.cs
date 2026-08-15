using MessagePipe;
using VContainer;
using Object = UnityEngine.Object;

public sealed class BoosterSystem : SystemBase
{
    [Inject] private Boosters _boosters;
    [Inject] private SheetContainer _sheetContainer;
    [Inject] private SceneScope _sceneScope;
    [Inject] private InteractionModule _interactionModule;
    [Inject] private AudioModule _audioModule;
    [Inject] private EconomyModule _economyModule;

    [Inject] private IPublisher<BoosterSelectedMessage> _boosterSelected;
    [Inject] private IPublisher<BoosterCanceledMessage> _boosterCanceled;
    [Inject] private IPublisher<BoosterCompletedMessage> _boosterCompleted;

    [Inject] private ISubscriber<GameStateExitMessage> _gameStateExitSub;

    public BoosterBase BoosterInstance { get; private set; }
    public BoosterType SelectedType { get; private set; }
    public bool Selected => SelectedType != BoosterType.None;

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        base.BuildMessages(bag);

        _gameStateExitSub.Subscribe(OnGameStateExit).AddTo(bag);
    }

    public override void Dispose()
    {
        base.Dispose();

        _interactionModule.ClearTouchInterceptor();
    }

    public void Select(BoosterType boosterType)
    {
        if (!TryCancel()) return;

        SelectedType = boosterType;
        var data = _boosters.Get(boosterType);
        BoosterInstance = Object.Instantiate(data.BoosterPrefab);
        _interactionModule.SetTouchInterceptor(BoosterInstance);
        _boosterSelected.Publish(new BoosterSelectedMessage());
        _audioModule.GetPlayer().Play(_audioModule.Sounds.BoosterSelect);
    }

    public bool TryCancel()
    {
        if (!Selected) return true;
        if (!BoosterInstance.TryCancel()) return false;

        SelectedType = BoosterType.None;
        if (BoosterInstance != null) Object.Destroy(BoosterInstance.gameObject);
        BoosterInstance = null;
        _interactionModule.ClearTouchInterceptor();
        _boosterCanceled.Publish(new BoosterCanceledMessage());

        return true;
    }

    public void Complete()
    {
        if (!Selected) return;

        _economyModule.TryConsume(new EconomyModule.Transaction
        {
            Units = 1,
            Item = SelectedType.ToEconomyItem(),
        });

        var boosterType = SelectedType;
        SelectedType = BoosterType.None;
        Object.Destroy(BoosterInstance.gameObject);
        BoosterInstance = null;
        _interactionModule.ClearTouchInterceptor();
        _boosterCompleted.Publish(new BoosterCompletedMessage
        {
            BoosterType = boosterType
        });
    }

    public bool CanSelect(BoosterType boosterType)
    {
        var data = _boosters.Get(boosterType);
        return data.BoosterPrefab.CanSelect(_sceneScope.Container);
    }

    private void OnGameStateExit(GameStateExitMessage m)
    {
        if (m.State is GamePlayingState) TryCancel();
    }
}

public struct BoosterSelectedMessage
{
}

public struct BoosterCanceledMessage
{
}

public struct BoosterCompletedMessage
{
    public BoosterType BoosterType;
}
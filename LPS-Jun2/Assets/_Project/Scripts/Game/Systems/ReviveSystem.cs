using MessagePipe;
using Scellecs.Morpeh;
using VContainer;

public sealed class ReviveSystem : SystemBase
{
    [Inject] private CapacityBoosterSystem _capacityBoosterSystem;
    [Inject] private LoseSystem _loseSystem;

    [Inject] private ISubscriber<ReviveMessage> _reviveSub;

    private Stash<BehaviourView<IceCarrier>> _iceCarriers;

    public override void OnAwake()
    {
        base.OnAwake();

        _iceCarriers = World.GetStash<BehaviourView<IceCarrier>>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _reviveSub.Subscribe(OnRevive).AddTo(bag);
    }

    private void OnRevive(ReviveMessage m)
    {
        if (m.LoseReason == LoseReason.ConveyorFull)
        {
            _capacityBoosterSystem.IncreaseCapacity();
        }
        else if (m.LoseReason == LoseReason.CantCompleteCarrier)
        {
            foreach (IceCarrier iceCarrier in _iceCarriers)
            {
                if (iceCarrier.IsBroken()) continue;
                iceCarrier.Break();
            }
        }
    }

    public bool CanRevive()
    {
        if (_capacityBoosterSystem.HasCapacityCarrier())
            return true;

        if (!_loseSystem.IsConveyorFull())
        {
            foreach (IceCarrier iceCarrier in _iceCarriers)
            {
                if (iceCarrier.IsBroken()) continue;
                return true;
            }
        }

        return false;
    }
}
using System;
using MessagePipe;
using Scellecs.Morpeh;
using VContainer;

public sealed class IceCarrierSystem : SystemBase
{
    [Inject] private ISubscriber<CarrierCompleteMessage> _carrierCompleteSub;

    private Stash<BehaviourView<IceCarrier>> _iceCarriers;

    public override void OnAwake()
    {
        base.OnAwake();

        _iceCarriers = World.GetStash<BehaviourView<IceCarrier>>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _carrierCompleteSub.Subscribe(OnCarrierComplete).AddTo(bag);
    }

    private void OnCarrierComplete(CarrierCompleteMessage m)
    {
        HandleComplete(m.Carrier);
    }

    private void HandleComplete(Carrier completedCarrier)
    {
        foreach (IceCarrier iceCarrier in _iceCarriers)
        {
            if (iceCarrier.IsBroken()) continue;
            if (!iceCarrier.MatchesRequiredCarrierType(completedCarrier)) continue;
            iceCarrier.Break();
        }
    }
}
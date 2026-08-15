using System;
using MessagePipe;
using Scellecs.Morpeh;
using VContainer;

public sealed class HiddenCarrierSystem : SystemBase
{
    [Inject] private ISubscriber<CarrierCompleteMessage> _carrierCompleteSub;

    private Stash<BehaviourView<HiddenCarrier>> _hiddenCarriers;

    public override void OnAwake()
    {
        base.OnAwake();

        _hiddenCarriers = World.GetStash<BehaviourView<HiddenCarrier>>();
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
        foreach (HiddenCarrier hiddenCarrier in _hiddenCarriers)
        {
            if (hiddenCarrier.IsRevealed()) continue;
            var completeColorType = completedCarrier.GetCompleteColorType();
            var requiredColorType = hiddenCarrier.GetRequiredColorType();
            if (requiredColorType != completeColorType) continue;
            hiddenCarrier.Reveal();
        }
    }
}
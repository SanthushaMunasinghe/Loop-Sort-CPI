using MessagePipe;
using Scellecs.Morpeh;
using VContainer;

public sealed class TagCarrierSystem : SystemBase
{
    [Inject] private ISubscriber<LevelBuildCompleteMessage> _levelBuildCompleteSub;
    [Inject] private ISubscriber<CarrierInteractUpdateMessage> _carrierInteractUpdateSub;
    [Inject] private ISubscriber<CarrierCompleteMessage> _carrierCompleteSub;

    private Stash<BehaviourView<TagCarrier>> _tagCarriers;

    public override void OnAwake()
    {
        base.OnAwake();

        _tagCarriers = World.GetStash<BehaviourView<TagCarrier>>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        base.BuildMessages(bag);

        _levelBuildCompleteSub.Subscribe(OnLevelBuildComplete).AddTo(bag);
        _carrierInteractUpdateSub.Subscribe(OnCarrierInteractUpdate).AddTo(bag);
        _carrierCompleteSub.Subscribe(OnCarrierComplete).AddTo(bag);
    }

    private void OnLevelBuildComplete(LevelBuildCompleteMessage m)
    {
        foreach (TagCarrier tagCarrier in _tagCarriers)
        {
            tagCarrier.AdjustRotation();
        }
    }

    private void OnCarrierInteractUpdate(CarrierInteractUpdateMessage m)
    {
        var carrier = m.Carrier;
        if (!_tagCarriers.Has(carrier)) return;
        TagCarrier tagCarrier = _tagCarriers.Get(carrier);
        if (m.CanInteract)
            tagCarrier.SetModelActive();
        else
            tagCarrier.SetModelInactive();
    }

    private void OnCarrierComplete(CarrierCompleteMessage m)
    {
        var carrier = m.Carrier;
        if (!_tagCarriers.Has(carrier)) return;
        TagCarrier tagCarrier = _tagCarriers.Get(carrier);
        tagCarrier.ApplyCompleteMotion();
    }
}
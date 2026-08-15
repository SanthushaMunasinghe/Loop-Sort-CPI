using MessagePipe;
using VContainer;

public sealed class RockCarrierSystem : SystemBase
{
    [Inject] private ISubscriber<CarrierCompleteMessage> _carrierCompleteSub;

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _carrierCompleteSub.Subscribe(OnCarrierComplete).AddTo(bag);
    }

    private void OnCarrierComplete(CarrierCompleteMessage m)
    {
        HandleCompleteMotion(m.Carrier);
    }

    private void HandleCompleteMotion(Carrier completedCarrier)
    {
        if (!completedCarrier.TryGetComponent<RockCarrier>(out var rockCarrier)) return;
        rockCarrier.ApplyCompleteMotion();
    }
}
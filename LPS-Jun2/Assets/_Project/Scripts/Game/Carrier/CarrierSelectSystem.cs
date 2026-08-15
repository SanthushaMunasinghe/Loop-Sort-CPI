using System;
using MessagePipe;
using VContainer;

public sealed class CarrierSelectSystem : SystemBase
{
    [Inject] private IPublisher<BlockTransferMessage> _blockTransferPub;
    [Inject] private ISubscriber<CarrierSelectMessage> _carrierSelectSub;

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _carrierSelectSub.Subscribe(OnCarrierSelect).AddTo(bag);
    }

    private void OnCarrierSelect(CarrierSelectMessage m)
    {
        if (m.Carrier.IsComplete()) return;
        if (m.Carrier.IsEmpty()) return;
        Transfer(m.Carrier);
    }

    private void Transfer(Carrier from)
    {
        _blockTransferPub.Publish(new BlockTransferMessage
        {
            Carrier = from,
        });
    }
}

public struct BlockTransferMessage
{
    public Carrier Carrier;
}
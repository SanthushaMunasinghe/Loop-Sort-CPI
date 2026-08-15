using System;
using MessagePipe;
using Scellecs.Morpeh;
using VContainer;

public sealed class BlockDarkSystem : SystemBase
{
    [Inject] private ISubscriber<BlockNextTransferUpdateMessage> _blockNextTransferUpdateSub;

    private Stash<BlockNextTransfer> _blockNextTransfers;

    public override void OnAwake()
    {
        base.OnAwake();

        _blockNextTransfers = World.GetStash<BlockNextTransfer>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _blockNextTransferUpdateSub.Subscribe(OnBlockNextTransferUpdate).AddTo(bag);
    }

    private void OnBlockNextTransferUpdate(BlockNextTransferUpdateMessage m)
    {
        // var block = m.Block;
        // var inCarrier = block.Container is Carrier;
        // if (!inCarrier || _blockNextTransfers.Has(block))
        //     block.SetNormalColor();
        // else
        //     block.SetDarkColor();
    }
}
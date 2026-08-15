using System.Collections.Generic;
using MessagePipe;
using VContainer;

public sealed class UndoBoosterSystem : SystemBase
{
    [Inject] private ISubscriber<BlockTransferCompleteMessage> _blockTransferCompleteSub;

    private Carrier _previousCarrier;
    private List<Block> _blocks;

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _blockTransferCompleteSub.Subscribe(OnBlockTransferComplete).AddTo(bag);
    }

    private void OnBlockTransferComplete(BlockTransferCompleteMessage m)
    {
        _previousCarrier = m.Carrier;
        _blocks = m.Blocks;
    }

    public bool CanUndo()
    {
        if (_previousCarrier == null) return false;
        if (_blocks == null) return false;
        if (_previousCarrier.IsComplete()) return false;

        var availableSpaceCount = _previousCarrier.GetAvailableSpaceCount();
        if (_blocks.Count > availableSpaceCount) return false;

        var firstBlock = _blocks[0];
        if (firstBlock.Container is Carrier carrier)
            if (carrier.IsComplete() || carrier.IsTransferringOrAddingBlocks())
                return false;

        return true;
    }

    public void Undo()
    {
        _blocks.Reverse();
        foreach (var block in _blocks)
        {
            _previousCarrier.AddBlock(block);
        }

        _previousCarrier = null;
        _blocks = null;
    }
}
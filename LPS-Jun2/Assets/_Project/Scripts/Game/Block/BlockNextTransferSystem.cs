using Cysharp.Threading.Tasks;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine.Pool;
using VContainer;
using IComponent = Scellecs.Morpeh.IComponent;

public sealed class BlockNextTransferSystem : SystemBase
{
    [Inject] private ISubscriber<BlockTransferStartMessage> _blockTransferStartSub;
    [Inject] private ISubscriber<BlockTransferCompleteMessage> _blockTransferCompleteSub;
    [Inject] private ISubscriber<CarrierAddBlockMessage> _carrierAddBlockSub;
    [Inject] private ISubscriber<LevelAnimationEnterCompleteMessage> _levelAnimationEnterCompleteSub;

    [Inject] private IPublisher<BlockNextTransferUpdateMessage> _blockNextTransferUpdatePub;

    private Stash<BlockNextTransfer> _blockNextTransfers;
    private Stash<BehaviourView<Carrier>> _carriers;

    private bool _isBlockCreateComplete;

    public override void OnAwake()
    {
        base.OnAwake();

        _blockNextTransfers = World.GetStash<BlockNextTransfer>();
        _carriers = World.GetStash<BehaviourView<Carrier>>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _blockTransferStartSub.Subscribe(OnBlockTransferStart).AddTo(bag);
        _blockTransferCompleteSub.Subscribe(OnBlockTransferComplete).AddTo(bag);
        _carrierAddBlockSub.Subscribe(OnCarrierAddBlock).AddTo(bag);
        _levelAnimationEnterCompleteSub.Subscribe(OnLevelAnimationEnterComplete).AddTo(bag);
    }

    private void OnBlockTransferStart(BlockTransferStartMessage m)
    {
        foreach (var block in m.Blocks)
        {
            if (_blockNextTransfers.Has(block))
                _blockNextTransfers.Remove(block);

            _blockNextTransferUpdatePub.Publish(new BlockNextTransferUpdateMessage
            {
                Carrier = m.Carrier,
                Block = block,
                IsNextTransfer = false
            });
        }
    }

    private void OnBlockTransferComplete(BlockTransferCompleteMessage m)
    {
        RefreshNextTransferBlocks(m.Carrier);
    }

    private void OnCarrierAddBlock(CarrierAddBlockMessage m)
    {
        if (!_isBlockCreateComplete) return;
        RefreshNextTransferBlocks(m.Carrier);
    }

    private void OnLevelAnimationEnterComplete(LevelAnimationEnterCompleteMessage m)
    {
        HandleCarrierBlockCreateComplete();
    }

    private async UniTaskVoid HandleCarrierBlockCreateComplete()
    {
        await UniTask.NextFrame(SceneLoadToken);
        _isBlockCreateComplete = true;
        foreach (Carrier carrier in _carriers)
            RefreshNextTransferBlocks(carrier);
    }

    private void RefreshNextTransferBlocks(Carrier carrier)
    {
        using var p = HashSetPool<Block>.Get(out var nextTransferBlocks);
        if (!carrier.IsTransferringOrAddingBlocks())
            carrier.FindNextTransferBlocks(nextTransferBlocks);

        foreach (var block in carrier.GetBlocks())
        {
            var isNextTransfer = false;
            if (nextTransferBlocks.Contains(block))
            {
                isNextTransfer = true;
                if (!_blockNextTransfers.Has(block))
                    _blockNextTransfers.Add(block);
            }
            else
            {
                if (_blockNextTransfers.Has(block))
                    _blockNextTransfers.Remove(block);
            }

            _blockNextTransferUpdatePub.Publish(new BlockNextTransferUpdateMessage
            {
                Carrier = carrier,
                Block = block,
                IsNextTransfer = isNextTransfer
            });
        }
    }
}

public struct BlockNextTransfer : IComponent
{
}

public struct BlockNextTransferUpdateMessage
{
    public Carrier Carrier;
    public Block Block;
    public bool IsNextTransfer;
}
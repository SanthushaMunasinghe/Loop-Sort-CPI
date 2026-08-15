using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer;

public sealed class CarrierOutlineSystem : SystemBase
{
    [Inject] private ISubscriber<BlockTransferStartMessage> _blockTransferStartSub;
    [Inject] private ISubscriber<BlockTransferCompleteMessage> _blockTransferCompleteSub;

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _blockTransferStartSub.Subscribe(OnBlockTransferStart).AddTo(bag);
        _blockTransferCompleteSub.Subscribe(OnBlockTransferComplete).AddTo(bag);
    }

    private void OnBlockTransferStart(BlockTransferStartMessage m)
    {
        HandleOutline(m).Forget();
    }

    private void OnBlockTransferComplete(BlockTransferCompleteMessage m)
    {
        // _outlineEffect.RemoveGameObject(m.Carrier.ModelParent);
    }

    private async UniTaskVoid HandleOutline(BlockTransferStartMessage m)
    {
        // _outlineEffect.AddGameObject(m.Carrier.ModelParent, 1);
        await UniTask.Delay(TimeSpan.FromSeconds(.3f));
        // _outlineEffect.RemoveGameObject(m.Carrier.ModelParent);
    }
}
using LitMotion;
using LitMotion.Extensions;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public sealed class TunnelCarrierSystem : SystemBase
{
    [Inject] private ISubscriber<CarrierBlockCreateCompleteMessage> _carrierBlockCreateCompleteSub;
    [Inject] private ISubscriber<BlockTransferCompleteMessage> _blockTransferCompleteSub;
    [Inject] private ISubscriber<LevelAnimationEnterStartMessage> _levelAnimationEnterStartSub;
    [Inject] private ISubscriber<LevelAnimationCarrierCompleteMessage> _levelAnimationCarrierCompleteSub;

    [Inject] private Conveyor _conveyor;

    private Stash<BehaviourView<Carrier>> _carriers;
    private Stash<BehaviourView<TunnelCarrier>> _tunnelCarriers;
    private Filter _tunnelCarrierFilter;

    public override void OnAwake()
    {
        base.OnAwake();

        _carriers = World.GetStash<BehaviourView<Carrier>>();
        _tunnelCarriers = World.GetStash<BehaviourView<TunnelCarrier>>();
        _tunnelCarrierFilter = World.Filter.With<BehaviourView<TunnelCarrier>>().Build();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        base.BuildMessages(bag);

        _carrierBlockCreateCompleteSub.Subscribe(OnCarrierBlockCreateComplete).AddTo(bag);
        _blockTransferCompleteSub.Subscribe(OnBlockTransferComplete).AddTo(bag);
        _levelAnimationEnterStartSub.Subscribe(OnLevelAnimationEnterStart).AddTo(bag);
        _levelAnimationCarrierCompleteSub.Subscribe(OnLevelAnimationCarrierComplete).AddTo(bag);
    }

    private void OnCarrierBlockCreateComplete(CarrierBlockCreateCompleteMessage m)
    {
        SetBlockPositions();
    }

    private void OnBlockTransferComplete(BlockTransferCompleteMessage m)
    {
        if (_tunnelCarrierFilter.IsEmpty()) return;

        var carrier = m.Carrier;
        if (!carrier.TryGetComponent<TunnelCarrier>(out var tunnelCarrier)) return;
        EnableNextTransferBlocks(carrier);
        tunnelCarrier.UpdateView();
    }

    private void OnLevelAnimationEnterStart(LevelAnimationEnterStartMessage m)
    {
        foreach (TunnelCarrier carrier in _tunnelCarriers)
        {
            carrier.OnLevelAnimationStart();
        }
    }

    private void OnLevelAnimationCarrierComplete(LevelAnimationCarrierCompleteMessage m)
    {
        foreach (TunnelCarrier carrier in _tunnelCarriers)
        {
            carrier.OnLevelAnimationEnd();
        }
    }

    private void EnableNextTransferBlocks(Carrier carrier)
    {
        using var p = ListPool<Block>.Get(out var nextTransferBlocks);
        carrier.FindNextTransferBlocks(nextTransferBlocks);
        var centerPoint = nextTransferBlocks.GetCenterPoint();
        var blocks = carrier.GetBlocks();
        foreach (var block in blocks)
        {
            var isNextTransferBlock = nextTransferBlocks.Contains(block);
            block.gameObject.SetActive(isNextTransferBlock);
            if (!isNextTransferBlock) continue;
            TunnelCarrier tunnelCarrier = _tunnelCarriers.Get(carrier);
            tunnelCarrier.ApplyCoverMotion();
            var blockT = block.transform;
            var from = blockT.parent.worldToLocalMatrix.MultiplyPoint3x4(centerPoint - Vector3.up * 1.51f);
            var to = blockT.localPosition;
            LMotion.Create(from, to, .2f)
                .WithDelay(.2f, skipValuesDuringDelay: false)
                .BindToLocalPosition(blockT)
                .AddTo(block);
        }
    }

    private void SetBlockPositions()
    {
        foreach (var entity in _tunnelCarrierFilter)
        {
            Carrier carrier = _carriers.Get(entity);
            var blocks = carrier.GetBlocks();
            for (var i = 0; i < blocks.Count; i++)
            {
                var idx = i % _conveyor.GroupBlockCount;
                var coordinate = carrier.GetBlockCoordinate(idx);
                var localPosition = carrier.CoordinateToLocalPosition(coordinate);
                var block = blocks[i];
                var blockT = block.transform;
                blockT.localPosition = localPosition;
            }

            TunnelCarrier tunnelCarrier = _tunnelCarriers.Get(entity);
            EnableNextTransferBlocks(carrier);
            tunnelCarrier.UpdateView();
        }
    }
}
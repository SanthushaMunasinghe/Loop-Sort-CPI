using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LitMotion;
using MessagePipe;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public sealed class HiddenBlockSystem : SystemBase
{
    [Inject] private ISubscriber<BlockTransferStartMessage> _blockTransferStartSub;
    [Inject] private ISubscriber<BlockTransferCompleteMessage> _blockTransferCompleteSub;
    [Inject] private ISubscriber<ShuffleCarrierMessage> _shuffleCarrierSub;
    [Inject] private ISubscriber<CarrierCompleteMessage> _carrierCompleteSub;

    [Inject] private IPublisher<HiddenBlockRevealMessage> _hiddenBlockRevealPub;

    [Inject] private Particles _particles;
    [Inject] private PrefabModule _prefabModule;
    [Inject] private Materials _materials;
    [Inject] private Conveyor _conveyor;

    private readonly Dictionary<Carrier, Vector3> _positionByCarrier = new();
    private readonly Dictionary<Carrier, List<HiddenBlock>> _hiddenBlocksByCarrier = new();

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _blockTransferStartSub.Subscribe(OnBlockTransferStart).AddTo(bag);
        _blockTransferCompleteSub.Subscribe(OnBlockTransferComplete).AddTo(bag);
        _shuffleCarrierSub.Subscribe(OnShuffleCarrier).AddTo(bag);
        _carrierCompleteSub.Subscribe(OnCarrierComplete).AddTo(bag);
    }

    private void OnBlockTransferStart(BlockTransferStartMessage m)
    {
        HandleReveal(m.Carrier);
    }

    private void OnBlockTransferComplete(BlockTransferCompleteMessage m)
    {
        HandleOpen(m.Carrier);
    }

    private void OnShuffleCarrier(ShuffleCarrierMessage m)
    {
        HandleReveal(m.Carrier);
        HandleOpen(m.Carrier);
    }

    private void OnCarrierComplete(CarrierCompleteMessage m)
    {
        HandleComplete(m.Carrier);
        HandleOpen(m.Carrier);
    }

    private void HandleReveal(Carrier carrier)
    {
        var nextFirstBlock = carrier.GetNextFirstBlock();
        if (nextFirstBlock == null || nextFirstBlock.FeatureType != FeatureType.Hidden) return;

        using var p1 = ListPool<Block>.Get(out var blocks);
        carrier.FindBlocksWithSameColor(blocks);
        HandleHiddenBlocks(carrier, blocks);
    }

    private void HandleComplete(Carrier carrier)
    {
        var blocks = carrier.GetBlocks();
        HandleHiddenBlocks(carrier, blocks);
    }

    private void HandleHiddenBlocks(Carrier carrier, List<Block> blocks)
    {
        ListPool<HiddenBlock>.Get(out var hiddenBlocks);
        foreach (var block in blocks)
        {
            if (block.FeatureType != FeatureType.Hidden) continue;
            var hiddenBlock = block.GetComponent<HiddenBlock>();
            if (hiddenBlock.IsRevealed()) continue;
            hiddenBlock.Reveal();
            hiddenBlock.Open();
            hiddenBlocks.Add(hiddenBlock);
        }

        if (hiddenBlocks.Count == 0) return;
        var centerPoint = hiddenBlocks.GetCenterPoint();
        _positionByCarrier[carrier] = centerPoint;
        _hiddenBlocksByCarrier[carrier] = hiddenBlocks;

        var groupBlockCount = hiddenBlocks.Count / _conveyor.GroupBlockCount;
        _hiddenBlockRevealPub.Publish(new HiddenBlockRevealMessage
        {
            GroupBlockCount = groupBlockCount,
        });
    }

    private async UniTaskVoid HandleOpen(Carrier carrier)
    {
        await UniTask.Yield(cancellationToken: SceneLoadToken);
        HandleParticle(carrier);
        HandleDissolve(carrier);
    }

    private void HandleParticle(Carrier carrier)
    {
        if (!_positionByCarrier.TryGetValue(carrier, out var centerPoint)) return;
        _prefabModule.Rent(_particles.CarrierHiddenReveal, centerPoint + Vector3.up, carrier.transform.rotation);
        _positionByCarrier.Remove(carrier);
    }

    private void HandleDissolve(Carrier carrier)
    {
        if (!_hiddenBlocksByCarrier.TryGetValue(carrier, out var hiddenBlocks)) return;
        _hiddenBlocksByCarrier.Remove(carrier);

        using var p1 = HashSetPool<MeshRenderer>.Get(out var groupBlocks);
        foreach (var hiddenBlock in hiddenBlocks)
        {
            var groupBlock = carrier.GetGroupBlockOf(hiddenBlock.Block);
            if (groupBlocks.Contains(groupBlock)) continue;
            groupBlocks.Add(groupBlock);

            using var p2 = ListPool<Material>.Get(out var materials);
            materials.Add(hiddenBlock.Block.MeshRenderer.sharedMaterial);
            materials.Add(_materials.HiddenBlock);
            groupBlock.SetSharedMaterials(materials);

            LMotion.Create(0f, .5f, 1f)
                .WithEase(Ease.InOutSine)
                .BindToPropertyBlock(groupBlock, "_DissolveAmount", 1)
                .AddTo(carrier);
        }
        ListPool<HiddenBlock>.Release(hiddenBlocks);
    }
}

public struct HiddenBlockRevealMessage
{
    public int GroupBlockCount;
}
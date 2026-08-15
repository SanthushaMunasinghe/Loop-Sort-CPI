using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using Random = Freya.Random;

public sealed class ShuffleBoosterSystem : SystemBase
{
    [Inject] private Monitors _monitors;
    [Inject] private OutlineModule _outlineModule;

    [Inject] private IPublisher<ShuffleCarrierMessage> _shuffleCarrierPub;

    private Filter _enabledCarriers;
    private Stash<BehaviourView<Carrier>> _carriers;

    public override void OnAwake()
    {
        base.OnAwake();

        _enabledCarriers = World.Filter.With<Enabled>().With<BehaviourView<Carrier>>().Build();
        _carriers = World.GetStash<BehaviourView<Carrier>>();
    }

    public bool CanShuffle()
    {
        return true;
    }

    public bool TryShuffle(Carrier carrier)
    {
        if (carrier.IsEmpty()) return false;
        if (carrier.IsComplete()) return false;
        if (!carrier.CanBeginTransfer()) return false;
        if (carrier.IsTransferringOrAddingBlocks()) return false;
        if (carrier.AreAllBlocksSameColor()) return false;

        Shuffle(carrier);
        return true;
    }

    private async UniTask Shuffle(Carrier carrier)
    {
        using var p1 = ListPool<List<Block>>.Get(out var blockGroups);
        while (!carrier.IsEmpty())
        {
            var blocks = ListPool<Block>.Get();
            carrier.FindBlocksWithSameColor(blocks);
            blocks.Reverse();
            blockGroups.Add(blocks);

            foreach (var block in blocks)
                carrier.RemoveBlock(block);
        }

        blockGroups.Reverse();
        blockGroups.ShuffleDeranged();

        carrier.DisableGroupBlock();
        carrier.DisableTransfer(carrier.gameObject);

        using var p2 = ListPool<UniTask>.Get(out var tasks);
        foreach (var blocks in blockGroups)
        foreach (var block in blocks)
        {
            var blockT = block.transform;
            var fromPosition = blockT.position;
            carrier.AddBlock(block, motion: false);
            var toPosition = blockT.position;
            var task = ApplyShuffleGroupBlockMotion(carrier, block, fromPosition, toPosition);
            tasks.Add(task);
        }

        foreach (var blocks in blockGroups)
            ListPool<Block>.Release(blocks);

        await UniTask.WhenAll(tasks);

        _shuffleCarrierPub.Publish(new ShuffleCarrierMessage
        {
            Carrier = carrier
        });

        carrier.EnableGroupBlock();
        carrier.EnableTransfer(carrier.gameObject);
    }

    private async UniTask ApplyShuffleGroupBlockMotion(Carrier carrier, Block block, Vector3 fromPosition, Vector3 toPosition)
    {
        var carrierT = carrier.transform;
        var blockT = block.transform;
        var moveDirection = (toPosition - fromPosition).normalized;
        var crossDirection = Vector3.Cross(moveDirection, carrierT.up);
        var cross = crossDirection * .75f;
        var up = carrierT.up * 2f;

        await LMotion.Create(fromPosition, fromPosition + up, .2f)
            .WithEase(Ease.InOutSine)
            .BindToPosition(blockT)
            .AddTo(carrier);

        await LMotion.Create(blockT.position, blockT.position + cross, .3f)
            .WithEase(Ease.InOutBack)
            .BindToPosition(blockT)
            .AddTo(carrier);

        var moveSidePosition = toPosition + up + cross;
        var distance = Vector3.Distance(blockT.position, moveSidePosition);
        await LMotion.Create(blockT.position, moveSidePosition, distance * .16f)
            .WithEase(Ease.InOutSine)
            .BindToPosition(blockT)
            .AddTo(carrier);

        await LMotion.Create(blockT.position, toPosition + up, .15f)
            .WithEase(Ease.InOutSine)
            .BindToPosition(blockT)
            .AddTo(carrier);

        await LMotion.Create(blockT.position, toPosition, .1f)
            .BindToPosition(blockT)
            .AddTo(carrier);
    }

    private async UniTask ApplyShuffleSphereMotion(Carrier carrier, Block block, Vector3 fromPosition, Vector3 toPosition)
    {
        var carrierT = carrier.transform;
        var blockT = block.transform;
        var up = carrierT.up * 2f;

        await LMotion.Create(fromPosition, fromPosition + up, .4f)
            .WithEase(Ease.InOutSine)
            .BindToPosition(blockT)
            .AddTo(carrier);

        var centerPoint = carrierT.position + carrierT.forward * 1.8f + carrierT.up * 5f;
        var randomPosition = centerPoint + Random.InUnitSphere * 2f;
        await LMotion.Create(blockT.position, randomPosition, .5f)
            .WithEase(Ease.InOutBack)
            .BindToPosition(blockT)
            .AddTo(carrier);

        randomPosition = centerPoint + Random.InUnitSphere * 2f;
        await LMotion.Create(blockT.position, randomPosition, .5f)
            .WithEase(Ease.InOutBack)
            .BindToPosition(blockT)
            .AddTo(carrier);

        await LMotion.Create(blockT.position, toPosition + up, .3f)
            .WithEase(Ease.InOutSine)
            .BindToPosition(blockT)
            .AddTo(carrier);

        await LMotion.Create(blockT.position, toPosition, .1f)
            .BindToPosition(blockT)
            .AddTo(carrier);
    }

    public void ShowHighlight()
    {
        _monitors.DisableRaycaster(this);
        _outlineModule.DeactivateRender();

        var highlightMonitor = _monitors.Get<HighlightMonitor>();
        highlightMonitor.ShowMask(alpha: .95f);

        foreach (var entity in _enabledCarriers)
        {
            var carrier = _carriers.Get(entity).Behaviour;
            if (!carrier.CanBeginTransfer()) continue;
            carrier.Highlight.SetActive(true);
        }
    }

    public void HideHighlight()
    {
        _monitors.RestoreRaycaster(this);
        _outlineModule.ActivateRender();

        var highlightMonitor = _monitors.Get<HighlightMonitor>();
        highlightMonitor.HideMask();

        foreach (var entity in _enabledCarriers)
        {
            var carrier = _carriers.Get(entity).Behaviour;
            carrier.Highlight.SetActive(false);
        }
    }
}

public struct ShuffleCarrierMessage
{
    public Carrier Carrier;
}
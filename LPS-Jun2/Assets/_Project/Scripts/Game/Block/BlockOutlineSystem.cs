using System.Collections.Generic;
using MessagePipe;
using UnityEngine;
using VContainer;

public sealed class BlockOutlineSystem : SystemBase
{
    [Inject] private PrefabModule _prefabModule;
    [Inject] private BlockConfig _blockConfig;
    [Inject] private Conveyor _conveyor;
    [Inject] private OutlineModule _outlineModule;

    [Inject] private ISubscriber<BlockNextTransferUpdateMessage> _blockNextTransferUpdateSub;

    private readonly Dictionary<GameObject, OutlineEntity> _outlines = new();

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _blockNextTransferUpdateSub.Subscribe(OnBlockNextTransferUpdate).AddTo(bag);
    }

    private void OnBlockNextTransferUpdate(BlockNextTransferUpdateMessage m)
    {
        var block = m.Block;
        if (block.Container is not Carrier carrier)
        {
            RemoveOutlineInstance(block.gameObject);
            return;
        }

        var idxOfBlock = carrier.GetBlockIdx(block);
        var groupBlockIdx = idxOfBlock / _conveyor.GroupBlockCount;
        var groupBlock = groupBlockIdx >= carrier.GroupBlocks.Count ? null : carrier.GroupBlocks[groupBlockIdx];
        var isActive = m.IsNextTransfer;
        if (carrier.CanComplete()) isActive = false;

        if (isActive)
        {
            if (groupBlock != null)
            {
                var mesh = carrier.GroupBlockFilters[groupBlockIdx].sharedMesh;
                CreateOutlineInstance(groupBlock.gameObject, mesh);
            }

            if (block.MeshRenderer.enabled)
                CreateOutlineInstance(block.gameObject, block.MeshFilter.sharedMesh);
            else
                RemoveOutlineInstance(block.gameObject);
        }
        else
        {
            if (groupBlock != null) RemoveOutlineInstance(groupBlock.gameObject);
            RemoveOutlineInstance(block.gameObject);
        }
    }

    private void CreateOutlineInstance(GameObject target, Mesh mesh)
    {
        if (_outlines.ContainsKey(target)) return;
        var outlineEntity = _outlineModule.Add(target, mesh, Color.black, 2f, OutlineRenderMode.Visible);
        _outlines[target] = outlineEntity;
    }

    private void RemoveOutlineInstance(GameObject target)
    {
        if (!_outlines.TryGetValue(target, out var outlineEntity)) return;
        _outlineModule.Remove(outlineEntity);
        _outlines.Remove(target);
    }
}
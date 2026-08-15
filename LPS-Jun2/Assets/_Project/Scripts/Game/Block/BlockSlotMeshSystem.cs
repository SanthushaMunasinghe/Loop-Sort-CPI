using System.Collections.Generic;
using Dreamteck.Splines;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine.Pool;
using VContainer;

public sealed class BlockSlotMeshSystem : SystemBase
{
    [Inject] private BlockConfig _blockConfig;
    [Inject] private SplineComputer _splineComputer;
    [Inject] private PrefabModule _prefabModule;
    [Inject] private Conveyor _conveyor;
    [Inject] private RemoteConfigModule _remoteConfigModule;

    [Inject] private ISubscriber<ConveyorSlotAddBlockMessage> _conveyorSlotAddBlockSub;
    [Inject] private ISubscriber<ConveyorSlotRemoveBlockMessage> _conveyorSlotRemoveBlockSub;

    private Stash<ConveyorSlotLink> _conveyorSlotLinks;
    private BlockPhysicsConfig _blockPhysicsConfig;

    private readonly List<ConveyorBlock> _conveyorBlocks = new();

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _blockPhysicsConfig = _remoteConfigModule.GetDataClassNew<BlockPhysicsConfig>();

        if (_blockPhysicsConfig.Type != BlockPhysicsConfig.PhysicsType.None) return;

        _conveyorSlotAddBlockSub.Subscribe(OnConveyorSlotAddBlock).AddTo(bag);
        _conveyorSlotRemoveBlockSub.Subscribe(OnConveyorSlotRemoveBlock).AddTo(bag);
    }

    private void OnConveyorSlotAddBlock(ConveyorSlotAddBlockMessage m)
    {
        m.Block.MeshRenderer.enabled = false;
    }

    private void OnConveyorSlotRemoveBlock(ConveyorSlotRemoveBlockMessage m)
    {
        m.Block.MeshRenderer.enabled = true;
    }

    public override void OnAwake()
    {
        base.OnAwake();

        _conveyorSlotLinks = World.GetStash<ConveyorSlotLink>();
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        if (_blockPhysicsConfig.Type != BlockPhysicsConfig.PhysicsType.None) return;

        using var p1 = HashSetPool<ConveyorSlot>.Get(out var hashSet);
        using var p2 = ListPool<ConveyorSlot>.Get(out var list);

        var isSplineClosed = _splineComputer.isClosed;
        var conveyorBlockIdx = 0;
        var collisionDistance = _conveyor.SlotCollisionDistance + .15f;
        foreach (var link in _conveyorSlotLinks)
        {
            var slot = link.Current;
            if (hashSet.Contains(slot)) continue;
            if (slot.IsElementBeingAdded()) continue;

            list.Add(slot);
            hashSet.Add(slot);

            var next = link.Next;
            while (slot.IsCompatibleWith(next))
            {
                if (hashSet.Contains(next)) break;
                if (next.IsElementBeingAdded()) break;

                var nextLink = _conveyorSlotLinks.Get(next);
                var distance = isSplineClosed ? nextLink.PreviousDistanceUnclamped : nextLink.PreviousDistance;
                if (distance > collisionDistance) break;

                list.Add(next);
                hashSet.Add(next);

                next = nextLink.Next;
            }

            var previous = link.Previous;
            while (slot.IsCompatibleWith(previous))
            {
                if (hashSet.Contains(previous)) break;
                // if (previous.IsElementBeingAdded()) break;

                var previousLink = _conveyorSlotLinks.Get(previous);
                var distance = isSplineClosed ? previousLink.NextDistanceUnclamped : previousLink.NextDistance;
                if (distance > collisionDistance) break;

                list.Insert(0, previous);
                hashSet.Add(previous);

                previous = previousLink.Previous;
            }

            if (!isSplineClosed)
            {
                list.Sort((left, right) =>
                {
                    var leftPercent = left.SplineFollower.GetPercent();
                    var rightPercent = right.SplineFollower.GetPercent();
                    return leftPercent.CompareTo(rightPercent);
                });
            }

            var first = list[0];
            var last = list[^1];

            var conveyorBlock = _conveyorBlocks.Count > conveyorBlockIdx
                ? _conveyorBlocks[conveyorBlockIdx]
                : _prefabModule.Rent(_blockConfig.ConveyorBlock);

            var firstBlock = slot.GetFirstBlock();
            if (firstBlock != null)
                firstBlock.InjectColorType(firstBlock.ColorType, conveyorBlock.gameObject);

            conveyorBlockIdx++;
            if (!_conveyorBlocks.Contains(conveyorBlock))
                _conveyorBlocks.Add(conveyorBlock);

            conveyorBlock.Set(_splineComputer, first, last);

            list.Clear();
        }

        for (var i = _conveyorBlocks.Count - 1; i >= conveyorBlockIdx; i--)
        {
            var conveyorBlock = _conveyorBlocks[i];
            _prefabModule.Return(conveyorBlock.gameObject);
            _conveyorBlocks.RemoveAt(i);
        }
    }
}
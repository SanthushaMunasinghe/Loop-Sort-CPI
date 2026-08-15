using System.Collections.Generic;
using Dreamteck.Splines;
using Scellecs.Morpeh;
using UnityEngine.Pool;
using VContainer;

public sealed class ConveyorSlotMergeSystem : SystemBase
{
    [Inject] private Conveyor _conveyor;
    [Inject] private SplineComputer _splineComputer;
    [Inject] private RemoteConfigModule _remoteConfigModule;

    private Stash<ConveyorSlotLink> _conveyorSlotLinks;
    private Stash<ConveyorBlockGroup> _conveyorBlockGroups;
    private Stash<BehaviourView<ConveyorSlot>> _conveyorSlots;

    private ConveyorSystemConfig _conveyorSystemConfig;
    private BlockPhysicsConfig _blockPhysicsConfig;

    private readonly HashSet<ConveyorSlot> _previousMergingSlots = new();

    public override void OnAwake()
    {
        base.OnAwake();

        _conveyorSlotLinks = World.GetStash<ConveyorSlotLink>();
        _conveyorBlockGroups = World.GetStash<ConveyorBlockGroup>();
        _conveyorSlots = World.GetStash<BehaviourView<ConveyorSlot>>();

        _conveyorSystemConfig = _remoteConfigModule.GetDataClassNew<ConveyorSystemConfig>();
        _blockPhysicsConfig = _remoteConfigModule.GetDataClassNew<BlockPhysicsConfig>();
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        if (!_conveyorSystemConfig.MergeSystem) return;
        if (_blockPhysicsConfig.Type != BlockPhysicsConfig.PhysicsType.None) return;

        using var p1 = HashSetPool<int>.Get(out var mergingIds);
        using var p2 = HashSetPool<int>.Get(out var targetIds);
        using var p3 = HashSetPool<ConveyorSlot>.Get(out var mergingSlots);

        foreach (var link in _conveyorSlotLinks)
        {
            var current = link.Current;
            var next = link.Next;

            if (!_conveyorBlockGroups.Has(current)) continue;
            if (!_conveyorBlockGroups.Has(next)) continue;

            var currentGroup = _conveyorBlockGroups.Get(current);
            var nextGroup = _conveyorBlockGroups.Get(next);

            if (!current.IsCompatibleWith(next)) continue;
            if (mergingIds.Contains(currentGroup.GroupId)) continue;
            if (currentGroup.GroupId == nextGroup.GroupId) continue;
            if (targetIds.Contains(currentGroup.GroupId)) continue;

            mergingIds.Add(currentGroup.GroupId);
            targetIds.Add(nextGroup.GroupId);

            AddMergingGroup(currentGroup, mergingSlots);
        }

        foreach (var slot in mergingSlots)
        {
            _conveyor.SetSpeedOffset(slot, _conveyor.UnscaledSpeed / 2f);
        }

        foreach (var slot in _previousMergingSlots)
        {
            if (mergingSlots.Contains(slot)) continue;
            _conveyor.SetSpeedOffset(slot, 0f);
        }

        _previousMergingSlots.Clear();
        _previousMergingSlots.AddRange(mergingSlots);
    }

    private void AddMergingGroup(ConveyorBlockGroup mergingGroup, HashSet<ConveyorSlot> slots)
    {
        foreach (ConveyorSlot slot in _conveyorSlots)
        {
            if (!_conveyorBlockGroups.Has(slot)) continue;
            var group = _conveyorBlockGroups.Get(slot);
            if (group.GroupId != mergingGroup.GroupId) continue;
            slots.Add(slot);
        }
    }
}
using Dreamteck.Splines;
using Scellecs.Morpeh;
using UnityEngine.Pool;
using VContainer;

public sealed class ConveyorBlockGroupSystem : SystemBase
{
    [Inject] private SplineComputer _splineComputer;
    [Inject] private Conveyor _conveyor;

    private Stash<ConveyorSlotLink> _conveyorSlotLinks;
    private Stash<ConveyorBlockGroup> _conveyorBlockGroups;

    public override void OnAwake()
    {
        base.OnAwake();

        _conveyorBlockGroups = World.GetStash<ConveyorBlockGroup>();
        _conveyorSlotLinks = World.GetStash<ConveyorSlotLink>();
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        using var p = HashSetPool<ConveyorSlot>.Get(out var hashSet);

        var groupIdx = 0;
        var collisionDistance = _conveyor.SlotCollisionDistance + .15f;
        foreach (var link in _conveyorSlotLinks)
        {
            var slot = link.Current;
            if (hashSet.Contains(slot)) continue;
            _conveyorBlockGroups.Set(slot, new ConveyorBlockGroup { GroupId = -1 });
            if (slot.IsElementBeingAdded()) continue;

            hashSet.Add(slot);
            _conveyorBlockGroups.Set(slot, new ConveyorBlockGroup { GroupId = groupIdx });

            var next = link.Next;
            while (slot.IsCompatibleWith(next))
            {
                if (hashSet.Contains(next)) break;
                if (next.IsElementBeingAdded()) break;

                var nextLink = _conveyorSlotLinks.Get(next);
                var distance = nextLink.PreviousDistanceUnclamped;
                if (distance > collisionDistance) break;

                hashSet.Add(next);
                _conveyorBlockGroups.Set(next, new ConveyorBlockGroup { GroupId = groupIdx });
                next = nextLink.Next;
            }

            var previous = link.Previous;
            while (slot.IsCompatibleWith(previous))
            {
                if (hashSet.Contains(previous)) break;
                // if (previous.IsElementBeingAdded()) break;

                var previousLink = _conveyorSlotLinks.Get(previous);
                var distance = previousLink.NextDistanceUnclamped;
                if (distance > collisionDistance) break;

                hashSet.Add(previous);
                _conveyorBlockGroups.Set(previous, new ConveyorBlockGroup { GroupId = groupIdx });
                previous = previousLink.Previous;
            }

            groupIdx++;
        }
    }
}

public struct ConveyorBlockGroup : IComponent
{
    public int GroupId;
}
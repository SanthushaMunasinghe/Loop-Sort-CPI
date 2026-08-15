using Dreamteck.Splines;
using Scellecs.Morpeh;
using VContainer;

public sealed class ConveyorSlotCollisionSystem : SystemBase, IFixedSystem
{
    [Inject] private SplineComputer _splineComputer;
    [Inject] private ConveyorConfig _config;
    [Inject] private Conveyor _conveyor;

    private Filter _filter;
    private Stash<BehaviourView<ConveyorSlot>> _conveyorSlots;
    private Stash<ConveyorSlotLink> _conveyorSlotLinks;
    private Stash<CarrierTransferPoint> _carrierTransferPoints;

    public override void OnAwake()
    {
        base.OnAwake();

        _filter = World.Filter.With<BehaviourView<ConveyorSlot>>().With<ConveyorSlotLink>().Build();
        _conveyorSlots = World.GetStash<BehaviourView<ConveyorSlot>>();
        _conveyorSlotLinks = World.GetStash<ConveyorSlotLink>();
        _carrierTransferPoints = World.GetStash<CarrierTransferPoint>();
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        return;

        var occupiedSlotRatio01 = _conveyor.GetOccupiedSlotRatio01(withoutExtra: false);
        foreach (var entity in _filter)
        {
            ref var conveyorSlotView = ref _conveyorSlots.Get(entity);
            var conveyorSlot = conveyorSlotView.Behaviour;
            if (!conveyorSlot.HasElement()) continue;

            var current = conveyorSlot;
            ref var conveyorSlotLink = ref _conveyorSlotLinks.Get(entity);
            var next = conveyorSlotLink.Next;

            if (occupiedSlotRatio01 >= .98f)
            {
                current.SplineFollower.follow = true;
                continue;
            }

            if (next == null) continue;

            var currentPercent = current.SplineFollower.GetPercent();
            var nextPercent = next.SplineFollower.GetPercent();

            var collisionDistance = _conveyor.SlotCollisionDistance;
            var travel = _splineComputer.TravelUnclamped(currentPercent, collisionDistance);
            var collided = _splineComputer.IsInRange(nextPercent, currentPercent, travel);

            if (current.IsElementBeingAdded())
            {
                collided = true;
            }
            else
            {
                foreach (var transferPoint in _carrierTransferPoints)
                {
                    var distance = _splineComputer.DistanceTo(currentPercent, transferPoint.Percent);
                    if (distance > collisionDistance)
                    {
                        var f = collisionDistance * 2f;
                        var from = _splineComputer.TravelUnclamped(transferPoint.Percent, f, Spline.Direction.Backward);
                        if (_splineComputer.IsInRange(currentPercent, from, transferPoint.Percent))
                        {
                            collided = true;
                            break;
                        }
                    }
                    else
                    {
                        collided = false;
                        break;
                    }
                }
            }

            current.SplineFollower.follow = !collided;
        }
    }
}
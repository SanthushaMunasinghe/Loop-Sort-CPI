using Scellecs.Morpeh;

public sealed class ConveyorSlotActivateSystem : SystemBase, IFixedSystem
{
    private Stash<BehaviourView<ConveyorSlot>> _conveyorSlots;

    public override void OnAwake()
    {
        base.OnAwake();

        _conveyorSlots = World.GetStash<BehaviourView<ConveyorSlot>>();
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        return;

        foreach (ConveyorSlot conveyorSlot in _conveyorSlots)
        {
            var hasElement = conveyorSlot.HasElement();
            // conveyorSlot.SplineFollower.enabled = hasElement;
            conveyorSlot.SplineFollower.useTriggers = hasElement;
        }
    }
}
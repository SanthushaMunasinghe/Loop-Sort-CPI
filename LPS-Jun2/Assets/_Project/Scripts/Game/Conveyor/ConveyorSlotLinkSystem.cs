using Dreamteck.Splines;
using Scellecs.Morpeh;
using UnityEngine.Pool;
using VContainer;

public sealed class ConveyorSlotLinkSystem : SystemBase, IFixedSystem
{
    [Inject] private SplineComputer _splineComputer;
    [Inject] private ConveyorConfig _config;

    private Stash<BehaviourView<ConveyorSlot>> _conveyorSlots;
    private Stash<ConveyorSlotLink> _conveyorSlotLinks;

    public override void OnAwake()
    {
        base.OnAwake();

        _conveyorSlots = World.GetStash<BehaviourView<ConveyorSlot>>();
        _conveyorSlotLinks = World.GetStash<ConveyorSlotLink>();
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        return;

        using var p = ListPool<ConveyorSlot>.Get(out var occupied);
        foreach (ConveyorSlot conveyorSlot in _conveyorSlots)
        {
            if (!conveyorSlot.HasElement())
            {
                _conveyorSlotLinks.Remove(conveyorSlot);
                continue;
            }
            occupied.Add(conveyorSlot);
        }

        occupied.Sort((left, right)
            => left.SplineFollower.GetPercent().CompareTo(right.SplineFollower.GetPercent()));

        for (var i = 0; i < occupied.Count; i++)
        {
            var previous = i == 0 ? occupied[^1] : occupied[i - 1];
            var current = occupied[i];
            var next = (i + 1) >= occupied.Count ? occupied[0] : occupied[i + 1];

            var previousPercent = previous.SplineFollower.GetPercent();
            var currentPercent = current.SplineFollower.GetPercent();
            var nextPercent = next.SplineFollower.GetPercent();
            var previousDistanceUnclamped = _splineComputer.CalculateLengthUnclamped(previousPercent, currentPercent);
            var nextDistanceUnclamped = _splineComputer.CalculateLengthUnclamped(currentPercent, nextPercent);
            var previousDistance = _splineComputer.CalculateLength(previousPercent, currentPercent);
            var nextDistance = _splineComputer.CalculateLength(currentPercent, nextPercent);

            var conveyorSlotLink = new ConveyorSlotLink
            {
                Current = current,
                Next = next,
                Previous = previous,

                NextDistanceUnclamped = nextDistanceUnclamped,
                PreviousDistanceUnclamped = previousDistanceUnclamped,
                NextDistance = nextDistance,
                PreviousDistance = previousDistance,

                CurrentPercent = currentPercent,
                NextPercent = nextPercent,
                PreviousPercent = previousPercent,
            };
            _conveyorSlotLinks.Set(current, conveyorSlotLink);
        }
    }
}

public struct ConveyorSlotLink : IComponent
{
    public ConveyorSlot Current;
    public ConveyorSlot Next;
    public ConveyorSlot Previous;

    public float NextDistanceUnclamped;
    public float PreviousDistanceUnclamped;
    public float NextDistance;
    public float PreviousDistance;

    public double CurrentPercent;
    public double NextPercent;
    public double PreviousPercent;
}
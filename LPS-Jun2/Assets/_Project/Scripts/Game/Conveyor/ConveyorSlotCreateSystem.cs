using System.Collections.Generic;
using Dreamteck.Splines;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine;
using VContainer;

/// <summary>
/// Adopts the conveyor slots that LevelSandboxGenerator baked into the scene.
///
/// The slot count, spline percents, conveyor speed and collision distance are all computed at
/// generate time by <see cref="LevelGeometry.ComputeSlotLayout"/> and stored on LevelSandbox, so
/// this only has to register each slot's follower with the conveyor and apply those values.
/// </summary>
public sealed class ConveyorSlotCreateSystem : SystemBase
{
    [Inject] private Conveyor _conveyor;
    [Inject] private LevelSandbox _levelSandbox;

    [Inject] private ISubscriber<LevelBuildCompleteMessage> _levelBuildCompleteSub;

    private Stash<ExtraSlot> _extraSlots;

    private readonly List<SplineFollower> _slots = new();

    public int LengthBasedSlotCount { get; private set; }

    public override void OnAwake()
    {
        base.OnAwake();

        _extraSlots = World.GetStash<ExtraSlot>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _levelBuildCompleteSub.Subscribe(OnLevelBuildComplete).AddTo(bag);
    }

    private void OnLevelBuildComplete(LevelBuildCompleteMessage m)
    {
        AdoptSlots();
    }

    private void AdoptSlots()
    {
        var root = _levelSandbox != null ? _levelSandbox.SlotsRoot : null;
        if (root == null)
        {
            Debug.LogWarning($"[{nameof(ConveyorSlotCreateSystem)}] No slots root — the conveyor will be " +
                             "empty. Re-generate the level.");
            return;
        }

        _slots.Clear();
        root.GetComponentsInChildren(true, _slots);

        // Order matters: UpdateFollower copies Conveyor.Speed onto the follower at call time, and
        // slots are not in the _followers list that later speed changes iterate.
        _conveyor.SetSpeed(_levelSandbox.ConveyorSpeed);
        _conveyor.SetCollisionDistance(_levelSandbox.SlotCollisionDistance);

        foreach (var splineFollower in _slots)
            _conveyor.UpdateFollower(splineFollower);

        LengthBasedSlotCount = _levelSandbox.LengthBasedSlotCount;
    }
}

public struct SlotCreateCompleteMessage
{
}

public struct ExtraSlot : IComponent
{
}

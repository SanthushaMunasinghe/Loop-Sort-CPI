using Freya;
using Lean.Touch;
using Scellecs.Morpeh;
using UnityEngine;
using VContainer;

public sealed class ConveyorSpeedSystem : SystemBase
{
    [Inject] private Conveyor _conveyor;
    [Inject] private RemoteConfigModule _remoteConfigModule;

    private Filter _filter;
    private Stash<BehaviourView<Carrier>> _carriers;
    private PaceConfig _paceConfig;
    private BlockPhysicsConfig _blockPhysicsConfig;

    public override void OnAwake()
    {
        base.OnAwake();

        _filter = World.Filter.With<BehaviourView<Carrier>>().With<Enabled>().Build();
        _carriers = World.GetStash<BehaviourView<Carrier>>();
        _paceConfig = _remoteConfigModule.GetDataClassNew<PaceConfig>();
        _blockPhysicsConfig = _remoteConfigModule.GetDataClassNew<BlockPhysicsConfig>();
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        const float targetSpeedScale = 1.8f;

        var isLastCarrier = IsLastCarrier();
        var speedScale = 1f;

        var occupiedSlotCount = _conveyor.GetOccupiedSlotCount();
        var isFirstLevels = 2 > Prefs.Level && occupiedSlotCount > 0;

        // The GameReviveState slow-down is gone with the game state machine.
        if (_paceConfig.SlowerWhenConveyorEmpty && occupiedSlotCount == 0)
            speedScale = .2f;
        else if (_paceConfig.HoldToSpeed)
        {
            if (IsHoldInputActive())
                speedScale = _paceConfig.HoldSpeedMultiplier;
        }
        else if (isFirstLevels)
            speedScale = targetSpeedScale;
        else if (isLastCarrier)
            speedScale = targetSpeedScale;
        else if (_paceConfig.FasterWhenConveyorFull && _conveyor.AreAllSlotsOccupied())
            speedScale = targetSpeedScale;
        else if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.SandLoop ||
                 _blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.SandLoopLite)
        {
            var occupiedSlotRatio01 = _conveyor.GetOccupiedSlotRatio01();
            var speedScaleOffset = occupiedSlotRatio01.RemapClamped(.25f, 1f, occupiedSlotRatio01, occupiedSlotRatio01 * 4f);
            speedScale = 1f + speedScaleOffset;
        }
        else if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.NoTraffic)
        {
            var occupiedSlotRatio01 = _conveyor.GetOccupiedSlotRatio01();
            speedScale = 1f + occupiedSlotRatio01 * .5f;
        }

        if (Mathf.Approximately(speedScale, _conveyor.SpeedScale)) return;
        _conveyor.SetSpeedScale(speedScale);
    }

    public bool IsLastCarrier()
    {
        Carrier targetCarrier = null;
        foreach (var entity in _filter)
        {
            Carrier carrier = _carriers.Get(entity);
            if (carrier.IsComplete()) continue;
            if (carrier.IsEmpty() && !carrier.IsTransferringOrAddingBlocks()) continue;

            if (targetCarrier == null)
            {
                targetCarrier = carrier;
            }
            else
            {
                targetCarrier = null;
                break;
            }
        }

        var isLastCarrier = targetCarrier != null;
        return isLastCarrier;
    }

    public bool IsHoldInputActive()
    {
        if (!_paceConfig.HoldToSpeed) return false;
        var fingers = LeanTouch.GetFingers(true, true, 1);
        if (fingers == null || fingers.Count == 0) return false;
        var finger = fingers[0];
        return finger.IsActive && finger.Old;
    }
}
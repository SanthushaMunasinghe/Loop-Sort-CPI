using Freya;
using Lean.Touch;
using Scellecs.Morpeh;
using UnityEngine;
using VContainer;

public sealed class ConveyorSpeedSystem : SystemBase
{
    private const float DefaultMinSpeedScale = 0.2f;

    [Inject] private Conveyor _conveyor;
    [Inject] private ConveyorConfig _config;
    [Inject] private RemoteConfigModule _remoteConfigModule;
    [Inject] private SceneScope _sceneScope;

    private PaceConfig _paceConfig;
    private BlockPhysicsConfig _blockPhysicsConfig;

    public override void OnAwake()
    {
        base.OnAwake();

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
        var occupiedSlotRatio01 = _conveyor.GetOccupiedSlotRatio01();
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
            var speedScaleOffset = occupiedSlotRatio01.RemapClamped(.25f, 1f, occupiedSlotRatio01, occupiedSlotRatio01 * 4f);
            speedScale = 1f + speedScaleOffset;
        }
        else if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.NoTraffic)
        {
            speedScale = 1f + occupiedSlotRatio01 * .5f;
        }

        // Near-full warning slowdown: dampens whatever speed was already in effect above, rather than
        // replacing it, so the speed at the threshold itself is unchanged and eases down toward (but
        // never below) a minimum speed multiplier as occupancy climbs from the threshold to 100% full.
        var redRatio = _sceneScope.ConveyorRedRatioOverride > 0f ? _sceneScope.ConveyorRedRatioOverride : _config.RedRatio;
        if (occupiedSlotRatio01 >= redRatio)
        {
            var minSpeedScale = _sceneScope.ConveyorMinSpeedScaleOverride > 0f
                ? _sceneScope.ConveyorMinSpeedScaleOverride
                : DefaultMinSpeedScale;
            var remainingRatio = 1f - redRatio;
            var t = remainingRatio > 0f ? Mathf.Clamp01((occupiedSlotRatio01 - redRatio) / remainingRatio) : 1f;
            var nearFullMultiplier = Mathf.Lerp(1f, minSpeedScale, t);
            speedScale *= nearFullMultiplier;
        }

        if (Mathf.Approximately(speedScale, _conveyor.SpeedScale)) return;
        _conveyor.SetSpeedScale(speedScale);
    }

    public bool IsLastCarrier()
    {
        Carrier targetCarrier = null;
        foreach (var carrier in _sceneScope.AllCarriers)
        {
            if (!carrier.gameObject.activeInHierarchy) continue;
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
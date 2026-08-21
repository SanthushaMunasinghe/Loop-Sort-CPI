using System.Collections.Generic;
using Dreamteck.Splines;
using LitMotion;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine;
using VContainer;

public sealed class Conveyor : GameBehaviourBase
{
    [Inject] private ConveyorConfig _conveyorConfig;
    [Inject] private CarrierConfig _carrierConfig;
    [Inject] private MaterialPropertyBlock _propertyBlock;
    [Inject] private SplineComputer _splineComputer;
    [Inject] private RemoteConfigModule _remoteConfigModule;
    [Inject] private SceneScope _sceneScope;

    [Inject] private ISubscriber<BlockTransferStartMessage> _blockTransferStartSub;
    [Inject] private ISubscriber<BlockTransferCompleteMessage> _blockTransferCompleteSub;

    private RendererPropertyRegistry _propertyRegistry;

    private Filter _conveyorSlotFilter;
    private Filter _conveyorSlotExtraFilter;
    private Stash<BehaviourView<ConveyorSlot>> _conveyorSlots;
    private BlockPhysicsConfig _blockPhysicsConfig;

    private readonly HashSet<SplineFollower> _followers = new();
    private readonly Dictionary<SplineFollower, float> _speedBySlot = new();
    private readonly Dictionary<SplineFollower, float> _speedOffsetBySlot = new();
    private readonly Dictionary<SplineFollower, MotionHandle> _speedMotionBySlot = new();
    // private readonly HashSet<SplineFollower> _speedMotionBlockedFollowers = new();

    public float SlotCollisionDistance { get; private set; }
    public float UnscaledSpeed { get; private set; }
    public float Speed { get; private set; }
    public float SpeedScale { get; private set; } = 1f;

    public Vector3Int BlockSize { get; private set; }
    public int GroupBlockCount { get; private set; }
    public int GroupSlotCount { get; private set; }
    public int MaxBlockCount { get; private set; }
    public int SlotCount { get; private set; }
    public int SlotElementCount { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        _propertyRegistry = GetComponent<RendererPropertyRegistry>();

        _conveyorSlotFilter = World.Filter.With<BehaviourView<ConveyorSlot>>().Without<ExtraSlot>().Build();
        _conveyorSlotExtraFilter = World.Filter.With<BehaviourView<ConveyorSlot>>().Build();
        _conveyorSlots = World.GetStash<BehaviourView<ConveyorSlot>>();

        // transform.position = Vector3.down * _conveyorSettings.ConveyorHeight;

        _blockPhysicsConfig = _remoteConfigModule.GetDataClassNew<BlockPhysicsConfig>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _blockTransferStartSub.Subscribe(OnBlockTransferStart).AddTo(bag);
        _blockTransferCompleteSub.Subscribe(OnBlockTransferComplete).AddTo(bag);
    }

    public float GetOccupiedSlotRatio01(bool withoutExtra = true)
    {
        var filter = withoutExtra ? _conveyorSlotFilter : _conveyorSlotExtraFilter;
        if (filter.IsEmpty()) return 0f;

        var noSpaceCount = 0;
        foreach (var entity in filter)
        {
            ConveyorSlot conveyorSlot = _conveyorSlots.Get(entity);
            if (conveyorSlot.HasSpace()) continue;
            if (conveyorSlot.IsElementBeingAdded()) continue;
            noSpaceCount++;
        }

        return noSpaceCount / (float) filter.GetLengthSlow();
    }

    public int GetOccupiedSlotCount(bool withoutExtra = true)
    {
        var filter = withoutExtra ? _conveyorSlotFilter : _conveyorSlotExtraFilter;
        if (filter.IsEmpty()) return 0;

        var occupiedCount = 0;
        foreach (var entity in filter)
        {
            ConveyorSlot conveyorSlot = _conveyorSlots.Get(entity);
            if (!conveyorSlot.HasElement()) continue;
            occupiedCount++;
        }

        return occupiedCount;
    }

    public int GetTotalSlotCount(bool withoutExtra = true)
    {
        var filter = withoutExtra ? _conveyorSlotFilter : _conveyorSlotExtraFilter;
        return filter.GetLengthSlow();
    }

    public bool AreAllSlotsOccupied(bool withoutExtra = true)
    {
        return GetOccupiedSlotCount(withoutExtra) == GetTotalSlotCount(withoutExtra);
    }

    public void SetSideColor(Color color)
    {
        var targets = _propertyRegistry.GetTargets("Side Color");
        _propertyBlock.Clear();
        foreach (var target in targets)
        {
            _propertyBlock.SetColor(target.PropertyId, color);
            foreach (var targetRenderer in target.TargetRenderers)
                targetRenderer.SetPropertyBlock(_propertyBlock, target.MaterialIndex);
        }
    }

    public void SetCollisionDistance(float slotCollisionDistance)
    {
        SlotCollisionDistance = slotCollisionDistance;
    }

    public bool SetSpeed(float speed)
    {
        // if (_speedMotionBlockedFollowers.Count > 0) return false;
        UnscaledSpeed = speed;
        ApplySpeed(UnscaledSpeed * SpeedScale);
        return true;
    }

    public bool SetSpeedScale(float speedScale)
    {
        // if (_speedMotionBlockedFollowers.Count > 0) return false;
        SpeedScale = speedScale;
        ApplySpeed(UnscaledSpeed * SpeedScale);
        return true;
    }

    private void ApplySpeed(float speed)
    {
        Speed = speed;
        UpdateSpeed();
    }

    private void UpdateSpeed()
    {
        foreach (var follower in _followers)
        {
            _speedOffsetBySlot.TryGetValue(follower, out var offset);
            // if (_speedMotionBlockedFollowers.Contains(follower)) offset = 0;
            UpdateFollower(follower, Speed + offset);
        }
    }

    private void UpdateFollower(SplineFollower follower, float newSpeed)
    {
        // if (_speedBySlot.TryGetValue(follower, out var oldSpeed))
        //     if (Mathf.Approximately(oldSpeed, newSpeed))
        //         return;

        if (_speedMotionBySlot.TryGetValue(follower, out var existingMotion))
            existingMotion.TryCancel();

        var motion = LMotion.Create(follower.followSpeed, newSpeed, 1f)
            .BindToFollowerSpeed(follower)
            .AddTo(this);

        _speedBySlot[follower] = newSpeed;
        _speedMotionBySlot[follower] = motion;
    }

    public void SetSpeedOffset(ConveyorSlot slot, float newSpeedOffset)
    {
        _speedOffsetBySlot[slot.SplineFollower] = newSpeedOffset;
        UpdateSpeed();
    }

    public float GetSpeedOf(ConveyorSlot slot)
    {
        return Speed + _speedOffsetBySlot[slot.SplineFollower];
    }

    public float GetSpeedOffsetOf(ConveyorSlot slot)
    {
        var follower = slot.SplineFollower;
        // if (_speedMotionBlockedFollowers.Contains(follower)) return 0f;
        return _speedOffsetBySlot.GetValueOrDefault(follower);
    }

    public void AddFollower(SplineFollower splineFollower)
    {
        _followers.Add(splineFollower);
        UpdateFollower(splineFollower);
    }

    public void UpdateFollower(SplineFollower splineFollower)
    {
        splineFollower.followSpeed = Speed;
        splineFollower.spline = _splineComputer;
    }

    private void OnBlockTransferStart(BlockTransferStartMessage m)
    {
        // foreach (var block in m.Blocks)
        // {
        //     var conveyorSlot = block.Container as ConveyorSlot;
        //     if (conveyorSlot == null) continue;
        //     _speedMotionBlockedFollowers.Add(conveyorSlot.SplineFollower);
        // }
    }

    private void OnBlockTransferComplete(BlockTransferCompleteMessage m)
    {
        // foreach (var block in m.Blocks)
        // {
        //     var conveyorSlot = block.Container as ConveyorSlot;
        //     if (conveyorSlot == null) continue;
        //     _speedMotionBlockedFollowers.Remove(conveyorSlot.SplineFollower);
        // }
    }

    public void SetCarrierBlockArgs(CarrierBlockArgs args)
    {
        var carrierSize = _carrierConfig.Sizes[_blockPhysicsConfig.Type];
        var size = args.Size;
        SlotElementCount = _sceneScope.ConveyorSlotElementCountOverride > 0
            ? _sceneScope.ConveyorSlotElementCountOverride
            : carrierSize.SlotElementCount;
        BlockSize = size;
        GroupBlockCount = size.x * size.y * carrierSize.SingleColorMultiplier;
        GroupSlotCount = GroupBlockCount / SlotElementCount;
        MaxBlockCount = size.x * size.y * size.z;
        SlotCount = args.SlotCount;
    }
}
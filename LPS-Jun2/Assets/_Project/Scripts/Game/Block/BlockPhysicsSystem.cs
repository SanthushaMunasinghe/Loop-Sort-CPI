using Dreamteck.Splines;
using MessagePipe;
using Scellecs.Morpeh;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

public sealed class BlockPhysicsSystem : SystemBase, IFixedSystem
{
    [Inject] private BlockConfig _config;
    [Inject] private SplineComputer _splineComputer;
    [Inject] private Conveyor _conveyor;
    [Inject] private RemoteConfigModule _remoteConfigModule;

    [Inject] private ISubscriber<ConveyorSlotAddBlockMessage> _conveyorSlotAddBlockSub;
    [Inject] private ISubscriber<ConveyorSlotRemoveBlockMessage> _conveyorSlotRemoveBlockSub;
    [Inject] private ISubscriber<LevelBuildCompleteMessage> _levelBuildCompleteSub;

    private Filter _filter;
    private Stash<BehaviourView<Block>> _blocks;
    private BlockPhysicsConfig _blockPhysicsConfig;

    private Vector3[] _bakedPositions;
    private Vector3[] _bakedTargetPositions;
    private Dictionary<long, List<int>> _spatialHash;
    private float _cellSize;

    public override void OnAwake()
    {
        base.OnAwake();

        _filter = World.Filter.With<BehaviourView<Block>>().With<Enabled>().With<BlockPhysicsActive>().Build();
        _blocks = World.GetStash<BehaviourView<Block>>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        base.BuildMessages(bag);

        _blockPhysicsConfig = _remoteConfigModule.GetDataClassNew<BlockPhysicsConfig>();

        if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.None)
            return;

        if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.Free)
            Time.maximumDeltaTime = .018f;

        if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.SandLoop || _blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.SandLoopLite)
        {
            Time.fixedDeltaTime = .01f;
            Time.maximumDeltaTime = .04f;
        }

        _conveyorSlotAddBlockSub.Subscribe(OnConveyorSlotAddBlock).AddTo(bag);
        _conveyorSlotRemoveBlockSub.Subscribe(OnConveyorSlotRemoveBlock).AddTo(bag);
        _levelBuildCompleteSub.Subscribe(OnLevelBuildComplete).AddTo(bag);
    }

    private void OnConveyorSlotAddBlock(ConveyorSlotAddBlockMessage m)
    {
        var block = m.Block;
        var rigidbody = block.Rigidbody;
        rigidbody.mass = _config.Mass;
        rigidbody.linearDamping = _config.Drag;
        rigidbody.angularDamping = _config.AngularDrag;
        rigidbody.maxLinearVelocity = _config.MaxLinearVelocity;
        rigidbody.isKinematic = false;
        rigidbody.detectCollisions = true;
        block.Collider.enabled = true;

        if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.SandLoop || _blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.SandLoopLite)
        {
            rigidbody.useGravity = false;
            // rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            // rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            block.Collider.radius = .25f;
        }

        var blockT = block.transform;
        var currentPosition = blockT.position;
        var targetForcePosition = GetTargetForcePosition(currentPosition);
        var forceDirection = (targetForcePosition - currentPosition).normalized;
        rigidbody.AddForce(forceDirection * _config.InitialImpulseForce, ForceMode.Impulse);
    }

    private void OnConveyorSlotRemoveBlock(ConveyorSlotRemoveBlockMessage m)
    {
        var block = m.Block;
        block.Rigidbody.isKinematic = true;
    }

    private void OnLevelBuildComplete(LevelBuildCompleteMessage m)
    {
        BakeSpline(_config.BakeSampleCount);
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.None) return;

        foreach (var entity in _filter)
        {
            Block block = _blocks.Get(entity);
            if (block.Rigidbody.isKinematic) continue;

            var blockT = block.transform;
            var currentPosition = blockT.position;
            var targetForcePosition = GetTargetForcePosition(currentPosition);

            // Conveyor Outside Fix
            if (0f > currentPosition.y)
            {
                block.Rigidbody.position = _bakedTargetPositions.GetRandom();
                block.Rigidbody.linearVelocity = Vector3.zero;
                block.Rigidbody.angularVelocity = Vector3.zero;
                continue;
            }

            var speedMultiplier = _blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.SandLoop ||
                _blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.SandLoopLite
                    ? .38f
                    : 1f;

            var forceDirection = (targetForcePosition - currentPosition).normalized;
            var speed = _config.ConveyorForce * _conveyor.Speed * speedMultiplier;
            var force = forceDirection * speed;
            block.Rigidbody.AddForce(force);
        }
    }

    private void BakeSpline(int sampleCount)
    {
        _bakedPositions = new Vector3[sampleCount];
        _bakedTargetPositions = new Vector3[sampleCount];

        var sample = new SplineSample();
        for (var i = 0; i < sampleCount; i++)
        {
            var percent = (double)i / (sampleCount - 1);
            _splineComputer.Evaluate(percent, ref sample);
            _bakedPositions[i] = sample.position;

            var travelDistance = _config.ForceDirectionOffset;
            var targetPercent = _splineComputer.isClosed
                ? _splineComputer.TravelUnclamped(percent, travelDistance)
                : _splineComputer.Travel(percent, travelDistance);
            _splineComputer.Evaluate(targetPercent, ref sample);
            _bakedTargetPositions[i] = sample.position;
        }

        // var splineLength = _splineComputer.CalculateLength();
        // _cellSize = splineLength / sampleCount;
        _cellSize = _config.CellSize;

        _spatialHash = new Dictionary<long, List<int>>(sampleCount * 2);
        for (var i = 0; i < sampleCount; i++)
        {
            var key = HashPosition(_bakedPositions[i]);
            if (!_spatialHash.TryGetValue(key, out var list))
            {
                list = new List<int>(4);
                _spatialHash[key] = list;
            }
            list.Add(i);
        }
    }

    private long HashPosition(Vector3 position)
    {
        var x = Mathf.FloorToInt(position.x / _cellSize);
        var y = Mathf.FloorToInt(position.y / _cellSize);
        var z = Mathf.FloorToInt(position.z / _cellSize);

        // Pack three ints into a single long to avoid struct key overhead
        // Using 21-bit ranges: supports coordinates roughly ±1M cells
        return ((long)(x & 0x1FFFFF) << 42)
               | ((long)(y & 0x1FFFFF) << 21)
               | (long)(z & 0x1FFFFF);
    }

    private Vector3 GetTargetForcePosition(Vector3 position)
    {
        var cx = Mathf.FloorToInt(position.x / _cellSize);
        var cy = Mathf.FloorToInt(position.y / _cellSize);
        var cz = Mathf.FloorToInt(position.z / _cellSize);

        var bestSqrDist = float.MaxValue;
        var bestIndex = -1;

        var maxSearchRadius = _config.MaxSearchRadius;
        var radius = 1;
        while (bestIndex < 0  && radius <= maxSearchRadius)
        {
            for (var dx = -radius; dx <= radius; dx++)
            for (var dy = -radius; dy <= radius; dy++)
            for (var dz = -radius; dz <= radius; dz++)
            {
                // Skip inner cells already checked in previous iteration
                if (radius > 1
                    && Mathf.Abs(dx) < radius
                    && Mathf.Abs(dy) < radius
                    && Mathf.Abs(dz) < radius)
                    continue;

                var nx = cx + dx;
                var ny = cy + dy;
                var nz = cz + dz;

                var key = ((long)(nx & 0x1FFFFF) << 42)
                          | ((long)(ny & 0x1FFFFF) << 21)
                          | (long)(nz & 0x1FFFFF);

                if (!_spatialHash.TryGetValue(key, out var list)) continue;

                for (var i = 0; i < list.Count; i++)
                {
                    var idx = list[i];
                    var sqrDist = (position - _bakedPositions[idx]).sqrMagnitude;
                    if (sqrDist < bestSqrDist)
                    {
                        bestSqrDist = sqrDist;
                        bestIndex = idx;
                    }
                }
            }

            radius++;
        }

        return bestIndex >= 0 ? _bakedTargetPositions[bestIndex] : position + Vector3.down;
    }
}

public struct BlockPhysicsActive : IComponent
{
}
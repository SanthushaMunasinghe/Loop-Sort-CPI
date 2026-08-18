using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Dreamteck.Splines;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public sealed class BlockTransferSystem : SystemBase
{
    [Inject] private SplineComputer _splineComputer;
    [Inject] private ConveyorConfig _conveyorConfig;
    [Inject] private AudioModule _audioModule;
    [Inject] private Conveyor _conveyor;
    [Inject] private RemoteConfigModule _remoteConfigModule;
    [Inject] private SceneScope _sceneScope;

    [Inject] private ISubscriber<BlockTransferMessage> _blockTransferSub;
    [Inject] private ISubscriber<LevelBuildCompleteMessage> _levelBuildCompleteSub;
    [Inject] private IPublisher<BlockTransferCompleteMessage> _blockTransferCompletePub;
    [Inject] private IPublisher<BlockTransferStartMessage> _blockTransferStartPub;
    [Inject] private IPublisher<CarrierTriggerCompleteMessage> _carrierTriggerCompletePub;
    [Inject] private IPublisher<CarrierTriggerStartMessage> _carrierTriggerStartPub;
    [Inject] private IPublisher<BlockCarrierMeshUpdateMessage> _blockCarrierMeshUpdatePub;

    private Filter _conveyorSlotFilter;
    private Filter _conveyorSlotExtraFilter;
    private Stash<BehaviourView<ConveyorSlot>> _conveyorSlots;
    private Stash<CarrierTransferPoint> _carrierTransferPoints;
    private Stash<BlockLastCarrier> _blockLastCarriers;
    private Stash<BlockTransferCarrier> _blockTransferCarriers;

    private PaceConfig _paceConfig;
    private BlockPhysicsConfig _blockPhysicsConfig;
    private BlockTransferConfig _blockTransferConfig;
    private ConveyorSystemConfig _conveyorSystemConfig;
    private SoundConfig _soundConfig;

    private readonly Dictionary<Carrier, Carrier> _removeTransferBlockTarget = new();

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _blockTransferSub.Subscribe(OnBlockTransfer).AddTo(bag);
        _levelBuildCompleteSub.Subscribe(OnLevelBuildComplete).AddTo(bag);
    }

    public override void OnAwake()
    {
        base.OnAwake();

        _conveyorSlotFilter = World.Filter.With<BehaviourView<ConveyorSlot>>().Without<ExtraSlot>().Build();
        _conveyorSlotExtraFilter = World.Filter.With<BehaviourView<ConveyorSlot>>().Build();
        _conveyorSlots = World.GetStash<BehaviourView<ConveyorSlot>>();
        _carrierTransferPoints = World.GetStash<CarrierTransferPoint>();
        _blockLastCarriers = World.GetStash<BlockLastCarrier>();
        _blockTransferCarriers = World.GetStash<BlockTransferCarrier>();

        _paceConfig = _remoteConfigModule.GetDataClassNew<PaceConfig>();
        _blockTransferConfig = _remoteConfigModule.GetDataClassNew<BlockTransferConfig>();
        _conveyorSystemConfig = _remoteConfigModule.GetDataClassNew<ConveyorSystemConfig>();
        _soundConfig = _remoteConfigModule.GetDataClassNew<SoundConfig>();
    }

    private void OnLevelBuildComplete(LevelBuildCompleteMessage m)
    {
        SetRemoveTransferBlockTargets();

        _blockPhysicsConfig = _remoteConfigModule.GetDataClassNew<BlockPhysicsConfig>();
        if (_blockPhysicsConfig.Type != BlockPhysicsConfig.PhysicsType.None) return;
        AddCarrierTriggers();
    }

    private void AddCarrierTriggers()
    {
        var triggerGroup = _splineComputer.AddTriggerGroup();
        foreach (var carrier in _sceneScope.EmptyCarriers)
        {
            var worldPosition = carrier.TransferProjectPoint.position;
            var project = _splineComputer.Project(worldPosition);
            var firstTrigger = triggerGroup.AddTrigger(project.percent, SplineTrigger.Type.Forward);
            firstTrigger.AddListener(splineUser => HandleCarrierTrigger(splineUser, carrier));
            var travel = _splineComputer.TravelUnclamped(project.percent, _conveyorConfig.CarrierTriggerOffset, Spline.Direction.Backward);
            var travelTrigger = triggerGroup.AddTrigger(travel, SplineTrigger.Type.Forward);
            travelTrigger.AddListener(splineUser => HandleCarrierTrigger(splineUser, carrier));
        }
    }

    private void SetRemoveTransferBlockTargets()
    {
        using var p1 = DictionaryPool<Carrier, SplineSample>.Get(out var splineSamples);
        foreach (var carrier in _sceneScope.AllCarriers)
        {
            var worldPosition = carrier.TransferProjectPoint.position;
            var project = _splineComputer.Project(worldPosition);
            splineSamples[carrier] = project;
        }

        foreach (var carrier in _sceneScope.AllCarriers)
        {
            SplineSample? targetSample = null;
            Carrier targetCarrier = null;

            var carrierSample = splineSamples[carrier];
            var carrierPercent = _splineComputer.TravelUnclamped(carrierSample.percent, 1.5f);
            foreach (var (c, s) in splineSamples)
            {
                if (carrier == c) continue;
                if (carrierPercent > s.percent) continue;
                if (targetSample != null)
                {
                    var sample = targetSample.Value;
                    if (s.percent >= sample.percent) continue;
                }
                targetSample = s;
                targetCarrier = c;
            }

            if (targetSample == null)
            {
                foreach (var (c, s) in splineSamples)
                {
                    if (carrier == c) continue;
                    if (targetSample != null)
                    {
                        var sample = targetSample.Value;
                        if (s.percent >= sample.percent) continue;
                    }
                    if (Math.Abs(s.percent - carrierSample.percent) < .01f) continue;
                    targetSample = s;
                    targetCarrier = c;
                }
            }

            _removeTransferBlockTarget[carrier] = targetCarrier;
        }
    }

    private ConveyorSlot FindEmptySlot()
    {
        foreach (ConveyorSlot conveyorSlot in _conveyorSlots)
        {
            if (!conveyorSlot.HasSpace()) continue;
            return conveyorSlot;
        }

        return null;
    }

    private int FindEmptySlotCount(bool withoutExtra = true)
    {
        var filter = withoutExtra ? _conveyorSlotFilter : _conveyorSlotExtraFilter;
        var count = 0;
        foreach (var entity in filter)
        {
            ConveyorSlot conveyorSlot = _conveyorSlots.Get(entity);
            if (!conveyorSlot.HasSpace()) continue;
            count++;
        }

        return count;
    }

    private double ProjectEmptySlotPercent(Carrier carrier)
    {
        var worldPosition = carrier.TransferProjectPoint.position;
        var project = _splineComputer.Project(worldPosition);
        var percent = project.percent;
        return percent;
    }

    private int FindClosestSlotCount(double start, float distance)
    {
        var halfDistance = distance / 2f;
        var forwardTravel = _splineComputer.TravelUnclamped(start, halfDistance);
        var backwardTravel = _splineComputer.TravelUnclamped(start, halfDistance, Spline.Direction.Backward);

        var slotCount = 0;
        foreach (ConveyorSlot conveyorSlot in _conveyorSlots)
        {
            if (!conveyorSlot.HasElement()) continue;

            var slotPercent = conveyorSlot.SplineFollower.GetPercent();
            if (_splineComputer.IsInRange(slotPercent, backwardTravel, forwardTravel))
            {
                slotCount++;
            }
        }

        return slotCount;
    }

    private void OnBlockTransfer(BlockTransferMessage m)
    {
        TransferBlocksToEmptySlots(m.Carrier);
    }

    private async UniTaskVoid TransferBlocksToEmptySlots(Carrier carrier)
    {
        if (!carrier.CanBeginTransfer()) return;
        if (carrier.IsTransferringOrAddingBlocks()) return;

        var blocks = new List<Block>();
        using var p = ListPool<UniTask>.Get(out var tasks);

        carrier.FindNextTransferBlocks(blocks);
        TrimBlocks(blocks);
        if (blocks.Count == 0) return;

        if (_soundConfig.Stack)
        {
            _audioModule.GetPlayer()
                .WithVolumeScale(.65f)
                .Play(_audioModule.Sounds.CarrierTransferAb);
        }
        else
        {
            _audioModule.GetPlayer().Play(_audioModule.Sounds.CarrierTransfer);
        }

        var percent = ProjectEmptySlotPercent(carrier);
        var emptySlot = FindEmptySlot();
        var slotCollisionDistance = _conveyor.SlotCollisionDistance;

        if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.None)
        {
            var closestSlotDepth = _conveyorConfig.ClosestSlotDepth;
            var closestSlotCount = FindClosestSlotCount(percent, slotCollisionDistance * closestSlotDepth);
            if (closestSlotCount > closestSlotDepth) return;
        }

        _carrierTransferPoints.Set(carrier, new CarrierTransferPoint
        {
            Percent = percent,
        });

        _blockLastCarriers.RemoveAll();

        var slotSpeed = _conveyor.UnscaledSpeed;
        var slotElementCount = _conveyor.SlotElementCount;
        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];

            if (emptySlot == null) break;
            if (!emptySlot.HasSpace())
            {
                emptySlot = FindEmptySlot();
                percent = _splineComputer.TravelUnclamped(percent, .01f, Spline.Direction.Backward);
            }
            if (emptySlot == null) break;

            emptySlot.SplineFollower.SetPercent(percent);
            var delay = slotCollisionDistance / slotSpeed / slotElementCount * i;
            delay *= _paceConfig.BlockAnimationDurationMultiplier;
            var task = emptySlot.AddBlock(block, delay);
            tasks.Add(task);

            _blockLastCarriers.Set(block, new BlockLastCarrier { Carrier = carrier });
            _blockTransferCarriers.Set(block, new BlockTransferCarrier { Carrier = carrier });
        }

        _blockTransferStartPub.Publish(new BlockTransferStartMessage
        {
            Carrier = carrier,
            Blocks = blocks,
        });

        carrier.BeginTransfer();
        await UniTask.WhenAll(tasks).AttachExternalCancellation(carrier.SceneLoadToken);
        await UniTask.Delay(TimeSpan.FromMilliseconds(100), delayTiming: PlayerLoopTiming.FixedUpdate, cancellationToken: carrier.SceneLoadToken);
        carrier.EndTransfer();

        _blockTransferCompletePub.Publish(new BlockTransferCompleteMessage
        {
            Carrier = carrier,
            Blocks = blocks,
        });

        _blockCarrierMeshUpdatePub.Publish(new BlockCarrierMeshUpdateMessage
        {
            Carrier = carrier
        });

        _carrierTransferPoints.Remove(carrier);
    }

    private int GetConveyorBlockCount(bool withoutExtra = false)
    {
        var filter = withoutExtra ? _conveyorSlotFilter : _conveyorSlotExtraFilter;
        var count = 0;
        foreach (var entity in filter)
        {
            ConveyorSlot conveyorSlot = _conveyorSlots.Get(entity);
            count += conveyorSlot.GetElements().Count;
        }

        return count;
    }

    private void TrimBlocks(List<Block> blocks)
    {
        var totalSlotCount = _conveyor.GetTotalSlotCount();
        var blockCount = blocks.Count;

        if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.None)
        {
            var occupiedSlotCount = _conveyor.GetOccupiedSlotCount(withoutExtra: false);
            if (occupiedSlotCount >= totalSlotCount)
            {
                blocks.Clear();
                return;
            }

            var emptySlotCount = FindEmptySlotCount(withoutExtra: false);
            var requiredSlotCount = blockCount / _conveyor.SlotElementCount;
            if (emptySlotCount >= requiredSlotCount) return;

            var minSlotCount = _conveyor.GroupSlotCount;
            var extraSlotCount = requiredSlotCount - emptySlotCount;
            extraSlotCount += emptySlotCount % minSlotCount;
            var removeBlockCount = extraSlotCount * _conveyor.SlotElementCount;
            blocks.RemoveRange(blockCount - removeBlockCount, removeBlockCount);
        }
        else
        {
            var conveyorBlockCount = GetConveyorBlockCount(withoutExtra: false);
            var maxBlockCount = totalSlotCount * _conveyor.SlotElementCount;
            if (_conveyorSystemConfig.LoanSystem)
            {
                if (conveyorBlockCount >= maxBlockCount)
                    blocks.Clear();
                else if (conveyorBlockCount + blockCount > maxBlockCount)
                {
                    var firstBlock = blocks[0];
                    int? removeBlockCount = null;
                    foreach (var carrier in _sceneScope.AllCarriers)
                    {
                        if (carrier.IsFull() || carrier.IsEmpty()) continue;
                        var nextColorType = carrier.GetNextColorType();
                        if (nextColorType != firstBlock.ColorType) continue;
                        if ((Carrier)firstBlock.Container == carrier) continue;

                        var spaceCount = carrier.GetMaxBlockCount() - carrier.GetBlocks().Count;
                        var removeCount = Mathf.Max(0, blockCount - spaceCount);
                        if (removeCount > removeBlockCount) continue;
                        removeBlockCount = removeCount;
                    }

                    var remove = removeBlockCount.GetValueOrDefault(0);
                    blocks.RemoveRange(blockCount - remove, remove);
                }
            }
            else if (conveyorBlockCount + blockCount > maxBlockCount)
            {
                var emptySlotCount = (maxBlockCount - conveyorBlockCount) / _conveyor.SlotElementCount;
                var requiredSlotCount = blockCount / _conveyor.SlotElementCount;
                var minSlotCount = _conveyor.GroupSlotCount;
                var extraSlotCount = requiredSlotCount - emptySlotCount;
                extraSlotCount += emptySlotCount % minSlotCount;
                var removeBlockCount = extraSlotCount * _conveyor.SlotElementCount;
                blocks.RemoveRange(blockCount - removeBlockCount, removeBlockCount);
            }
        }
    }

    private void HandleCarrierTrigger(SplineUser splineUser, Carrier carrier)
    {
        // The playing/tutorial state gate is gone with the game state machine — the sandbox is
        // always "playing".
        if (!carrier.gameObject.activeInHierarchy)
            return;

        var conveyorSlot = splineUser.GetComponent<ConveyorSlot>();
        if (conveyorSlot.IsElementBeingAdded())
            return;

        var elements = conveyorSlot.GetElements();
        var elementCount = elements.Count;
        if (elementCount == 0)
            return;

        HandleCarrierTrigger(carrier, elements);
    }

    public async UniTaskVoid HandleCarrierTrigger(Carrier carrier, List<Block> blocks)
    {
        // Only SceneScope's curated Start/Empty carriers actually function — everything else is
        // inert even though it still has its own physics trigger (level generation is unchanged).
        if (!_sceneScope.IsRegisteredCarrier(carrier)) return;

        // Same here — no state machine, so conveyor -> carrier pickup is always allowed.
        foreach (var block in blocks)
        {
            if (!_blockTransferCarriers.Has(block)) continue;
            var blockTransferCarrier = _blockTransferCarriers.Get(block);
            var targetCarrier = _removeTransferBlockTarget[blockTransferCarrier.Carrier];
            if (carrier == targetCarrier) _blockTransferCarriers.Remove(block);
        }

        if (!carrier.gameObject.activeInHierarchy)
            return;

        using var p = ListPool<UniTask>.Get(out var tasks);

        var addBlock = false;
        var blockCount = blocks.Count;
        for (var i = blockCount - 1; i >= 0; i--)
        {
            var block = blocks[i];

            if (_blockTransferCarriers.Has(block))
            {
                var blockTransferCarrier = _blockTransferCarriers.Get(block);
                if (blockTransferCarrier.Carrier == carrier) continue;
            }

            if (carrier.IsFull()) break;
            if (!carrier.CanBeginTransfer()) break;

            // A sink decides for itself what it will have — CanTransferBlock below — so neither the
            // colour match nor the better-carrier scan gets a say. IsFull above still caps it.
            if (!carrier.IsSink())
            {
                if (!block.IsCompatibleWith(carrier)) continue;
                if (HasBetterCarrier(carrier, block)) continue;
            }

            if (!carrier.CanTransferBlock(block)) continue;

            var isTransferring = carrier.IsTransferring();
            if (isTransferring)
            {
                return;
                // var splineFollower = (SplineFollower)splineUser;
                // splineFollower.enabled = false;
                // await UniTask.WaitWhile(carrier.IsTransferring, cancellationToken: carrier.SceneLoadToken);
                // splineFollower.enabled = true;
            }

            var collisionDelay = _conveyor.SlotCollisionDistance / _conveyor.Speed;
            var delay = (blockCount - i - 1) * collisionDelay / 2f;

            var task = carrier.AddBlock(block, delay);
            tasks.Add(task);

            addBlock = true;
        }
        if (!addBlock) return;

        _carrierTriggerStartPub.Publish(new CarrierTriggerStartMessage
        {
            Carrier = carrier
        });

        await UniTask.WhenAll(tasks).AttachExternalCancellation(carrier.SceneLoadToken);

        _carrierTriggerCompletePub.Publish(new CarrierTriggerCompleteMessage
        {
            Carrier = carrier
        });
    }

    private bool HasBetterCarrier(Carrier referenceCarrier, Block block)
    {
        var betterCarrier = default(Carrier);
        var betterCarrierBlockCount = 0;
        foreach (var carrier in _sceneScope.AllCarriers)
        {
            // A sink acts on its own trigger only. Letting it score as someone else's better carrier
            // would hold blocks back from carriers that can actually use them.
            if (carrier.IsSink()) continue;
            if (!carrier.CanTransferBlock(block)) continue;

            if (carrier.IsBetterCarrier(block))
            {
                betterCarrier = carrier;
                break;
            }

            if (_blockTransferConfig.FirstAvailable) continue;
            if (carrier.IsFull()) continue;
            if (!carrier.CanBeginTransfer()) continue;
            if (!carrier.AreAllBlocksSameColor()) continue;
            var nextColorType = carrier.GetNextColorType();
            if (block.ColorType != nextColorType) continue;

            var blockCount = carrier.GetBlocks().Count;
            if (betterCarrierBlockCount > blockCount) continue;
            if (betterCarrierBlockCount == blockCount && referenceCarrier != carrier) continue;

            betterCarrier = carrier;
            betterCarrierBlockCount = blockCount;
        }

        return betterCarrier != null && betterCarrier != referenceCarrier;
    }
}

// Messages

public struct BlockTransferStartMessage
{
    public Carrier Carrier;
    public List<Block> Blocks;
}

public struct BlockTransferCompleteMessage
{
    public Carrier Carrier;
    public List<Block> Blocks;
}

public struct BlockCarrierMeshUpdateMessage
{
    public Carrier Carrier;
}

public struct CarrierTriggerStartMessage
{
    public Carrier Carrier;
}

public struct CarrierTriggerCompleteMessage
{
    public Carrier Carrier;
}

// Components

public struct CarrierTransferPoint : IComponent
{
    public double Percent;
}

public struct BlockLastCarrier : IComponent
{
    public Carrier Carrier;
}

public struct BlockTransferCarrier : IComponent
{
    public Carrier Carrier;
}
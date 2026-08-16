using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

/// <summary>
/// Binds the pre-generated carrier triggers to the transfer system.
///
/// The trigger GameObjects themselves are produced by LevelSandboxGenerator and live in the scene,
/// so this no longer creates anything — it only wires each BlockTrigger to the carrier it was
/// generated for.
/// </summary>
public sealed class BlockTriggerSystem : SystemBase, IFixedSystem
{
    [Inject] private BlockTransferSystem _blockTransferSystem;
    [Inject] private RemoteConfigModule _remoteConfigModule;
    [Inject] private LevelSandbox _levelSandbox;

    [Inject] private IPublisher<CarrierTriggerCompleteMessage> _carrierTriggerCompletePub;
    [Inject] private IPublisher<CarrierTriggerStartMessage> _carrierTriggerStartPub;
    [Inject] private ISubscriber<LevelBuildCompleteMessage> _levelBuildCompleteSub;

    private Stash<BehaviourView<Carrier>> _carriers;
    private BlockPhysicsConfig _blockPhysicsConfig;

    public override void OnAwake()
    {
        base.OnAwake();

        _carriers = World.GetStash<BehaviourView<Carrier>>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        base.BuildMessages(bag);

        _blockPhysicsConfig = _remoteConfigModule.GetDataClassNew<BlockPhysicsConfig>();
        if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.None) return;
        _levelBuildCompleteSub.Subscribe(OnLevelBuildComplete).AddTo(bag);
    }

    private void OnLevelBuildComplete(LevelBuildCompleteMessage obj)
    {
        BindCarrierTriggers();
    }

    private void BindCarrierTriggers()
    {
        var root = _levelSandbox != null ? _levelSandbox.TriggersRoot : null;
        if (root == null)
        {
            Debug.LogWarning($"[{nameof(BlockTriggerSystem)}] No triggers root — conveyor to carrier " +
                             "pickup will not work. Re-generate the level.");
            return;
        }

        var triggers = root.GetComponentsInChildren<CarrierBlockTrigger>(true);
        foreach (var carrierTrigger in triggers)
        {
            var carrier = carrierTrigger.Carrier;
            if (carrier == null)
            {
                Debug.LogWarning($"[{nameof(BlockTriggerSystem)}] Trigger '{carrierTrigger.name}' has no " +
                                 "Carrier assigned.", carrierTrigger);
                continue;
            }

            if (!carrierTrigger.TryGetComponent<BlockTrigger>(out var blockTrigger)) continue;

            blockTrigger.AddListener(block =>
            {
                var conveyorSlot = block.Container as ConveyorSlot;
                if (conveyorSlot == null) return;
                using var p = ListPool<Block>.Get(out var blocks);
                blocks.Add(block);
                _blockTransferSystem.HandleCarrierTrigger(carrier, blocks);
            });
        }
    }
}

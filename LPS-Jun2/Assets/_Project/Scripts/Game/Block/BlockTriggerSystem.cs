using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

/// <summary>
/// Binds every trigger that feeds blocks into carriers to the transfer system: the pre-generated
/// per-carrier triggers, and the one hand-placed GlobalTrigger.
///
/// The trigger GameObjects themselves already live in the scene — the per-carrier ones produced by
/// LevelSandboxGenerator, GlobalTrigger placed by hand — so this creates nothing, it only wires each
/// BlockTrigger to whatever should handle the blocks that enter it.
/// </summary>
public sealed class BlockTriggerSystem : SystemBase, IFixedSystem
{
    [Inject] private BlockTransferSystem _blockTransferSystem;
    [Inject] private RemoteConfigModule _remoteConfigModule;
    [Inject] private LevelSandbox _levelSandbox;
    [Inject] private SceneScope _sceneScope;

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
        BindGlobalTrigger();
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

    /// <summary>
    /// Binds the one hand-placed GlobalTrigger (SceneScope.GlobalTrigger) — unlike a per-carrier
    /// trigger it isn't tied to a specific carrier, so on each block it asks SceneScope to find a
    /// compatible Empty carrier and routes the block there.
    /// </summary>
    private void BindGlobalTrigger()
    {
        var globalTrigger = _sceneScope.GlobalTrigger;
        if (globalTrigger == null)
        {
            Debug.LogWarning($"[{nameof(BlockTriggerSystem)}] No Global Trigger assigned on " +
                              "SceneScope — global carrier pickup will not work.");
            return;
        }

        if (!globalTrigger.TryGetComponent<BlockTrigger>(out var blockTrigger)) return;

        blockTrigger.AddListener(block =>
        {
            var conveyorSlot = block.Container as ConveyorSlot;
            if (conveyorSlot == null) return;

            var carrier = _sceneScope.FindCompatibleEmptyCarrier(block);
            if (carrier == null) return;

            using var p = ListPool<Block>.Get(out var blocks);
            blocks.Add(block);
            _blockTransferSystem.HandleCarrierTrigger(carrier, blocks);
        });
    }
}

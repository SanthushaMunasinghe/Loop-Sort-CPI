using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

/// <summary>
/// Binds every trigger that feeds blocks into carriers to the transfer system: the per-carrier
/// triggers, and the one hand-placed GlobalTrigger.
///
/// The trigger GameObjects themselves already live in the scene — the per-carrier ones produced by
/// LevelSandboxGenerator or placed by hand, GlobalTrigger placed by hand — so this creates nothing,
/// it only wires each BlockTrigger to whatever should handle the blocks that enter it.
/// </summary>
public sealed class BlockTriggerSystem : SystemBase, IFixedSystem
{
    [Inject] private BlockTransferSystem _blockTransferSystem;
    [Inject] private RemoteConfigModule _remoteConfigModule;
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
        BindGroceryTriggers();
        BindGlobalTrigger();
    }

    /// <summary>
    /// Every CarrierBlockTrigger in the scene, wherever it sits — not just the ones the generator put
    /// under LevelSandbox's Triggers Root, so a trigger placed or re-parented by hand (which is how a
    /// Default carrier gets fed) is bound the same as a generated one. Triggers belonging to carriers
    /// this scene isn't running are bound too and cost nothing: HandleCarrierTrigger drops them on its
    /// own IsRegisteredCarrier check.
    /// </summary>
    private void BindCarrierTriggers()
    {
        var triggers = Object.FindObjectsByType<CarrierBlockTrigger>(FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (triggers.Length == 0)
        {
            // Carts are fed by GroceryTriggers instead, so no CarrierBlockTrigger is expected there.
            if (_sceneScope.UseShoppingCarts) return;

            Debug.LogWarning($"[{nameof(BlockTriggerSystem)}] No CarrierBlockTrigger in the scene — " +
                             "conveyor to carrier pickup will not work. Re-generate the level, or place " +
                             "the triggers by hand.");
            return;
        }

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
    /// Every GroceryTrigger in the scene, each feeding the shopping cart it names. Only Grocery Items
    /// are picked up; anything else riding the belt passes through. From there it is exactly a Default
    /// carrier's pickup — HandleCarrierTrigger's registered-carrier gate, type match, better-carrier
    /// scan and no-straight-back rule.
    /// </summary>
    private void BindGroceryTriggers()
    {
        var triggers = Object.FindObjectsByType<GroceryTrigger>(FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (triggers.Length == 0)
        {
            if (_sceneScope.UseShoppingCarts)
                Debug.LogWarning($"[{nameof(BlockTriggerSystem)}] Use Shopping Carts is on but there is no " +
                                 "GroceryTrigger in the scene — conveyor to cart pickup will not work.");
            return;
        }

        foreach (var groceryTrigger in triggers)
        {
            var cart = groceryTrigger.Cart;
            if (cart == null)
            {
                Debug.LogWarning($"[{nameof(BlockTriggerSystem)}] Grocery trigger '{groceryTrigger.name}' " +
                                 "has no Cart assigned.", groceryTrigger);
                continue;
            }

            if (!groceryTrigger.TryGetComponent<BlockTrigger>(out var blockTrigger)) continue;

            blockTrigger.AddListener(block =>
            {
                if (!block.TryGetComponent<GroceryItem>(out _)) return;
                var conveyorSlot = block.Container as ConveyorSlot;
                if (conveyorSlot == null) return;
                using var p = ListPool<Block>.Get(out var blocks);
                blocks.Add(block);
                _blockTransferSystem.HandleCarrierTrigger(cart, blocks);
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
        // Default carriers each take blocks in through their own trigger, so the global one has
        // nothing to do — and FindCompatibleEmptyCarrier would return null on every block anyway.
        // Shopping carts are fed the same way.
        if (_sceneScope.UsesSelfFedCarriers) return;

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

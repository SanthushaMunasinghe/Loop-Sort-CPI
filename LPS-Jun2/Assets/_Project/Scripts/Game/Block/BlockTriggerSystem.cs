using Dreamteck.Splines;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public sealed class BlockTriggerSystem : SystemBase, IFixedSystem
{
    [Inject] private SplineComputer _splineComputer;
    [Inject] private GameMachine _gameMachine;
    [Inject] private ConveyorConfig _conveyorConfig;
    [Inject] private BlockTransferSystem _blockTransferSystem;
    [Inject] private RemoteConfigModule _remoteConfigModule;

    [Inject] private IPublisher<CarrierTriggerCompleteMessage> _carrierTriggerCompletePub;
    [Inject] private IPublisher<CarrierTriggerStartMessage> _carrierTriggerStartPub;
    [Inject] private ISubscriber<LevelBuildCompleteMessage> _levelBuildCompleteSub;

    private Stash<BehaviourView<Carrier>> _carriers;
    private BlockPhysicsConfig _blockPhysicsConfig;

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        base.BuildMessages(bag);

        _blockPhysicsConfig = _remoteConfigModule.GetDataClassNew<BlockPhysicsConfig>();
        if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.None) return;
        _levelBuildCompleteSub.Subscribe(OnLevelBuildComplete).AddTo(bag);
    }

    private void OnLevelBuildComplete(LevelBuildCompleteMessage obj)
    {
        AddCarrierTriggers();
    }

    private void AddCarrierTriggers()
    {
        foreach (Carrier carrier in _carriers)
        {
            var worldPosition = carrier.TransferProjectPoint.position;
            var project = _splineComputer.Project(worldPosition);
            var travel = _splineComputer.TravelUnclamped(project.percent, _conveyorConfig.CarrierTriggerOffset, Spline.Direction.Backward);
            var collider = new GameObject("Trigger", typeof(BoxCollider), typeof(BlockTrigger));
            collider.GetComponent<BoxCollider>().isTrigger = true;
            var sample = _splineComputer.Evaluate(travel);
            collider.transform.position = sample.position + Vector3.up;
            collider.transform.forward = sample.forward;
            collider.transform.localScale = new Vector3(2f, 2f, .2f);
            var blockTrigger = collider.GetComponent<BlockTrigger>();
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

    public override void OnAwake()
    {
        base.OnAwake();

        _carriers = World.GetStash<BehaviourView<Carrier>>();
    }
}
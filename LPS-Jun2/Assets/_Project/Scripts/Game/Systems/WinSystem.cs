using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine.Pool;
using VContainer;

public sealed class WinSystem : SystemBase
{
    [Inject] private GameMachine _gameMachine;
    [Inject] private Conveyor _conveyor;

    [Inject] private ISubscriber<LevelBuildCompleteMessage> _levelBuildCompleteSub;

    private Stash<BehaviourView<Carrier>> _carriers;
    private Stash<BehaviourView<ConveyorSlot>> _conveyorSlots;

    private bool _isLevelBuilt;

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _levelBuildCompleteSub.Subscribe(OnLevelBuildComplete).AddTo(bag);
    }

    public override void OnAwake()
    {
        base.OnAwake();

        _carriers = World.GetStash<BehaviourView<Carrier>>();
        _conveyorSlots = World.GetStash<BehaviourView<ConveyorSlot>>();
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        if (_gameMachine.ActiveStateName != nameof(GamePlayingState)) return;
        CheckCarrierBlockColors();
    }

    private void CheckCarrierBlockColors()
    {
        if (!_isLevelBuilt) return;

        foreach (ConveyorSlot conveyorSlot in _conveyorSlots)
            if (conveyorSlot.HasElement())
                return;

        foreach (Carrier carrier in _carriers)
        {
            if (carrier.IsEmpty())
                continue;

            if (!carrier.CanComplete())
                return;
        }

        _gameMachine.RequestStateChange<GameWinState>();
    }

    private void OnLevelBuildComplete(LevelBuildCompleteMessage obj)
    {
        _isLevelBuilt = true;
    }

    public int RemainingCarrierCount()
    {
        using var p = HashSetPool<Block>.Get(out var remainingBlocks);
        foreach (Carrier carrier in _carriers)
        {
            if (carrier.IsEmpty()) continue;
            if (carrier.IsComplete()) continue;
            var blocks = carrier.GetBlocks();
            remainingBlocks.AddRange(blocks);
        }

        foreach (ConveyorSlot conveyorSlot in _conveyorSlots)
        {
            if (!conveyorSlot.HasElement()) continue;
            var elements = conveyorSlot.GetElements();
            remainingBlocks.AddRange(elements);
        }

        return remainingBlocks.Count / _conveyor.MaxBlockCount;
    }
}
using System.Collections.Generic;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine.Pool;
using VContainer;

public sealed class LoseSystem : SystemBase
{
    [Inject] private GameMachine _gameMachine;
    [Inject] private Level _level;
    [Inject] private Conveyor _conveyor;
    [Inject] private RemoteConfigModule _remoteConfigModule;

    [Inject] private ISubscriber<LevelBuildCompleteMessage> _levelBuildCompleteSub;
    [Inject] private ISubscriber<LevelLoseMessage> _levelLoseSub;

    private Filter _enabledCarriers;
    private Filter _conveyorSlotFilter;
    private Filter _conveyorSlotExtraFilter;
    private Stash<BehaviourView<ConveyorSlot>> _conveyorSlots;
    private Stash<BehaviourView<Carrier>> _carriers;
    private Stash<BlockLastCarrier> _blockLastCarriers;
    private Stash<BehaviourView<IceCarrier>> _iceCarriers;

    private bool _isLevelBuilt;
    private UniTimer _canCompleteCarrierTimer;
    private UniTimer _startCheckTimer;
    private LoseReason _loseReason;
    private LoseConfig _loseConfig;
    private IceBreakConfig _iceBreakConfig;

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _levelBuildCompleteSub.Subscribe(OnLevelBuildComplete).AddTo(bag);
        _levelLoseSub.Subscribe(OnLevelLose).AddTo(bag);
    }

    public override void OnAwake()
    {
        base.OnAwake();

        _enabledCarriers = World.Filter.With<BehaviourView<Carrier>>().With<Enabled>().Build();
        _conveyorSlotFilter = World.Filter.With<BehaviourView<ConveyorSlot>>().Without<ExtraSlot>().Build();
        _conveyorSlotExtraFilter = World.Filter.With<BehaviourView<ConveyorSlot>>().Build();
        _conveyorSlots = World.GetStash<BehaviourView<ConveyorSlot>>();
        _carriers = World.GetStash<BehaviourView<Carrier>>();
        _blockLastCarriers = World.GetStash<BlockLastCarrier>();
        _iceCarriers = World.GetStash<BehaviourView<IceCarrier>>();

        _loseConfig = _remoteConfigModule.GetDataClassNew<LoseConfig>();
        _iceBreakConfig = _remoteConfigModule.GetDataClassNew<IceBreakConfig>();
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        if (_gameMachine.ActiveStateName != nameof(GamePlayingState))
        {
            _startCheckTimer = UniTimer.CreateFromSeconds(.1f);
            return;
        }
        if (!_startCheckTimer.IsExpired) return;
        if (!_isLevelBuilt) return;

        var isConveyorFull = IsConveyorFull();
        var canCompleteCarrier = CanCompleteCarrier();
        if (canCompleteCarrier) _canCompleteCarrierTimer = UniTimer.CreateFromSeconds(3f);
        var canCompleteCarrierExpired = _canCompleteCarrierTimer.IsExpired;

        if (!isConveyorFull && !canCompleteCarrierExpired) return;

        _loseReason = isConveyorFull ? LoseReason.ConveyorFull : LoseReason.CantCompleteCarrier;

        if (_loseReason == LoseReason.CantCompleteCarrier)
        {
            if (!_iceBreakConfig.BreakFail)
            {
                foreach (IceCarrier iceCarrier in _iceCarriers)
                {
                    if (iceCarrier.IsBroken()) continue;
                    iceCarrier.Break();
                }

                return;
            }
        }

        _gameMachine.RequestStateChange<GameReviveState>();
    }

    public bool IsConveyorFull()
    {
        foreach (var e in _conveyorSlotExtraFilter)
        {
            ConveyorSlot conveyorSlot = _conveyorSlots.Get(e);
            // if (conveyorSlot.HasSpace())
            //     return false;

            if (conveyorSlot.IsElementBeingAdded())
                return false;

            if (_loseConfig.FailOnFull)
                continue;

            var elements = conveyorSlot.GetElements();
            foreach (var element in elements)
            {
                var betterCarrier = default(Carrier);
                foreach (var entity in _enabledCarriers)
                {
                    Carrier carrier = _carriers.Get(entity);

                    if (!carrier.IsBetterCarrier(element)) continue;

                    betterCarrier = carrier;
                    break;
                }

                if (betterCarrier != null)
                {
                    if (CanTransferBlockTo(betterCarrier, element))
                        return false;

                    continue;
                }

                foreach (var entity in _enabledCarriers)
                {
                    Carrier carrier = _carriers.Get(entity);

                    if (!CanTransferBlockTo(carrier, element)) continue;

                    if (_blockLastCarriers.Has(element))
                    {
                        var blockLastCarrier = _blockLastCarriers.Get(element);
                        if (blockLastCarrier.Carrier == carrier) continue;
                    }

                    return false;
                }
            }
        }

        var occupiedSlotCount = _conveyor.GetOccupiedSlotCount(withoutExtra: false);
        var totalSlotCount = _conveyor.GetTotalSlotCount();
        return occupiedSlotCount >= totalSlotCount;
    }

    private bool CanTransferBlockTo(Carrier carrier, Block element)
    {
        if (!carrier.CanBeginTransfer()) return false;
        if (!carrier.CanTransferBlock(element)) return false;
        if (carrier.IsFull()) return false;
        if (!element.IsCompatibleWith(carrier)) return false;
        return true;
    }

    private bool CanCompleteCarrier()
    {
        var maxBlockCount = _conveyor.MaxBlockCount;
        var totalBlockCount = 0;

        using var p = DictionaryPool<ColorType, int>.Get(out var colors);
        foreach (var entity in _enabledCarriers)
        {
            Carrier carrier = _carriers.Get(entity);
            if (carrier.IsComplete()) continue;

            var blocks = carrier.GetBlocks();
            totalBlockCount += blocks.Count;

            if (!carrier.CanBeginTransfer()) continue;
            if (carrier.IsEmpty()) continue;

            foreach (var block in blocks)
            {
                if (block.CanBlockToLoseSystem()) return true;
                IncrementColorCount(colors, block);
                var colorCount = colors[block.ColorType];
                if (colorCount >= maxBlockCount) return true;
            }
        }

        foreach (ConveyorSlot conveyorSlot in _conveyorSlots)
        {
            if (!conveyorSlot.HasElement()) continue;
            foreach (var block in conveyorSlot.GetElements())
            {
                IncrementColorCount(colors, block);
                var colorCount = colors[block.ColorType];
                if (colorCount >= maxBlockCount) return true;
            }
        }

        return colors.Count == 0 && totalBlockCount == 0;
    }

    public LoseReason GetLoseReason()
    {
        return _loseReason;
    }

    private void OnLevelBuildComplete(LevelBuildCompleteMessage m)
    {
        _isLevelBuilt = true;
    }

    private void OnLevelLose(LevelLoseMessage m)
    {
        _level.LoseCount.Value++;
    }

    private static void IncrementColorCount(Dictionary<ColorType, int> colors, Block block)
    {
        if (!colors.TryGetValue(block.ColorType, out var count))
            colors[block.ColorType] = 1;
        else
            colors[block.ColorType] = count + 1;
    }
}

public enum LoseReason
{
    ConveyorFull,
    CantCompleteCarrier,
}
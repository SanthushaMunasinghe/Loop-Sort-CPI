using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Dreamteck.Splines;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine;
using VContainer;

public sealed class ConveyorSlotCreateSystem : SystemBase
{
    [Inject] private Conveyor _conveyor;
    [Inject] private ConveyorConfig _config;
    [Inject] private SplineComputer _splineComputer;
    [Inject] private PrefabModule _prefabModule;
    [Inject] private RemoteConfigModule _remoteConfigModule;

    [Inject] private ISubscriber<LevelBuildCompleteMessage> _levelBuildCompleteSub;
    [Inject] private IPublisher<SlotCreateCompleteMessage> _slotCreateCompletePub;

    private Stash<ExtraSlot> _extraSlots;

    private readonly List<SplineFollower> _slots = new();

    public int LengthBasedSlotCount { get; private set; }

    public override void OnAwake()
    {
        base.OnAwake();

        _extraSlots = World.GetStash<ExtraSlot>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _levelBuildCompleteSub.Subscribe(OnLevelBuildComplete).AddTo(bag);
    }

    private void OnLevelBuildComplete(LevelBuildCompleteMessage m)
    {
        CreateSlots().Forget();
    }

    private async UniTaskVoid CreateSlots()
    {
        var parent = _splineComputer.transform;
        var length = _splineComputer.CalculateLength();

        var groupSlotCount = _conveyor.GroupSlotCount;
        var slotCount = _conveyor.SlotCount;
        var totalSlotCount = groupSlotCount * slotCount;

        var slotCollisionDistance = _config.SlotCollisionDistance;

        var lengthBasedSlotCount = Mathf.FloorToInt(length / slotCollisionDistance);
        lengthBasedSlotCount = NormalizeSlotCount(lengthBasedSlotCount);

        var paceConfig = _remoteConfigModule.GetDataClassNew<PaceConfig>();

        var lengthSlot = (float)lengthBasedSlotCount / groupSlotCount;
        var conveyorSpeed =
            _config.Speed + (_config.Speed * (lengthSlot / slotCount) - _config.Speed) * .5f;
        conveyorSpeed *= paceConfig.ConveyorSpeedMultiplier;
        _conveyor.SetSpeed(conveyorSpeed);

        for (var i = 0; i < totalSlotCount; i++)
        {
            CreateConveyorSlot(parent);
        }

        var conveyorSystemConfig = _remoteConfigModule.GetDataClassNew<ConveyorSystemConfig>();

        if (conveyorSystemConfig.LoanSystem)
        {
            var extraSlotCount = groupSlotCount * _config.LoanSystemSlotCount;
            totalSlotCount += extraSlotCount;
            for (var i = 0; i < extraSlotCount; i++)
            {
                var instance = CreateConveyorSlot(parent);
                _extraSlots.Add(instance);
            }
        }

        await UniTask.NextFrame(SceneLoadToken);

        var adaptiveSize = length / totalSlotCount;
        slotCollisionDistance = conveyorSystemConfig.AdaptiveSize
            ? adaptiveSize
            : Mathf.Min(slotCollisionDistance, adaptiveSize);

        for (var i = 0; i < totalSlotCount; i++)
        {
            var distance = slotCollisionDistance * i;
            var travel = _splineComputer.Travel(0d, distance);
            var splineFollower = _slots[i];
            splineFollower.SetPercent(travel);
        }

        _conveyor.SetCollisionDistance(slotCollisionDistance);

        // Debug.Log($"Slot Count: {totalSlotCount / blockGroupSlotCount} - " +
        //           $"Length Based Slot Count: {lengthBasedSlotCount / blockGroupSlotCount}");

        LengthBasedSlotCount = lengthBasedSlotCount / groupSlotCount;

        _slotCreateCompletePub.Publish(new SlotCreateCompleteMessage());
    }

    private ConveyorSlot CreateConveyorSlot(Transform parent)
    {
        var instance = _prefabModule.Rent(_config.SlotPrefab, parent);
        var splineFollower = instance.GetComponent<SplineFollower>();
        _conveyor.UpdateFollower(splineFollower);
        _slots.Add(splineFollower);
        return splineFollower.GetComponent<ConveyorSlot>();
    }

    private int NormalizeSlotCount(int count)
    {
        var groupSlotCount = _conveyor.GroupSlotCount;
        var a = count % groupSlotCount;
        var b = groupSlotCount - a;
        if (b > a) count -= a;
        else count += b;
        return count;
    }
}

public struct SlotCreateCompleteMessage
{
}

public struct ExtraSlot : IComponent
{
}
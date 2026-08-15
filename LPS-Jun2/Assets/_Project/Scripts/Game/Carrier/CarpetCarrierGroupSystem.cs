using System.Collections.Generic;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine.Pool;
using VContainer;

public sealed class CarpetCarrierGroupSystem : SystemBase
{
    [Inject] private LevelSheet.Level _level;
    [Inject] private Features _features;
    [Inject] private PrefabModule _prefabModule;

    [Inject] private ISubscriber<LevelBuildCompleteMessage> _levelBuildCompleteSub;
    [Inject] private ISubscriber<CarrierCompleteMessage> _carrierCompleteSub;
    [Inject] private ISubscriber<LevelAnimationEnterStartMessage> _levelAnimationEnterStartSub;
    [Inject] private ISubscriber<LevelAnimationCarrierCompleteMessage> _levelAnimationCarrierCompleteSub;

    private Stash<BehaviourView<Carrier>> _carriers;

    private readonly List<CarpetCarrierGroup> _groups = new();

    public override void OnAwake()
    {
        base.OnAwake();

        _carriers = World.GetStash<BehaviourView<Carrier>>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _levelBuildCompleteSub.Subscribe(OnLevelBuildComplete).AddTo(bag);
        _carrierCompleteSub.Subscribe(OnCarrierComplete).AddTo(bag);
        _levelAnimationEnterStartSub.Subscribe(OnLevelAnimationEnterStart).AddTo(bag);
        _levelAnimationCarrierCompleteSub.Subscribe(OnLevelAnimationCarrierComplete).AddTo(bag);
    }

    private void OnLevelBuildComplete(LevelBuildCompleteMessage m)
    {
        Create();
    }

    private void OnCarrierComplete(CarrierCompleteMessage m)
    {
        foreach (var group in _groups)
            group.RevealNext();
    }

    private void OnLevelAnimationEnterStart(LevelAnimationEnterStartMessage m)
    {
        foreach (var carrierGroup in _groups)
        {
            carrierGroup.OnLevelAnimationStart();
        }
    }

    private void OnLevelAnimationCarrierComplete(LevelAnimationCarrierCompleteMessage m)
    {
        foreach (var carrierGroup in _groups)
        {
            carrierGroup.OnLevelAnimationEnd();
        }
    }

    private void Create()
    {
        using var p = HashSetPool<Carrier>.Get(out var checkedCarriers);
        var carriersRef = _level.Carriers.Ref;
        foreach (Carrier carrier in _carriers)
        {
            if (checkedCarriers.Contains(carrier)) continue;
            if (!carriersRef.HasCarrierFeature(carrier.Type, FeatureType.Carpet)) continue;
            if (!carriersRef.TryGetCarrierGroup(carrier.Type, FeatureType.Carpet, out var group)) continue;

            var data = _features.Get(FeatureType.Carpet);
            var instance = _prefabModule.Rent(data.Prefab).GetComponent<CarpetCarrierGroup>();
            _groups.Add(instance);

            foreach (var carrierType in group)
            {
                var targetCarrier = FindCarrier(carrierType);
                instance.AddCarrier(targetCarrier);
                checkedCarriers.Add(targetCarrier);
            }

            instance.Initialize();
        }
    }

    private Carrier FindCarrier(CarrierSheet.CarrierType carrierType)
    {
        foreach (Carrier carrier in _carriers)
            if (carrier.Type == carrierType)
                return carrier;
        return null;
    }
}
using System.Collections.Generic;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public sealed class LockCarrierGroupSystem : SystemBase
{
    [Inject] private LevelSheet.Level _level;
    [Inject] private Features _features;
    [Inject] private PrefabModule _prefabModule;
    [Inject] private Level _levelData;

    [Inject] private ISubscriber<LevelBuildCompleteMessage> _levelBuildCompleteSub;
    [Inject] private ISubscriber<KeyBlockCollectMessage> _keyBlockCollectSub;
    [Inject] private ISubscriber<LevelAnimationEnterStartMessage> _levelAnimationEnterStartSub;
    [Inject] private ISubscriber<LevelAnimationCarrierCompleteMessage> _levelAnimationCarrierCompleteSub;

    private Stash<BehaviourView<Carrier>> _carriers;

    private readonly Dictionary<ColorType, LockCarrierGroup> _groupByColor = new();

    public override void OnAwake()
    {
        base.OnAwake();

        _carriers = World.GetStash<BehaviourView<Carrier>>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _levelBuildCompleteSub.Subscribe(OnLevelBuildComplete).AddTo(bag);
        _keyBlockCollectSub.Subscribe(OnKeyBlockCollect).AddTo(bag);
        _levelAnimationEnterStartSub.Subscribe(OnLevelAnimationEnterStart).AddTo(bag);
        _levelAnimationCarrierCompleteSub.Subscribe(OnLevelAnimationCarrierComplete).AddTo(bag);
    }

    private void OnLevelBuildComplete(LevelBuildCompleteMessage m)
    {
        Create();
    }

    private void OnKeyBlockCollect(KeyBlockCollectMessage m)
    {
        if (!_groupByColor.TryGetValue(m.KeyBlockGroup.ColorType, out var lockGroup)) return;
        lockGroup.Unlock(m.KeyBlockGroup);
    }

    private void OnLevelAnimationEnterStart(LevelAnimationEnterStartMessage m)
    {
        foreach (var carrierGroup in _groupByColor.Values)
        {
            carrierGroup.OnLevelAnimationStart();
        }
    }

    private void OnLevelAnimationCarrierComplete(LevelAnimationCarrierCompleteMessage m)
    {
        foreach (var carrierGroup in _groupByColor.Values)
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
            if (!carriersRef.TryGetCarrierFeature(carrier.Type, FeatureType.Lock, out var feature)) continue;
            if (!carriersRef.TryGetCarrierGroup(carrier.Type, FeatureType.Lock, out var group)) continue;

            var data = _features.Get(FeatureType.Lock);
            var instance = _prefabModule.Rent(data.Prefab).GetComponent<LockCarrierGroup>();
            var colorType = _levelData.GetColor(feature.FeatureData);
            instance.SetColorType(colorType);
            _groupByColor[colorType] = instance;

            foreach (var carrierType in group)
            {
                var targetCarrier = FindCarrier(carrierType);
                if (targetCarrier == null) Debug.Log($"{carrierType} is not found. {nameof(LockCarrierGroupSystem)}");
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
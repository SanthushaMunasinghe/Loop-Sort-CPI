using MessagePipe;
using Scellecs.Morpeh;
using VContainer;

public sealed class CarrierBlockCreateSystem : SystemBase
{
    [Inject] private LevelSheet.Level _level;
    [Inject] private PrefabModule _prefabModule;
    [Inject] private CarrierConfig _config;
    [Inject] private Blocks _blocks;
    [Inject] private RemoteConfigModule _remoteConfigModule;
    [Inject] private Level _levelData;
    [Inject] private Conveyor _conveyor;

    [Inject] private ISubscriber<LevelBuildCompleteMessage> _levelBuildCompleteSub;

    [Inject] private IPublisher<CarrierBlockCreateMessage> _carrierBlockCreatePub;
    [Inject] private IPublisher<CarrierBlockCreateCompleteMessage> _carrierBlockCreateCompletePub;

    private Filter _filter;
    private Stash<BehaviourView<Carrier>> _carriers;
    private Stash<FeatureContainer> _featureContainers;
    private PaceConfig _paceConfig;

    public override void OnAwake()
    {
        base.OnAwake();

        _filter = World.Filter.With<BehaviourView<Carrier>>().With<Enabled>().Build();
        _carriers = World.GetStash<BehaviourView<Carrier>>();
        _featureContainers = World.GetStash<FeatureContainer>();
        _paceConfig = _remoteConfigModule.GetDataClassNew<PaceConfig>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _levelBuildCompleteSub.Subscribe(OnLevelBuildComplete).AddTo(bag);
    }

    private void OnLevelBuildComplete(LevelBuildCompleteMessage obj)
    {
        CreateBlocks();
    }

    private void CreateBlocks()
    {
        var carriersRef = _level.Carriers.Ref;
        var blockCount = _conveyor.GroupBlockCount;
        foreach (var entity in _filter)
        {
            ref var view = ref _carriers.Get(entity);
            var carrier = view.Behaviour;

            if (!carriersRef.TryGetCarrier(carrier.Type, out var carrierColors))
                continue;

            foreach (var args in carrierColors)
            {
                var data = _blocks.Get(args.Feature);
                for (var i = 0; i < blockCount; i++)
                {
                    var instance = _prefabModule.Rent(data.Prefab);
                    instance.SetColorType(_levelData.GetColor(args.Color));
                    instance.SetFeatureType(args.Feature);
                    instance.SetMotionDurationMultiplier(_paceConfig.BlockAnimationDurationMultiplier);
                    carrier.AddBlock(instance, motion: false);

                    _featureContainers.Set(instance, new FeatureContainer { Data = args.FeatureData });
                    if (instance.TryGetComponent<IFeatureProcessor>(out var featureProcessor))
                        featureProcessor.ProcessFeature(args.Feature, args.FeatureData);
                }
            }

            _carrierBlockCreatePub.Publish(new CarrierBlockCreateMessage
            {
                Carrier = carrier
            });
        }
        _carrierBlockCreateCompletePub.Publish(new CarrierBlockCreateCompleteMessage());
    }
}

public struct CarrierBlockCreateMessage
{
    public Carrier Carrier;
}

public struct CarrierBlockCreateCompleteMessage
{
}
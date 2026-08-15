using System.Collections.Generic;
using MessagePipe;
using Scellecs.Morpeh;
using VContainer;

public sealed class KeyBlockGroupSystem : SystemBase
{
    [Inject] private Features _features;
    [Inject] private PrefabModule _prefabModule;
    [Inject] private Level _level;

    [Inject] private ISubscriber<CarrierBlockCreateMessage> _carrierBlockCreateSub;
    [Inject] private ISubscriber<BlockNextTransferUpdateMessage> _nextTransferUpdateSub;

    private Stash<FeatureContainer> _featureContainers;
    private Stash<BlockNextTransfer> _blockNextTransfers;

    private readonly Dictionary<ColorType, KeyBlockGroup> _keyByColor = new();
    private readonly Dictionary<Block, KeyBlockGroup> _keyByBlock = new();

    public override void OnAwake()
    {
        base.OnAwake();

        _featureContainers = World.GetStash<FeatureContainer>();
        _blockNextTransfers = World.GetStash<BlockNextTransfer>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _carrierBlockCreateSub.Subscribe(OnCarrierBlockCreate).AddTo(bag);
        _nextTransferUpdateSub.Subscribe(OnBlockNextTransferUpdate).AddTo(bag);
    }

    private void OnCarrierBlockCreate(CarrierBlockCreateMessage m)
    {
        CreateKeys(m.Carrier);
    }

    private void OnBlockNextTransferUpdate(BlockNextTransferUpdateMessage m)
    {
        if (!_blockNextTransfers.Has(m.Block)) return;
        TryCollectKey(m.Block);
    }

    private void CreateKeys(Carrier carrier)
    {
        var blocks = carrier.GetBlocks();
        foreach (var block in blocks)
        {
            if (block.FeatureType != FeatureType.Key)
                continue;

            var featureContainer = _featureContainers.Get(block, out var exists);
            if (!exists) continue;
            var colorType = _level.GetColor(featureContainer.Data);

            if (!_keyByColor.TryGetValue(colorType, out var key))
            {
                var data = _features.Get(FeatureType.Key);
                var instance = _prefabModule.Rent(data.Prefab, carrier.transform).GetComponent<KeyBlockGroup>();
                key = instance;
                _keyByColor[colorType] = key;
                key.SetColorType(colorType);
            }

            key.AddBlock(block);
            _keyByBlock[block] = key;
        }

        foreach (var keyBlockGroup in _keyByColor.Values)
        {
            keyBlockGroup.Initialize();
        }
    }

    private void TryCollectKey(Block block)
    {
        if (!_keyByBlock.TryGetValue(block, out var key)) return;
        key.Collect();
    }
}
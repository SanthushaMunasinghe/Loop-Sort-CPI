using System.Collections.Generic;
using MessagePipe;
using UnityEngine;
using VContainer;

public sealed class KeyBlockGroup : GameBehaviourBase, IBlockFeature
{
    [Inject] private IPublisher<KeyBlockCollectMessage> _keyBlockCollectPub;

    [field: SerializeField] public Transform Model { get; private set; }

    public ColorType ColorType { get; private set; }

    private bool _isCollected;

    private readonly List<Block> _blocks = new();

    public override void OnReturn()
    {
        base.OnReturn();

        ColorType = default;
        _isCollected = false;
        _blocks.Clear();
        ResetModel();
    }

    public void Initialize()
    {
        InjectColorType(ColorType);

        var centerPoint = _blocks.GetCenterPoint();
        transform.position = centerPoint;

        var firstBlock = _blocks[0];
        var carrier = firstBlock.Container as Carrier;
        if (carrier == null) return;
        transform.rotation = carrier.transform.rotation;
    }

    public void SetColorType(ColorType colorType)
    {
        ColorType = colorType;
    }

    public void AddBlock(Block block)
    {
        _blocks.Add(block);
        block.SetFeature(this);
    }

    public void Collect()
    {
        if (_isCollected) return;
        _isCollected = true;
        _keyBlockCollectPub.Publish(new KeyBlockCollectMessage { KeyBlockGroup = this });
    }

    public void ResetModel()
    {
        Model.localPosition = Vector3.zero;
        Model.localRotation = Quaternion.identity;
        Model.localScale = Vector3.one;
    }

    public bool IsCompatibleWith(Block block)
    {
        return true;
    }

    public bool CanBlockToLoseSystem()
    {
        return !_isCollected;
    }
}

public struct KeyBlockCollectMessage
{
    public KeyBlockGroup KeyBlockGroup;
}
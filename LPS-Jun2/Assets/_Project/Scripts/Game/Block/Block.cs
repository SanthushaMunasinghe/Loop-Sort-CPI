using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed partial class Block : GameBehaviourBase
{
    public ColorType ColorType { get; private set; }
    public FeatureType FeatureType { get; private set; }
    public float MotionDurationMultiplier { get; private set; }
    public IBlockContainer Container { get; private set; }
    public MeshFilter MeshFilter { get; private set; }
    public MeshRenderer MeshRenderer { get; private set; }
    public Rigidbody Rigidbody { get; private set; }
    public SphereCollider Collider { get; private set; }

    private IBlockFeature _blockFeature;
    private bool _isContainerChanged;
    private bool _isTransferLocked;

    protected override void Awake()
    {
        base.Awake();

        MeshFilter = GetComponent<MeshFilter>();
        MeshRenderer = GetComponent<MeshRenderer>();
        Rigidbody = GetComponent<Rigidbody>();
        Collider = GetComponent<SphereCollider>();
    }

    public override void OnRent()
    {
        base.OnRent();

        _blockFeature = GetComponent<IBlockFeature>();
        RegisterView<Block>();
    }

    public override void OnReturn()
    {
        base.OnReturn();

        _blockFeature = null;
        ClearContainer();
    }

    public void SetColorType(ColorType colorType)
    {
        ColorType = colorType;
        SetNormalColor();
    }

    public void SetNormalColor()
    {
        InjectColorType(ColorType);
    }

    public void SetDarkColor()
    {
        var colorOffset = Color.HSVToRGB(0f, 0f, -.1f);
        InjectColorType(ColorType, colorOffset);
    }

    public void SetFeatureType(FeatureType featureType)
    {
        FeatureType = featureType;
    }

    public void SetMotionDurationMultiplier(float durationMultiplier)
    {
        MotionDurationMultiplier = durationMultiplier;
    }

    public void SetContainer(IBlockContainer container)
    {
        Container?.RemoveBlock(this);
        Container = container;
        _isContainerChanged = true;
    }

    public void SetFeature(IBlockFeature feature)
    {
        _blockFeature = feature;
    }

    public void ClearContainer()
    {
        Container = null;
    }

    public void CompleteContainer()
    {
        _isContainerChanged = false;
    }

    public bool IsContainerModified()
    {
        return _isContainerChanged;
    }

    public bool IsCompatibleWith(Carrier carrier)
    {
        if (carrier.IsEmpty()) return true;
        var nextColorType = carrier.GetNextColorType();
        return ColorType == nextColorType;
    }

    public bool IsCompatibleWith(Block block)
    {
        return ColorType == block.ColorType ||
               (FeatureType != FeatureType.None && FeatureType == block.FeatureType &&
                _blockFeature != null && _blockFeature.IsCompatibleWith(this));
    }

    public void EnableTransfer()
    {
        _isTransferLocked = false;
    }

    public void DisableTransfer()
    {
        _isTransferLocked = true;
    }

    public bool CanBeginTransfer()
    {
        return !_isTransferLocked;
    }

    public bool CanBlockToLoseSystem()
    {
        return _blockFeature != null && _blockFeature.CanBlockToLoseSystem();
    }
}

public interface IBlockContainer
{
    public UniTask AddBlock(Block block, float delay = 0f, bool motion = true);
    public void RemoveBlock(Block block);
}

public interface IBlockFeature
{
    public bool IsCompatibleWith(Block block);
    public bool CanBlockToLoseSystem();
}

public interface IBlockTransferHandler
{
    public bool CanTransferBlock(Block block);
    public bool IsBetterCarrier(Block block);
}
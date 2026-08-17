using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

public sealed partial class Block : GameBehaviourBase
{
    [Inject] private RemoteConfigModule _remoteConfigModule;

    // Serialized so a block generated into the scene keeps its colour and feature across a save.
    [field: SerializeField] public ColorType ColorType { get; private set; }
    [field: SerializeField] public FeatureType FeatureType { get; private set; }
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

        // Drives both the duration and the arc height of the transfer motion. This used to be set
        // by CarrierBlockCreateSystem right after renting; blocks generated into the scene never
        // went through that, and a multiplier of 0 makes every transfer a teleport. Reading it here
        // covers generated, hand-added and spawned blocks alike.
        MotionDurationMultiplier = _remoteConfigModule.GetDataClassNew<PaceConfig>().BlockAnimationDurationMultiplier;

        // Generated blocks already carry a ColorType; re-apply it now that Colors is injected.
        if (ColorType.Base != BaseColor.None) SetNormalColor();
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

    /// <summary>
    /// Swaps the colour a generated block was saved with, before anything has been injected — which
    /// is why this touches the field only. SceneScope calls it from its own Awake, ahead of the
    /// OnRent above, and that is what puts the new material on.
    /// </summary>
    public void OverrideColorType(ColorType colorType)
    {
        ColorType = colorType;
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

#if UNITY_EDITOR
    /// <summary>
    /// Used by LevelSandboxGenerator. Colour and feature are serialized so they survive the scene
    /// save, and the material is applied straight away so the generated level looks right in the
    /// scene view rather than only once you press Play.
    /// </summary>
    public void EditorSetColor(Colors colors, ColorType colorType, FeatureType featureType)
    {
        ColorType = colorType;
        FeatureType = featureType;
        EditorInjectColorType(colors, colorType);
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

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
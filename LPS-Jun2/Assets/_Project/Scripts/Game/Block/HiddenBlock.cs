using UnityEngine;

public sealed class HiddenBlock : GameBehaviourBase, IBlockFeature, IFeatureProcessor
{
    [SerializeField] private MeshRenderer MeshRenderer;
    [SerializeField] private Material HiddenMaterial;

    public Block Block { get; private set; }

    private bool _revealed;

    protected override void Awake()
    {
        base.Awake();

        Block = GetComponent<Block>();
    }

    public override void OnRent()
    {
        base.OnRent();

        _revealed = false;
        RegisterView<HiddenBlock>();
    }

    public void Reveal()
    {
        _revealed = true;
        Block.EnableTransfer();
    }

    public void Open()
    {
        Block.SetNormalColor();
    }

    public bool IsRevealed()
    {
        return _revealed;
    }

    public bool IsCompatibleWith(Block block)
    {
        return !_revealed;
    }

    public bool CanBlockToLoseSystem()
    {
        return false;
    }

    public void ProcessFeature(FeatureType featureType, string data)
    {
        _revealed = false;
        Block.DisableTransfer();
        MeshRenderer.sharedMaterial = HiddenMaterial;
    }
}
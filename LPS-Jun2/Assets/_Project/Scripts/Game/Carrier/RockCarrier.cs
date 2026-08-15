using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

public sealed class RockCarrier : GameBehaviourBase, IFeatureProcessor, ILevelAnimationListener, IBlockTransferHandler
{
    [SerializeField] private Transform Barrier;
    [SerializeField] private Transform Model;

    private Carrier _carrier;
    private float _originalLocalPositionY;

    protected override void Awake()
    {
        base.Awake();

        _carrier = GetComponent<Carrier>();
        _originalLocalPositionY = Barrier.localPosition.y;
    }

    public void ProcessFeature(FeatureType featureType, string data)
    {
        if (featureType != FeatureType.Rock) return;

        _carrier.DisableInteraction(gameObject);
        Barrier.localPosition = Barrier.localPosition.WithY(_originalLocalPositionY);
    }

    public void ApplyCompleteMotion()
    {
        LMotion.Create(Barrier.localPosition.y, -.2f, .5f)
            .BindToLocalPositionY(Barrier)
            .AddTo(this);
    }

    public void OnLevelAnimationStart()
    {
        Model.position = Model.position.AddY(-2f);
    }

    public void OnLevelAnimationComplete()
    {
        LMotion.Create(-2f, 0f, .5f)
            .BindToLocalPositionY(Model)
            .AddTo(this);
    }

    public bool CanTransferBlock(Block block)
    {
        return true;
    }

    public bool IsBetterCarrier(Block block)
    {
        if (_carrier.IsFull()) return false;
        if (!_carrier.AreAllBlocksSameColor()) return false;
        var nextColorType = _carrier.GetNextColorType();
        return block.ColorType == nextColorType;
    }
}
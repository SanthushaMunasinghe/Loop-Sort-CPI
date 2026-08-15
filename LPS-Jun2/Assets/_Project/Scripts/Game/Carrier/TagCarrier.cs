using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using VContainer;

public sealed class TagCarrier : GameBehaviourBase, IFeatureProcessor, IBlockTransferHandler
{
    [SerializeField] private GameObject Model;

    [Inject] private Level _level;

    private Carrier _carrier;
    private ColorType _requiredColorType;
    private Quaternion _activeRotation;
    private Quaternion _inactiveRotation;
    private bool _isActive;

    public override void OnRent()
    {
        base.OnRent();

        _carrier ??= GetComponent<Carrier>();
        RegisterView<TagCarrier>();
    }

    public void ProcessFeature(FeatureType featureType, string data)
    {
        if (featureType != FeatureType.Tag) return;

        Model.transform.localScale = Vector3.one;
        _requiredColorType = _level.GetColor(data);
        InjectColorType(_requiredColorType, Model);
        InjectColorType(_requiredColorType);
    }

    public bool CanTransferBlock(Block block)
    {
        return block.ColorType == _requiredColorType;
    }

    public bool IsBetterCarrier(Block block)
    {
        return block.ColorType == _requiredColorType;
    }

    public void AdjustRotation()
    {
        var angle = Vector3.SignedAngle(Vector3.forward, transform.forward, Vector3.up);
        var direction = angle switch
        {
            > -45f and < 45f => Direction.Right,
            > 45f and <= 135f => Direction.Right,
            _ => Direction.Left
        };

        var modelT = Model.transform;
        modelT.rotation = direction.ToLookRotation();
        _activeRotation = modelT.rotation;
        _inactiveRotation = Quaternion.AngleAxis(90f, modelT.forward) * modelT.rotation;
        _isActive = _carrier.CanInteract();
        modelT.rotation = _isActive ? _activeRotation : _inactiveRotation;
    }

    public void SetModelActive()
    {
        if (_isActive) return;
        _isActive = true;

        LMotion.Create(_inactiveRotation, _activeRotation, .6f)
            .BindToRotation(Model.transform)
            .AddTo(this);
    }

    public void SetModelInactive()
    {
        if (!_isActive) return;
        _isActive = false;

        Model.transform.rotation = _inactiveRotation;
    }

    public void ApplyCompleteMotion()
    {
        var modelT = Model.transform;
        LMotion.Create(Vector3.one, Vector3H.AlmostZero, .5f)
            .WithDelay(.25f)
            .WithEase(Ease.InBack)
            .BindToLocalScale(modelT)
            .AddTo(this);
    }
}
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using VContainer;

public sealed class IceCarrier : GameBehaviourBase, IFeatureProcessor, ILevelAnimationListener
{
    [SerializeField] private GameObject Model;
    [SerializeField] private Transform BreakParticlePoint;

    [Inject] private Particles _particles;

    private Carrier _carrier;
    private bool _isBroken;
    private Vector3 _originalModelLocalPosition;

    private readonly List<CarrierSheet.CarrierType> _requiredCarrierTypes = new();

    protected override void Awake()
    {
        base.Awake();

        _carrier = GetComponent<Carrier>();
        _originalModelLocalPosition = Model.transform.localPosition;
    }

    public override void OnRent()
    {
        base.OnRent();

        RegisterView<IceCarrier>();
        _isBroken = false;
        Model.SetActive(true);
        _requiredCarrierTypes.Clear();
    }

    public void ProcessFeature(FeatureType featureType, string data)
    {
        if (featureType != FeatureType.Ice) return;
        if (!Enum.TryParse(data, ignoreCase: true, out CarrierSheet.CarrierType type)) return;

        _requiredCarrierTypes.Add(type);
        _carrier.DisableInteraction(gameObject);
        _carrier.DisableTransfer(gameObject);
    }

    public bool IsBroken()
    {
        return _isBroken;
    }

    public void Break()
    {
        BreakDelay().Forget();
    }

    private async UniTaskVoid BreakDelay()
    {
        _carrier.EnableTransfer(gameObject);

        await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: SceneLoadToken);

        _isBroken = true;
        _carrier.EnableInteraction(gameObject);
        Model.SetActive(false);
        PrefabModule.Rent(_particles.CarrierIceBreak, BreakParticlePoint.position, BreakParticlePoint.rotation);
    }

    public bool MatchesRequiredCarrierType(Carrier carrier)
    {
        return _requiredCarrierTypes.Contains(carrier.Type);
    }

    public void OnLevelAnimationStart()
    {
        Model.transform.localPosition = _originalModelLocalPosition.AddY(-3f);
    }

    public void OnLevelAnimationComplete()
    {
        var from = Model.transform.localPosition.y;
        var to = _originalModelLocalPosition.y;
        LMotion.Create(from, to, .4f)
            .BindToLocalPositionY(Model.transform)
            .AddTo(this);
    }
}
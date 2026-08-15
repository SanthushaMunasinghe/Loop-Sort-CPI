using MessagePipe;
using StatefulUI.Runtime.Core;
using StatefulUISupport.Scripts.Components;
using UnityEngine;
using VContainer;

public sealed class HiddenCarrier : GameBehaviourBase, IFeatureProcessor
{
    [SerializeField] private SkinnedMeshRenderer Model;
    [SerializeField] private StatefulComponent View;
    [SerializeField] private Animator Animator;

    [Inject] private Level _level;
    [Inject] private SettingsModule _settingsModule;

    [Inject] private ISubscriber<ColorBlindUpdateMessage> _colorBlindUpdateSub;

    private Carrier _carrier;
    private bool _isRevealed;
    private ColorType _requiredColorType;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int RevealHash = Animator.StringToHash("Reveal");
    private static readonly int ResetHash = Animator.StringToHash("Reset");

    public override void OnRent()
    {
        base.OnRent();

        RegisterView<HiddenCarrier>();
        _carrier ??= GetComponent<Carrier>();
    }

    public override void OnReturn()
    {
        base.OnReturn();

        _isRevealed = false;
        View.gameObject.SetActive(true);
        Model.gameObject.SetActive(true);
        Animator.SetTrigger(ResetHash);
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        base.BuildMessages(bag);

        _colorBlindUpdateSub.Subscribe(OnColorBlindUpdate).AddTo(bag);
    }

    public void ProcessFeature(FeatureType featureType, string data)
    {
        if (featureType != FeatureType.Hidden) return;

        _carrier.DisableInteraction(gameObject);
        _carrier.DisableTransfer(gameObject);
        _requiredColorType = _level.GetColor(data);
        InjectColorType(_requiredColorType, Model.gameObject);

        UpdateIconView();
    }

    private void UpdateIconView()
    {
        var colorData = Colors.Get(_requiredColorType);
        var icon = View.GetImage(ImageRole.Icon);
        var isColorBlind = _settingsModule.IsColorBlindActive();
        icon.overrideSprite = isColorBlind ? colorData.Sprite : null;
        icon.color = isColorBlind ? colorData.Color : colorData.Material.GetColor(BaseColorId);
        View.transform.localScale = isColorBlind ? Vector3.one * .9f : Vector3.one;
        View.GetImage(ImageRole.Background).gameObject.SetActive(!isColorBlind);
    }

    public bool IsRevealed()
    {
        return _isRevealed;
    }

    public ColorType GetRequiredColorType()
    {
        return _requiredColorType;
    }

    public void Reveal()
    {
        _isRevealed = true;
        _carrier.EnableInteraction(gameObject);
        _carrier.EnableTransfer(gameObject);
        View.gameObject.SetActive(false);
        Animator.SetTrigger(RevealHash);
    }

    private void OnColorBlindUpdate(ColorBlindUpdateMessage m)
    {
        UpdateIconView();
    }
}
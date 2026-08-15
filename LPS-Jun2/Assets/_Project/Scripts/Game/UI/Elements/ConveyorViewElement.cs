using MessagePipe;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public sealed class ConveyorViewElement : ElementBase
{
    [Inject] private Camera _camera;
    [Inject] private SceneModule _sceneModule;
    [Inject] private ConveyorConfig _config;

    [Inject] private ISubscriber<ConveyorSlotAddBlockMessage> _conveyorSlotAddBlockSub;
    [Inject] private ISubscriber<ConveyorSlotRemoveBlockMessage> _conveyorSlotRemoveBlockSub;
    [Inject] private ISubscriber<SlotCreateCompleteMessage> _slotCreateCompleteSub;
    [Inject] private ISubscriber<LevelBuildCompleteMessage> _levelBuildCompleteSub;

    private Image _red;
    private Color _originalColor;
    private Color _targetColor;

    private void Awake()
    {
        _red ??= GetImage(ImageRole.Red);
        _originalColor = _red.color;
        _red.color = Color.clear;
    }

    private void Update()
    {
        var deltaTime = Time.deltaTime;
        var color = Color.Lerp(_red.color, _targetColor, deltaTime * _config.ColorLerpModifier);
        _red.color = color;
    }

    public override void Setup()
    {
        base.Setup();

        _conveyorSlotAddBlockSub.Subscribe(_ => UpdateView());
        _conveyorSlotRemoveBlockSub.Subscribe(_ => UpdateView());
        _slotCreateCompleteSub.Subscribe(_ => UpdateView());
        _levelBuildCompleteSub.Subscribe(_ => UpdateView());
    }

    private void UpdateView()
    {
        var resolver = _sceneModule.Scope.Container;

        var conveyor = resolver.Resolve<Conveyor>();
        var occupiedSlotCount = conveyor.GetOccupiedSlotCount(withoutExtra: false);
        var totalSlotCount = conveyor.GetTotalSlotCount();
        var occupiedCount = occupiedSlotCount / conveyor.GroupSlotCount;
        var totalCount = totalSlotCount / conveyor.GroupSlotCount;
        SetText(TextRole.Percent, $"{occupiedCount}/{totalCount}");
        var ratio01 = occupiedSlotCount / (float)totalSlotCount;
        var red = ratio01 >= _config.RedRatio;
        _targetColor = red ? _originalColor : Color.clear;

        var themeType = resolver.Resolve<ThemeType>();
        SetState(StateRole.Reset);
        var customState = themeType switch
        {
            ThemeType.Hard => StateRole.Hard,
            ThemeType.SuperHard => StateRole.SuperHard,
            _ => StateRole.None
        };
        if (customState != StateRole.None) SetState(customState);
    }
}
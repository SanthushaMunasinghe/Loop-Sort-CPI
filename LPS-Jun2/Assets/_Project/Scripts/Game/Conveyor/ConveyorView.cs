using System;
using MessagePipe;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public sealed class ConveyorView : WorldCanvasBase
{
    [Inject] private Camera _camera;
    [Inject] private Conveyor _conveyor;
    [Inject] private ConveyorConfig _config;

    [Inject] private ISubscriber<ConveyorSlotAddBlockMessage> _conveyorSlotAddBlockSub;
    [Inject] private ISubscriber<ConveyorSlotRemoveBlockMessage> _conveyorSlotRemoveBlockSub;
    [Inject] private ISubscriber<SlotCreateCompleteMessage> _slotCreateCompleteSub;

    private Image _background;
    private Color _originalColor;
    private Color _targetColor;

    protected override void Awake()
    {
        base.Awake();

        _background ??= GetImage(ImageRole.Background);
        _originalColor = _background.color;
    }

    private void Update()
    {
        var deltaTime = Time.deltaTime;
        var color = Color.Lerp(_background.color, _targetColor, deltaTime * _config.ColorLerpModifier);
        _background.color = color;
    }

    public override void OnRent()
    {
        base.OnRent();

        _targetColor = _originalColor;
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _conveyorSlotAddBlockSub.Subscribe(OnConveyorSlotAddBlock).AddTo(bag);
        _conveyorSlotRemoveBlockSub.Subscribe(OnConveyorSlotRemoveBlock).AddTo(bag);
        _slotCreateCompleteSub.Subscribe(OnSlotCreateComplete).AddTo(bag);
    }

    private void OnConveyorSlotAddBlock(ConveyorSlotAddBlockMessage m)
    {
        UpdateView();
    }

    private void OnConveyorSlotRemoveBlock(ConveyorSlotRemoveBlockMessage m)
    {
        UpdateView();
    }

    private void OnSlotCreateComplete(SlotCreateCompleteMessage m)
    {
        UpdateView();
    }

    private void UpdateView()
    {
        // var occupiedSlotCount = _conveyor.GetOccupiedSlotCount();
        // var totalSlotCount = _conveyor.GetTotalSlotCount();
        // var occupiedCount = occupiedSlotCount / _config.BlockGroupSlotCount;
        // var totalCount = totalSlotCount / _config.BlockGroupSlotCount;
        // SetText(TextRole.Percent, $"{occupiedCount}/{totalCount}");
        // var ratio01 = occupiedSlotCount / (float)totalSlotCount;
        // var red = ratio01 >= _config.RedRatio;
        // _targetColor = red ? Color.red : _originalColor;
    }
}
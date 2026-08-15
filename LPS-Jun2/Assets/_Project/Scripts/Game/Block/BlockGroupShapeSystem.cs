using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using MessagePipe;
using StatefulUI.Runtime.Core;
using StatefulUISupport.Scripts.Components;
using UnityEngine;
using VContainer;

public sealed class BlockGroupShapeSystem : SystemBase
{
    [Inject] private ISubscriber<CarrierBlockGroupUpdateMessage> _carrierBlockGroupUpdateSub;
    [Inject] private ISubscriber<ColorBlindUpdateMessage> _colorBlindUpdateSub;

    [Inject] private BlockConfig _blockConfig;
    [Inject] private PrefabModule _prefabModule;
    [Inject] private Colors _colors;
    [Inject] private SettingsModule _settingsModule;
    [Inject] private Camera _camera;

    private Canvas _canvas;

    private readonly Dictionary<MeshRenderer, StatefulComponent> _viewByGroup = new();
    private readonly Dictionary<StatefulComponent, CompositeMotionHandle> _motionsByView = new();

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        base.BuildMessages(bag);

        _carrierBlockGroupUpdateSub.Subscribe(OnCarrierBlockGroupUpdate).AddTo(bag);
        _colorBlindUpdateSub.Subscribe(OnColorBlindUpdate).AddTo(bag);
    }

    public override void OnAwake()
    {
        base.OnAwake();

        CreateCanvas();
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        if (!_canvas.enabled) return;

        foreach (var (groupBlock, view) in _viewByGroup)
        {
            var viewT = view.transform;
            viewT.position = groupBlock.transform.position + Vector3.up * .76f;
            viewT.eulerAngles = viewT.eulerAngles.WithY(_camera.transform.eulerAngles.y);
        }
    }

    private void OnCarrierBlockGroupUpdate(CarrierBlockGroupUpdateMessage m)
    {
        foreach (var groupBlock in m.Carrier.GroupBlocks)
        {
            if (groupBlock.gameObject.activeSelf)
                CreateShape(m.Carrier, groupBlock);
            else
                RemoveShape(m.Carrier, groupBlock);
        }
    }

    private void OnColorBlindUpdate(ColorBlindUpdateMessage m)
    {
        _canvas.enabled = m.Active;
    }

    private void CreateCanvas()
    {
        var instance = new GameObject("Block Group Shapes");
        _canvas = instance.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = -1;
        _canvas.enabled = _settingsModule.IsColorBlindActive();
    }

    private void CreateShape(Carrier carrier, MeshRenderer groupBlock)
    {
        if (_viewByGroup.ContainsKey(groupBlock)) return;
        var colorType = _colors.FindColorType(groupBlock.sharedMaterial);
        if (colorType.Base == BaseColor.None) return;

        var parent = _canvas.transform;
        var instance = _prefabModule.Rent(_blockConfig.BlockGroupShapePrefab, parent);
        var instanceT = instance.transform;
        instanceT.rotation = carrier.transform.rotation;
        _viewByGroup[groupBlock] = instance;

        var data = _colors.Get(colorType);
        var image = instance.GetImage(ImageRole.Icon);
        image.sprite = data.Sprite;
        image.color = data.Color;

        var motions = CancelAndGetMotions(instance);
        motions.Add(LMotion.Create(Vector3H.AlmostZero, Vector3.one, .2f)
            .WithEase(Ease.OutBack)
            .BindToLocalScale(instanceT)
            .AddTo(carrier));
        motions.Add(LMotion.Create(0f, data.Color.a, .2f)
            .BindToColorA(image)
            .AddTo(carrier));
    }

    private async UniTaskVoid RemoveShape(Carrier carrier, MeshRenderer groupBlock)
    {
        if (!_viewByGroup.TryGetValue(groupBlock, out var instance)) return;
        var image = instance.GetImage(ImageRole.Icon);

        var motions = CancelAndGetMotions(instance);
        motions.Add(LMotion.Create(Vector3.one, Vector3H.AlmostZero, .1f)
            .BindToLocalScale(instance.transform)
            .AddTo(carrier));
        motions.Add(LMotion.Create(image.color.a, 0f, .1f)
            .BindToColorA(image)
            .AddTo(carrier));
        await UniTask.Delay(100, cancellationToken: SceneLoadToken);

        _prefabModule.Return(instance.gameObject);
        _viewByGroup.Remove(groupBlock);
    }

    private CompositeMotionHandle CancelAndGetMotions(StatefulComponent view)
    {
        _motionsByView.TryGetValue(view, out var motions);
        motions ??= new CompositeMotionHandle();
        motions.Cancel();
        _motionsByView[view] = motions;
        return motions;
    }
}
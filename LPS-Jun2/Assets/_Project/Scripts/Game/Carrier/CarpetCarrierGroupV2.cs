using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

public sealed class CarpetCarrierGroupV2 : CarpetCarrierGroup
{
    [SerializeField] private Transform Model;
    [SerializeField] private Transform Left;
    [SerializeField] private Transform Right;
    [SerializeField] private float WidthOffset;
    [SerializeField] private MeshRenderer Carpet;
    [SerializeField] private float ForwardOffset;
    [SerializeField] private float BackOffset;
    [SerializeField] private GameObject DividerPrefab;
    [SerializeField] private Transform Roll;

    private float _previousCarrierCount;

    private readonly List<Carrier> _carriers = new();
    private readonly List<GameObject> _dividers = new();

    private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

    public override void OnReturn()
    {
        base.OnReturn();

        _carriers.Clear();
        _dividers.Clear();
    }

    public override void Initialize()
    {
        base.Initialize();

        foreach (var carrier in _carriers)
        {
            carrier.DisableInteraction(gameObject);
            carrier.DisableTransfer(gameObject);
        }

        _carriers.Sort((left, right) =>
        {
            var leftT = left.Pivot;
            var rightT = right.Pivot;
            var leftTotal = leftT.position.x + leftT.position.z;
            var rightTotal = rightT.position.x + rightT.position.z;
            return leftTotal.CompareTo(rightTotal);
        });

        CreateModel();
    }

    public override void AddCarrier(Carrier carrier)
    {
        base.AddCarrier(carrier);

        _carriers.Add(carrier);
    }

    public override void RevealNext()
    {
        base.RevealNext();

        if (_carriers.Count == 0) return;
        var nextCarrier = _carriers[^1];
        nextCarrier.EnableInteraction(gameObject);
        nextCarrier.EnableTransfer(gameObject);
        _carriers.RemoveAt(_carriers.Count - 1);

        var firstTarget = _carriers.Count == 0 ? nextCarrier : _carriers[0];
        var lastTarget = _carriers.Count == 0 ? nextCarrier : _carriers[^1];
        UpdateModel(firstTarget, lastTarget);
    }

    private void CreateModel()
    {
        var firstTarget = _carriers[0];
        var lastTarget = _carriers[^1];
        var firstTargetT = firstTarget.Pivot;
        var lastTargetT = lastTarget.Pivot;

        var isFacingForward = Vector3.Angle(firstTargetT.forward, Vector3.forward) < 90f;

        var translateOffset = isFacingForward ? ForwardOffset : BackOffset;
        var centerPoint = (firstTargetT.position + lastTargetT.position) / 2f;
        centerPoint += firstTargetT.forward * translateOffset;
        transform.position = centerPoint;

        var targetDistance = Vector3.Distance(firstTargetT.position, lastTargetT.position);
        var width = targetDistance + WidthOffset;
        var widthScale = new Vector3(width, 1f, 1f);
        Model.localScale = widthScale;

        transform.rotation = firstTargetT.rotation;
        var rotateOffset = isFacingForward ? 0f : 180f;
        transform.Rotate(Vector3.up, rotateOffset);

        var carrierCount = _carriers.Count;
        _previousCarrierCount = carrierCount;
        PropertyBlock.Clear();
        PropertyBlock.SetVector(BaseMapSt, new Vector4(carrierCount, 1f, 0f, 0f));
        Carpet.SetPropertyBlock(PropertyBlock);

        var dividerCount = carrierCount - 1;
        var dividerOffset = width / carrierCount;
        var dividerDirection = firstTargetT.right;
        dividerDirection *= Vector3.Angle(Vector3.forward, firstTargetT.forward) < 90f ? 1f : -1f;
        var dividerStartPoint = transform.position - dividerDirection * (width / 2f);
        dividerStartPoint += dividerDirection * dividerOffset;
        for (var i = 0; i < dividerCount; i++)
        {
            var instance = PrefabModule.Rent(DividerPrefab);
            var instanceT = instance.transform;
            instanceT.position = dividerStartPoint + dividerDirection * (dividerOffset * i);
            instanceT.rotation = firstTargetT.rotation;
            instanceT.GetChild(0).localScale = Vector3.one;
            _dividers.Add(instance);
        }

        var rightTargetPosition = lastTargetT.position + lastTargetT.forward * translateOffset;
        Right.position = rightTargetPosition;

        var leftTargetPosition = firstTargetT.position + firstTargetT.forward * translateOffset;
        Left.parent = transform;
        Left.position = leftTargetPosition;

        Roll.localScale = Vector3.one;
        Left.localScale = Vector3.one;
    }

    private void UpdateModel(Carrier firstTarget, Carrier lastTarget)
    {
        const float duration = .5f;

        var firstTargetT = firstTarget.Pivot;
        var lastTargetT = lastTarget.Pivot;

        var isFacingForward = Vector3.Angle(firstTargetT.forward, Vector3.forward) < 90f;
        var completed = _carriers.Count == 0;

        var translateOffset = isFacingForward ? ForwardOffset : BackOffset;
        var centerPoint = (firstTargetT.position + lastTargetT.position) / 2f;
        centerPoint += firstTargetT.forward * translateOffset;
        LMotion.Create(transform.position, centerPoint, duration)
            .BindToPosition(transform)
            .AddTo(this);

        var targetDistance = Vector3.Distance(firstTargetT.position, lastTargetT.position);
        var width = completed ? 0 : targetDistance + WidthOffset;
        var widthScale = new Vector3(width, 1f, 1f);
        LMotion.Create(Model.localScale, widthScale, duration)
            .BindToLocalScale(Model)
            .AddTo(this);

        LMotion.Create(_previousCarrierCount, _carriers.Count, duration)
            .Bind(x =>
            {
                PropertyBlock.Clear();
                PropertyBlock.SetVector(BaseMapSt, new Vector4(x, 1f, 0f, 0f));
                Carpet.SetPropertyBlock(PropertyBlock);
            })
            .AddTo(this);
        _previousCarrierCount = _carriers.Count;

        RemoveDividers();

        var rightTargetPosition = lastTargetT.position + lastTargetT.forward * translateOffset;
        LMotion.Create(Right.position, rightTargetPosition, duration)
            .BindToPosition(Right)
            .AddTo(this);

        if (completed)
            Left.parent = Model;
        else
        {
            var leftTargetPosition = firstTargetT.position + firstTargetT.forward * translateOffset;
            LMotion.Create(Left.position, leftTargetPosition, duration)
                .BindToPosition(Left)
                .AddTo(this);
        }

        LMotion.Create(0f, 360f, duration)
            .BindToLocalEulerAnglesZ(Roll)
            .AddTo(this);

        if (completed)
            ApplyRemoveRollMotion(duration);
    }

    private void RemoveDividers()
    {
        var removeDividerCount = _dividers.Count + 1 - _carriers.Count;
        for (var i = 0; i < removeDividerCount; i++)
        {
            var idx = _dividers.Count - 1;
            if (0 > idx) continue;
            var removeInstance = _dividers[idx];
            _dividers.RemoveAt(idx);
            ApplyRemoveDividerMotion(removeInstance);
        }
    }

    private async UniTaskVoid ApplyRemoveDividerMotion(GameObject divider)
    {
        var t = divider.transform.GetChild(0);
        await LMotion.Create(Vector3.one, Vector3.zero, .25f)
            .WithEase(Ease.InBack)
            .BindToLocalScale(t)
            .AddTo(this);
        PrefabModule.Return(divider);
    }

    private void ApplyRemoveRollMotion(float delay)
    {
        var t = Roll;
        LMotion.Create(Vector3.one, Vector3.zero, .25f)
            .WithDelay(delay)
            .WithEase(Ease.InBack)
            .BindToLocalScale(t)
            .AddTo(this);
    }
}
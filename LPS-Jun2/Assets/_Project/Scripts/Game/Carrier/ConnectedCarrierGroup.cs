using System.Collections.Generic;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

public sealed class ConnectedCarrierGroup : GameBehaviourBase
{
    [SerializeField] private Transform Model;
    [SerializeField] private Transform Left;
    [SerializeField] private Transform Right;
    [SerializeField] private float WidthOffset;
    [SerializeField] private MeshRenderer TileModel;

    private readonly List<Carrier> _carriers = new();

    private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

    public override void OnReturn()
    {
        base.OnReturn();

        _carriers.Clear();
    }

    public void Initialize()
    {
        transform.localScale = Vector3.one;

        foreach (var carrier in _carriers)
        {
            carrier.DisableInteraction(gameObject);
        }

        _carriers.Sort((left, right) =>
        {
            var leftT = left.Pivot;
            var rightT = right.Pivot;
            var leftTotal = leftT.position.x + leftT.position.z;
            var rightTotal = rightT.position.x + rightT.position.z;
            return leftTotal.CompareTo(rightTotal);
        });

        var firstTarget = _carriers[0];
        var lastTarget = _carriers[^1];
        var firstTargetT = firstTarget.Pivot;
        var lastTargetT = lastTarget.Pivot;
        var direction = (lastTargetT.position - firstTargetT.position).normalized;
        var dot = Vector3.Dot(direction, firstTargetT.right);

        var b = dot > 0;
        var leftTarget = _carriers[b ? 0 : ^1];
        var rightTarget = _carriers[b ? ^1 : 0];
        var leftTargetT = leftTarget.Pivot;
        var rightTargetT = rightTarget.Pivot;

        var centerPoint = (leftTargetT.position + rightTargetT.position) / 2f;
        transform.position = centerPoint;
        transform.rotation = leftTargetT.rotation;

        var width = Vector3.Distance(leftTargetT.position, rightTargetT.position) + WidthOffset;
        Model.localScale = new Vector3(width, 1f, 1f);

        Left.position = leftTarget.Pivot.position;
        Right.position = rightTarget.Pivot.position;
        Left.rotation = leftTarget.Pivot.rotation;
        Right.rotation = rightTarget.Pivot.rotation;

        if (TileModel != null)
        {
            var carrierCount = _carriers.Count;
            PropertyBlock.Clear();
            PropertyBlock.SetVector(BaseMapSt, new Vector4(1f, carrierCount + 1f, 0f, 0f));
            TileModel.SetPropertyBlock(PropertyBlock);
        }
    }

    public void AddCarrier(Carrier carrier)
    {
        _carriers.Add(carrier);
    }

    public void Complete()
    {
        LMotion.Create(1f, 0f, .25f)
            .WithOnComplete(() => gameObject.SetActive(false))
            .BindToLocalScaleY(transform)
            .AddTo(this)
            .ToUniTask(SceneLoadToken);

        foreach (var carrier in _carriers)
        {
            carrier.EnableInteraction(gameObject);
        }
    }

    public void OnLevelAnimationStart()
    {
        transform.localScale = Vector3.zero;
    }

    public void OnLevelAnimationEnd()
    {
        transform.localScale = Vector3.one.WithY(0f);
        LMotion.Create(0f, 1f, .25f)
            .BindToLocalScaleY(transform)
            .AddTo(this)
            .ToUniTask(SceneLoadToken);
    }
}
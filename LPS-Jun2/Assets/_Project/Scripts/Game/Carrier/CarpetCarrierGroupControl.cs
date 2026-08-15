using System.Collections.Generic;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

public sealed class CarpetCarrierGroupControl : CarpetCarrierGroup
{
    [SerializeField] private Transform Model;
    [SerializeField] private Transform Left;
    [SerializeField] private Transform Right;
    [SerializeField] private float WidthOffset;

    private Vector3? _modelMoveDirection;

    private readonly List<Carrier> _carriers = new();

    public override void OnReturn()
    {
        base.OnReturn();

        _carriers.Clear();
        _modelMoveDirection = null;
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

        UpdateModel(withMotion: false);
    }

    private void UpdateModel(bool withMotion)
    {
        var duration = withMotion ? .3f : 0f;

        if (_carriers.Count == 0)
        {
            var movePosition = Left.position + _modelMoveDirection.GetValueOrDefault() * 1f;
            LMotion.Create(transform.position, movePosition, duration)
                .BindToPosition(transform)
                .AddTo(this);
            LMotion.Create(Model.localScale, new Vector3(0f, 1f, 1f), duration)
                .BindToLocalScale(Model)
                .AddTo(this);
            LMotion.Create(Left.localScale, new Vector3(0f, 1f, 1f), duration)
                .BindToLocalScale(Left)
                .AddTo(this);
            LMotion.Create(Left.position, movePosition, duration)
                .BindToPosition(Left)
                .AddTo(this);
            LMotion.Create(Right.localScale, new Vector3(0f, 1f, 1f), duration)
                .BindToLocalScale(Right)
                .AddTo(this);
            LMotion.Create(Right.position, movePosition, duration)
                .WithOnComplete(() => gameObject.SetActive(false))
                .BindToPosition(Right)
                .AddTo(this);

            return;
        }

        var firstTarget = _carriers[0];
        var lastTarget = _carriers[^1];
        var firstTargetT = firstTarget.Pivot;
        var lastTargetT = lastTarget.Pivot;
        var direction = (lastTargetT.position - firstTargetT.position).normalized;
        var dot = Vector3.Dot(direction, firstTargetT.right);

        _modelMoveDirection ??= (firstTargetT.position - lastTargetT.position).normalized;

        var b = dot > 0;
        var leftTarget = _carriers[b ? 0 : ^1];
        var rightTarget = _carriers[b ? ^1 : 0];
        var leftTargetT = leftTarget.Pivot;
        var rightTargetT = rightTarget.Pivot;

        var centerPoint = (leftTargetT.position + rightTargetT.position) / 2f;
        LMotion.Create(transform.position, centerPoint, duration)
            .BindToPosition(transform)
            .AddTo(this);
        transform.rotation = leftTargetT.rotation;

        var width = Vector3.Distance(leftTargetT.position, rightTargetT.position) + WidthOffset;
        var widthScale = new Vector3(width, 1f, 1f);
        LMotion.Create(Model.localScale, widthScale, duration)
            .BindToLocalScale(Model)
            .AddTo(this);

        LMotion.Create(Left.position, leftTarget.Pivot.position, duration)
            .BindToPosition(Left)
            .AddTo(this);
        LMotion.Create(Right.position, rightTarget.Pivot.position, duration)
            .BindToPosition(Right)
            .AddTo(this);
        Left.rotation = leftTarget.Pivot.rotation;
        Right.rotation = rightTarget.Pivot.rotation;
        Left.localScale = Vector3.one;
        Right.localScale = Vector3.one;
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
        UpdateModel(withMotion: true);
    }

    public override void OnLevelAnimationStart()
    {
        base.OnLevelAnimationStart();

        transform.localScale = Vector3.zero;
    }

    public override void OnLevelAnimationEnd()
    {
        base.OnLevelAnimationEnd();

        transform.localScale = new Vector3(0f, 1f, .1f);
        LMotion.Create(0f, 1f, .25f)
            .BindToLocalScaleX(transform)
            .AddTo(this)
            .ToUniTask(SceneLoadToken);
        LMotion.Create(.1f, 1f, .25f)
            .WithDelay(.25f)
            .BindToLocalScaleZ(transform)
            .AddTo(this)
            .ToUniTask(SceneLoadToken);
    }
}
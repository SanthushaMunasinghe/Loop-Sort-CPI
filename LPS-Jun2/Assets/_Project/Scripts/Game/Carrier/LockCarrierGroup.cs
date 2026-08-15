using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

public sealed class LockCarrierGroup : GameBehaviourBase
{
    [SerializeField] private Transform Lock;
    [SerializeField] private Transform UnlockPoint;
    [SerializeField] private float ForwardOffset;

    [Header("Skinned Mesh")]
    [SerializeField] private SkinnedMeshRenderer FrontDoor;
    [SerializeField] private SkinnedMeshRenderer TopDoor;

    [Header("Modular")]
    [SerializeField] private Transform Left;
    [SerializeField] private Transform Right;
    [SerializeField] private Transform Center;
    [SerializeField] private float CenterScaleOffset;
    [SerializeField] private Transform Door;
    [SerializeField] private float DoorScaleOffset;

    private ColorType _colorType;

    private readonly List<Carrier> _carriers = new();

    public override void OnReturn()
    {
        base.OnReturn();

        _colorType = default;
        _carriers.Clear();
        transform.localScale = Vector3.one;
        Lock.localScale = Vector3.one;

        TopDoor.SetBlendShapeWeight(0, 0);
        for (var i = 0; i < 7; i++) FrontDoor.SetBlendShapeWeight(i, 0);
    }

    public void Initialize()
    {
        InjectColorType(_colorType);

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

        var width = Vector3.Distance(leftTargetT.position, rightTargetT.position);
        Center.localScale = new Vector3(width + CenterScaleOffset, 1f, 1f);
        Door.localScale = new Vector3(width + DoorScaleOffset, 1f, 1f);

        Left.position = leftTarget.Pivot.position;
        Right.position = rightTarget.Pivot.position;

        transform.position += transform.forward * ForwardOffset;

        var rotateOffset = Vector3.Angle(leftTargetT.forward, Vector3.forward) < 91f ? 0f : 180f;
        transform.Rotate(Vector3.up, rotateOffset);
    }

    public void SetColorType(ColorType colorType)
    {
        _colorType = colorType;
    }

    public void AddCarrier(Carrier carrier)
    {
        _carriers.Add(carrier);
    }

    public async UniTaskVoid Unlock(KeyBlockGroup keyBlockGroup)
    {
        var keyT = keyBlockGroup.Model.transform;
        {
            var fromPosition = keyT.position;
            var toPosition = UnlockPoint.position;
            var fromRotation = keyT.rotation;
            var toRotation = UnlockPoint.rotation;
            var fromScale = keyT.localScale;
            var toScale = Vector3.one * 1.83f;
            await LMotion.Create(0f, 1f, .7f)
                .Bind(t =>
                {
                    var lerpPosition = Vector3.Lerp(fromPosition, toPosition, t);
                    lerpPosition.y += 5f * Mathf.PI * t * Mathf.Sin(t * Mathf.PI);
                    keyT.position = lerpPosition;

                    var lerpRotation = Quaternion.Lerp(fromRotation, toRotation, t);
                    keyT.rotation = lerpRotation;

                    var lerpScale = Vector3.Lerp(fromScale, toScale, t);
                    keyT.localScale = lerpScale;
                })
                .AddTo(this);
        }
        {
            var fromRotation = keyT.rotation;
            var toRotation = UnlockPoint.rotation * Quaternion.AngleAxis(90f, Vector3.up);
            LMotion.Create(fromRotation, toRotation, .2f)
                .WithDelay(.1f)
                .WithEase(Ease.OutBack)
                .BindToRotation(keyT)
                .AddTo(this);
        }
        await UniTask.Delay(300, cancellationToken: SceneLoadToken);
        {
            var fromScale = keyT.localScale;
            var toScale = Vector3H.AlmostZero;
            await LMotion.Create(fromScale, toScale, .1f)
                .BindToLocalScale(keyT)
                .AddTo(this);
        }
        {
            await LMotion.Create(Vector3.one, Vector3H.AlmostZero, .1f)
                .BindToLocalScale(Lock)
                .AddTo(this);
        }
        {
            for (var i = 7 - 1; i >= 0; i--)
            {
                await LMotion.Create(0f, 100f, .04f)
                    .BindToBlendShape(FrontDoor, i)
                    .AddTo(this);
            }
            await LMotion.Create(0f, 100f, .45f)
                .BindToBlendShape(TopDoor, 0)
                .AddTo(this);
        }
        {
            await LMotion.Create(1f, 0f, .25f)
                .BindToLocalScaleY(transform)
                .AddTo(this)
                .ToUniTask(SceneLoadToken);
        }

        gameObject.SetActive(false);
        keyBlockGroup.gameObject.SetActive(false);

        foreach (var carrier in _carriers)
        {
            carrier.EnableInteraction(gameObject);
            carrier.EnableTransfer(gameObject);
        }
    }

    public void OnLevelAnimationStart()
    {
        Lock.localScale = Vector3.zero;
        for (var i = 0; i < 7; i++)
        {
            FrontDoor.SetBlendShapeWeight(i, 100f);
        }
        TopDoor.SetBlendShapeWeight(0, 100f);
        transform.localScale = Vector3.zero;
    }

    public async UniTaskVoid OnLevelAnimationEnd()
    {
        transform.localScale = Vector3.one.WithY(0f);
        await LMotion.Create(0f, 1f, .25f)
            .BindToLocalScaleY(transform)
            .AddTo(this)
            .ToUniTask(SceneLoadToken);
        await LMotion.Create(100f, 0f, .45f)
            .BindToBlendShape(TopDoor, 0)
            .AddTo(this)
            .ToUniTask(SceneLoadToken);
        for (var i = 0; i < 7; i++)
        {
            await LMotion.Create(100f, 0f, .04f)
                .BindToBlendShape(FrontDoor, i)
                .AddTo(this)
                .ToUniTask(SceneLoadToken);
        }
        await LMotion.Create(Vector3H.AlmostZero, Vector3.one, .1f)
            .BindToLocalScale(Lock)
            .AddTo(this);
    }
}
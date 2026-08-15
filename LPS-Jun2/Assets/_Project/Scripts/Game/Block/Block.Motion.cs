using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;

public partial class Block
{
    private MotionHandle _localMoveMotion;

    public UniTask ApplyMoveToCarrierMotion(Vector3 position, Quaternion rotation)
    {
        if (_localMoveMotion.IsActive()) _localMoveMotion.Cancel();

        var fromPosition = transform.localPosition;
        var toPosition = position;
        var fromRotation = transform.localRotation;
        var toRotation = rotation;
        _localMoveMotion = ApplyJumpMotion(fromPosition, toPosition, fromRotation, toRotation);

        return _localMoveMotion.ToUniTask(cancellationToken: SceneLoadToken);
    }

    public UniTask ApplyMoveToSlotMotion(Vector3 position, Quaternion rotation)
    {
        if (_localMoveMotion.IsActive()) _localMoveMotion.Cancel();

        var fromPosition = transform.localPosition;
        var toPosition = position;
        var fromRotation = transform.localRotation;
        var toRotation = rotation;
        _localMoveMotion = ApplyJumpMotion(fromPosition, toPosition, fromRotation, toRotation);

        return _localMoveMotion.ToUniTask(cancellationToken: SceneLoadToken);
    }

    private MotionHandle ApplyJumpMotion(Vector3 fromPosition, Vector3 toPosition, Quaternion fromRotation, Quaternion toRotation)
    {
        return LMotion.Create(0f, 1f, .7f * MotionDurationMultiplier)
            .WithEase(Ease.InOutSine)
            .WithScheduler(MotionScheduler.FixedUpdate)
            .Bind(t =>
            {
                var lerpPosition = Vector3.Lerp(fromPosition, toPosition, t);
                var height = 2f * MotionDurationMultiplier;
                lerpPosition.y += height * Mathf.PI * EaseUtility.OutQuint(t) * Mathf.Sin(Mathf.PI * t);
                transform.localPosition = lerpPosition;

                var lerpRotation = Quaternion.Lerp(fromRotation, toRotation, t);
                transform.localRotation = lerpRotation;
            })
            .AddTo(this);
    }
}
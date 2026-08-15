using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

public sealed class HandMonitor : MonitorBase
{
    private readonly CompositeMotionHandle _handMotions = new();
    private Vector3 _lerpMousePosition;

    private void Awake()
    {
        var root = GetObject(ObjectRole.Root);
        root.SetActive(false);
    }

    public override void Render()
    {
        base.Render();

        var root = GetObject(ObjectRole.Root);

        if (InputH.GetKeyDown(KeyCode.Space))
        {
            root.SetActive(!root.activeSelf);
            if (root.activeSelf) _lerpMousePosition = Input.mousePosition;
        }

        if (!root.activeSelf) return;

        var deltaTime = Time.unscaledDeltaTime;
        _lerpMousePosition = Vector3.Lerp(_lerpMousePosition, Input.mousePosition, deltaTime * 10f);
        var rootT = root.transform;
        var handT = GetImage(ImageRole.Hand).transform;
        ScreenToCanvasSpace(rootT, _lerpMousePosition);

        if (Input.GetMouseButtonDown(0))
        {
            _handMotions.Cancel();
            var targetRotation = Quaternion.AngleAxis(12.5f, Vector3.forward);

            LMotion.Create(rootT.localScale, Vector3.one * .85f, .1f)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToLocalScale(rootT)
                .AddTo(_handMotions);
            LMotion.Create(handT.localRotation, targetRotation, .1f)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToLocalRotation(handT)
                .AddTo(_handMotions);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _handMotions.Cancel();

            LMotion.Create(rootT.localScale, Vector3.one, .1f)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToLocalScale(rootT)
                .AddTo(_handMotions);
            LMotion.Create(handT.localRotation, Quaternion.identity, .1f)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToLocalRotation(handT)
                .AddTo(_handMotions);
        }
    }
}
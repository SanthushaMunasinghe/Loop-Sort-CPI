using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using StatefulUISupport.Scripts.Components;
using UnityEngine;

public partial class Carrier
{
    private readonly CompositeMotionHandle _backMotions = new();
    private readonly CompositeMotionHandle _addBlockMotions = new();
    private readonly CompositeMotionHandle _transferMotions = new();

    public async UniTaskVoid ApplyOpenBackMotion(bool immediate = false)
    {
        if (BackRearRenderer == null || BackTopRenderer == null) return;
        _backMotions.Cancel();

        if (immediate)
        {
            BackTopRenderer.gameObject.SetActive(false);
            BackRearRenderer.gameObject.SetActive(false);
            return;
        }

        BackRearRenderer.gameObject.SetActive(true);
        BackTopRenderer.gameObject.SetActive(true);
        BackRearRenderer.SetBlendShapeWeight(0, 100f);
        BackRearRenderer.SetBlendShapeWeight(2, 100f);
        BackTopRenderer.SetBlendShapeWeight(0, 100f);
        {
            LMotion.Create(100f, 0f, .3f)
                .BindToBlendShape(BackRearRenderer, 0)
                .AddTo(this)
                .AddTo(_backMotions)
                .ToUniTask(ReturnToken);
            await LMotion.Create(0f, 100f, .2f)
                .BindToBlendShape(BackRearRenderer, 2)
                .AddTo(this)
                .AddTo(_backMotions)
                .ToUniTask(ReturnToken);
            await LMotion.Create(100f, 0f, .05f)
                .BindToBlendShape(BackRearRenderer, 2)
                .AddTo(this)
                .AddTo(_backMotions)
                .ToUniTask(ReturnToken);
        }
        {
            await LMotion.Create(100f, 0f, .6f)
                .BindToBlendShape(BackTopRenderer, 0)
                .AddTo(this)
                .AddTo(_backMotions)
                .ToUniTask(ReturnToken);
        }

        BackRearRenderer.gameObject.SetActive(false);
        BackTopRenderer.gameObject.SetActive(false);
    }

    public async UniTaskVoid ApplyCloseBackMotion(bool immediate = false)
    {
        if (BackRearRenderer == null || BackTopRenderer == null) return;
        _backMotions.Cancel();

        if (immediate)
        {
            BackTopRenderer.gameObject.SetActive(true);
            BackRearRenderer.gameObject.SetActive(true);
            BackTopRenderer.SetBlendShapeWeight(0, 100f);
            BackRearRenderer.SetBlendShapeWeight(0, 100f);
            return;
        }

        {
            BackRearRenderer.gameObject.SetActive(false);
            BackTopRenderer.SetBlendShapeWeight(0, 0f);
            BackTopRenderer.gameObject.SetActive(true);
            await LMotion.Create(0f, 100f, .6f)
                .BindToBlendShape(BackTopRenderer, 0)
                .AddTo(this)
                .AddTo(_backMotions)
                .ToUniTask(ReturnToken);
        }
        {
            BackRearRenderer.SetBlendShapeWeight(0, 0f);
            BackRearRenderer.SetBlendShapeWeight(2, 0f);
            BackRearRenderer.gameObject.SetActive(true);
            await LMotion.Create(0, 100f, .05f)
                .BindToBlendShape(BackRearRenderer, 2)
                .AddTo(this)
                .AddTo(_backMotions)
                .ToUniTask(ReturnToken);
            LMotion.Create(100f, 0f, .3f)
                .BindToBlendShape(BackRearRenderer, 2)
                .AddTo(this)
                .AddTo(_backMotions)
                .ToUniTask(ReturnToken);
            LMotion.Create(0f, 100f, .2f)
                .BindToBlendShape(BackRearRenderer, 0)
                .AddTo(this)
                .AddTo(_backMotions)
                .ToUniTask(ReturnToken);
        }
    }

    private void ApplyAddBlockMotion()
    {
        // _addBlockMotions.Complete();
        //
        // var t = transform;
        // var from = t.localScale;
        // var to = new Vector3(1f, .97f, 1f);
        //
        // LMotion.Create(from, to, .05f)
        //     .BindToLocalScale(t)
        //     .AddTo(this)
        //     .AddTo(_addBlockMotions);
        //
        // LMotion.Create(to, Vector3.one, .05f)
        //     .WithDelay(.05f)
        //     .BindToLocalScale(t)
        //     .AddTo(this)
        //     .AddTo(_addBlockMotions);
    }

    private void ApplyBeginTransferMotion()
    {
        if (_isTransferMotionDisabled) return;
        _transferMotions.Cancel();
        // {
        //     var t = HeadRenderer.transform;
        //     var from = t.localEulerAngles.WithZ(-8f);
        //     var to = t.localEulerAngles.WithZ(8f);
        //
        //     LMotion.Create(from, to, .3f)
        //         .WithLoops(-1, LoopType.Yoyo)
        //         .BindToLocalEulerAngles(t)
        //         .AddTo(this)
        //         .AddTo(_transferMotions);
        // }
        {
            var t = transform;
            _originalPosition ??= t.position;
            var from = _originalPosition.GetValueOrDefault();
            var to = from + t.forward * -.1f;

            LMotion.Create(from, to, .2f)
                .BindToPosition(t)
                .AddTo(this)
                .AddTo(_transferMotions);
        }
    }

    private void ApplyEndTransferMotion()
    {
        if (_isTransferMotionDisabled) return;
        _transferMotions.Cancel();

        HeadRenderer.transform.localScale = Vector3.one;

        var t = transform;
        var from = t.position;
        var to = _originalPosition.GetValueOrDefault();

        LMotion.Create(from, to, .2f)
            .BindToPosition(t)
            .AddTo(this)
            .AddTo(_transferMotions);
    }

    private void ApplyCheckmarkMotion()
    {
        var checkmark = View.GetImage(ImageRole.Checkmark);
        checkmark.gameObject.SetActive(true);
        var t = checkmark.transform;
        LMotion.Create(Vector3.one * 2f, Vector3.one, .5f)
            .WithEase(Ease.InOutBack)
            .BindToLocalScale(t);
        LMotion.Create(10f, 0f, .5f)
            .WithEase(Ease.InOutBack)
            .BindToLocalEulerAnglesZ(t);
        t.localScale = Vector3.zero;
    }
}
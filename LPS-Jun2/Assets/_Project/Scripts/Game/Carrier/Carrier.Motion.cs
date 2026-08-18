using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using StatefulUISupport.Scripts.Components;
using UnityEngine;
using UnityEngine.Pool;

public partial class Carrier
{
    private readonly CompositeMotionHandle _backMotions = new();
    private readonly CompositeMotionHandle _addBlockMotions = new();
    private readonly CompositeMotionHandle _transferMotions = new();
    private readonly CompositeMotionHandle _groupSlideMotions = new();

    private readonly List<Vector3> _originalGroupContainerPositions = new();
    private readonly List<Vector3> _originalGroupBlockPositions = new();

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

    /// <summary>
    /// Snapshots every CarrierBlockGroupParent container's and GroupBlocks/GroupBlockFilters
    /// entry's pristine world position, so ApplyGroupSlideMotion always has an absolute baseline to
    /// slide from rather than compounding off wherever an object currently sits. Called on rent —
    /// before any transfer can happen — so it always captures the authored/generated layout.
    /// </summary>
    private void CaptureGroupSlideOriginals()
    {
        _originalGroupContainerPositions.Clear();
        _originalGroupBlockPositions.Clear();

        if (BlockParent == null) return;

        for (var i = 0; i < BlockParent.childCount; i++)
        {
            var child = BlockParent.GetChild(i);
            if (child.TryGetComponent<CarrierBlockGroupParent>(out _))
                _originalGroupContainerPositions.Add(child.position);
        }

        foreach (var groupBlock in GroupBlocks)
            _originalGroupBlockPositions.Add(groupBlock.transform.position);
    }

    /// <summary>Puts every group container and GroupBlocks entry back at its captured original spot.</summary>
    private void RestoreGroupSlidePositions()
    {
        _groupSlideMotions.Cancel();

        if (BlockParent == null) return;

        var containerIndex = 0;
        for (var i = 0; i < BlockParent.childCount; i++)
        {
            var child = BlockParent.GetChild(i);
            if (!child.TryGetComponent<CarrierBlockGroupParent>(out _)) continue;
            if (containerIndex < _originalGroupContainerPositions.Count)
                child.position = _originalGroupContainerPositions[containerIndex];
            containerIndex++;
        }

        for (var i = 0; i < GroupBlocks.Count && i < _originalGroupBlockPositions.Count; i++)
            GroupBlocks[i].transform.position = _originalGroupBlockPositions[i];

        _originalGroupContainerPositions.Clear();
        _originalGroupBlockPositions.Clear();
    }

    /// <summary>
    /// Start mode only. After a transfer batch finishes, counts how many trailing
    /// CarrierBlockGroupParent groups are now fully empty and, if any are, slides every remaining
    /// group's container and matching GroupBlocks/GroupBlockFilters visual to an absolute target
    /// computed off its captured original position — never off wherever it currently sits, so
    /// repeated slides don't compound. Blocks new transfers on this carrier for the duration, via a
    /// token distinct from carrier.gameObject (ShuffleBoosterSystem already uses that one). No-ops
    /// for carriers with no CarrierBlockGroupParent containers — anything not filled via the
    /// Sandbox's Start-mode Apply Carrier Modes.
    /// </summary>
    public async UniTaskVoid ApplyGroupSlideMotion()
    {
        if (Mode != CarrierMode.Start) return;
        if (BlockParent == null) return;
        if (_originalGroupContainerPositions.Count == 0) return;

        using var pooled = ListPool<Transform>.Get(out var containers);
        for (var i = 0; i < BlockParent.childCount; i++)
        {
            var child = BlockParent.GetChild(i);
            if (child.TryGetComponent<CarrierBlockGroupParent>(out _))
                containers.Add(child);
        }
        if (containers.Count == 0) return;

        var emptiedCount = 0;
        for (var i = containers.Count - 1; i >= 0; i--)
        {
            if (containers[i].childCount > 0) break;
            emptiedCount++;
        }
        if (emptiedCount == 0) return;
        if (GroupBlocks.Count < 2) return;

        var remainingCount = containers.Count - emptiedCount;
        if (remainingCount <= 0) return;

        var delta = GroupBlocks[1].transform.position - GroupBlocks[0].transform.position;
        if (delta == Vector3.zero) return;

        _groupSlideMotions.Cancel();
        DisableTransfer(BlockParent.gameObject);
        try
        {
            var duration = _sceneScope.GroupSlideDuration;
            using var pTasks = ListPool<UniTask>.Get(out var tasks);
            for (var i = 0; i < remainingCount; i++)
            {
                var containerT = containers[i];
                var containerTarget = _originalGroupContainerPositions[i] + delta * emptiedCount;
                tasks.Add(LMotion.Create(containerT.position, containerTarget, duration)
                    .BindToPosition(containerT)
                    .AddTo(this)
                    .AddTo(_groupSlideMotions)
                    .ToUniTask(ReturnToken));

                if (i < GroupBlocks.Count && i < _originalGroupBlockPositions.Count)
                {
                    var meshT = GroupBlocks[i].transform;
                    var meshTarget = _originalGroupBlockPositions[i] + delta * emptiedCount;
                    tasks.Add(LMotion.Create(meshT.position, meshTarget, duration)
                        .BindToPosition(meshT)
                        .AddTo(this)
                        .AddTo(_groupSlideMotions)
                        .ToUniTask(ReturnToken));
                }
            }

            await UniTask.WhenAll(tasks);
        }
        finally
        {
            EnableTransfer(BlockParent.gameObject);
        }
    }
}
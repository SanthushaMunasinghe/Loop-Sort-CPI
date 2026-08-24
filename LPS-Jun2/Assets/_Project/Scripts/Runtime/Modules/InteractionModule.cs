using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lean.Touch;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class InteractionModule : ModuleBase, IStartable, IDisposable
{
    [Inject] private Camera _camera;
    [Inject] private HapticModule _hapticModule;

    private IInteractable _currentInteractable;
    private IInteractable _restrictedInteractable;
    private ITouchInterceptor _touchInterceptor;
    private bool _isInteractionRestricted;
    private LayerMask _layerMask;
    private UniTaskCompletionSource _onClickAsync;

    private readonly HashSet<object> _blockers = new();

    public void Start()
    {
        Input.multiTouchEnabled = false;
        _layerMask = LayerMask.GetMask(LayerRefs.Default);
        LeanTouch.OnFingerDown += OnFingerDown;
        LeanTouch.OnFingerUpdate += OnFingerUpdate;
        LeanTouch.OnFingerUp += OnFingerUp;
    }

    public void Dispose()
    {
        LeanTouch.OnFingerDown -= OnFingerDown;
        LeanTouch.OnFingerUpdate -= OnFingerUpdate;
        LeanTouch.OnFingerUp -= OnFingerUp;
    }

    private void OnFingerDown(LeanFinger finger)
    {
        if (finger.IsOverGui) return;

        var screenPointToRay = _camera.ScreenPointToRay(finger.ScreenPosition);
        if (!Physics.Raycast(screenPointToRay, out var hitInfo, 1000f, _layerMask)) return;

        var interactable = hitInfo.transform.GetComponentInParent<IInteractable>(includeInactive: true);

        if (_touchInterceptor != null)
        {
            _touchInterceptor.Intercept(finger, hitInfo);
        }
        else
        {
            if (interactable == null) return;

            if (_isInteractionRestricted && interactable != _restrictedInteractable) return;

            if (interactable is IDragInteractable dragInteractable)
                if (!dragInteractable.CanStartInteract(finger, hitInfo))
                    return;

            if (interactable is ITouchInteractable touchInteractable)
                touchInteractable.Interact(finger, hitInfo);
        }

        _currentInteractable = interactable;
        _hapticModule.PlaySelection();
        _onClickAsync?.TrySetResult();
    }

    private void OnFingerUpdate(LeanFinger finger)
    {
        if (_currentInteractable is IDragInteractable dragInteractable)
        {
            dragInteractable.UpdateInteraction(finger);
        }
    }

    private void OnFingerUp(LeanFinger finger)
    {
        if (_currentInteractable is IDragInteractable dragInteractable)
        {
            dragInteractable.CompleteInteract(finger);
        }
        _currentInteractable = null;
    }

    public void EnableRestriction(object owner = null)
    {
        if (owner != null) _blockers.Add(owner);
        _isInteractionRestricted = true;
    }

    public void DisableRestriction(object owner = null)
    {
        if (owner != null) _blockers.Remove(owner);
        if (_blockers.Count > 0) return;
        _isInteractionRestricted = false;
    }

    public void SetRestrictedInteractable(IInteractable interactable)
    {
        _restrictedInteractable = interactable;
    }

    public void SetLayerMask(LayerMask layerMask)
    {
        _layerMask = layerMask;
    }

    public async UniTask OnInteractAsync(CancellationToken token = default)
    {
        _onClickAsync = new UniTaskCompletionSource();
        await _onClickAsync.Task.AttachExternalCancellation(token);
        _onClickAsync = null;
    }

    public void SetTouchInterceptor(ITouchInterceptor blocker)
    {
        _touchInterceptor = blocker;
    }

    public void ClearTouchInterceptor()
    {
        _touchInterceptor = null;
    }
}

public interface IInteractable
{
}

public interface ITouchInteractable : IInteractable
{
    public void Interact(LeanFinger finger, RaycastHit hitInfo);
}

public interface IDragInteractable : IInteractable
{
    public bool CanStartInteract(LeanFinger finger, RaycastHit hitInfo);
    public void UpdateInteraction(LeanFinger finger);
    public void CompleteInteract(LeanFinger finger);
}

public interface ITouchInterceptor
{
    public void Intercept(LeanFinger finger, RaycastHit hitInfo);
}
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class PointerHoldElement : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDeselectHandler
{
    private AutoResetUniTaskCompletionSource _completionSource;
    private Action _startListeners;
    private Action _updateListeners;

    private async UniTaskVoid HandleListeners()
    {
        var task = OnReleaseAsync();
        while (task.Status == UniTaskStatus.Pending)
        {
            _updateListeners?.Invoke();
            await UniTask.Yield();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _completionSource?.TrySetResult();
        _completionSource = AutoResetUniTaskCompletionSource.Create();
        _startListeners?.Invoke();
        HandleListeners();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _completionSource?.TrySetResult();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _completionSource?.TrySetResult();
    }

    public UniTask OnReleaseAsync()
    {
        return _completionSource.Task;
    }

    public void AddStartListener(Action listener)
    {
        _startListeners += listener;
    }

    public void AddUpdateListener(Action listener)
    {
        _updateListeners += listener;
    }
}
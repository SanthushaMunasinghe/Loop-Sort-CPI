using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using StatefulUISupport.Scripts.Components;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class Monitors : MonoBehaviour
{
    private readonly Dictionary<Type, MonitorBase> _monitorByType = new();
    private readonly Dictionary<IMonitor, CancellationTokenSource> _transitionCtsByMonitor = new();
    private readonly Dictionary<MonitorBase, int> _sortingOrderByMonitor = new();
    private readonly List<MonitorBase> _activeMonitors = new();
    private readonly HashSet<object> _draggingOwners = new();

    private CanvasGroup _canvasGroup;
    private EventSystem _eventSystem;
    private int _originalPixelDragThreshold;
    private int _highestSortingOrder = 1000;
    private bool _isAlphaVisible = true;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _eventSystem = EventSystem.current;
        _originalPixelDragThreshold = _eventSystem.pixelDragThreshold;
    }

    public void Register(MonitorBase monitor)
    {
        Deactivate(monitor);
        _monitorByType[monitor.GetType()] = monitor;
        _sortingOrderByMonitor[monitor] = monitor.GetComponent<Canvas>().sortingOrder;
    }

    private void SetActive(Type monitorType, ITransition transition = null)
    {
        var monitor = _monitorByType[monitorType];
        monitor.gameObject.SetActive(true);
        if (!_activeMonitors.Contains(monitor))
            _activeMonitors.Add(monitor);

        ApplyActivateMonitorTransition(monitor, transition);
    }

    public void Activate(Type monitorType, ITransition transition = null)
    {
        DeactivateAll();
        SetActive(monitorType, transition);
        var monitor = _monitorByType[monitorType];
        monitor.Canvas.sortingOrder = _sortingOrderByMonitor[monitor];
    }

    public void Additive(Type monitorType, ITransition transition = null, bool sorting = true)
    {
        SetActive(monitorType, transition);
        var monitor = _monitorByType[monitorType];
        monitor.Canvas.sortingOrder = sorting ? _highestSortingOrder++ : _sortingOrderByMonitor[monitor];
    }

    public void Deactivate(Type monitorType, ITransition transition = null)
    {
        var monitor = _monitorByType[monitorType];
        Deactivate(monitor, transition);
    }

    public void Activate<TMonitor>(ITransition transition = null) where TMonitor : MonitorBase
    {
        var monitorType = typeof(TMonitor);
        Activate(monitorType, transition);
    }

    public void Additive<TMonitor>(ITransition transition = null, bool sorting = true) where TMonitor : MonitorBase
    {
        var monitorType = typeof(TMonitor);
        Additive(monitorType, transition, sorting);
    }

    public void Deactivate<TMonitor>(ITransition transition = null) where TMonitor : MonitorBase
    {
        var monitorType = typeof(TMonitor);
        Deactivate(monitorType, transition);
    }

    public async UniTaskVoid Deactivate(MonitorBase monitor, ITransition transition = null)
    {
        if (monitor.AlwaysActivated) return;
        if (transition != null)
        {
            if (_transitionCtsByMonitor.TryGetValue(monitor, out var cts))
            {
                cts.Cancel();
                _transitionCtsByMonitor.Remove(monitor);
            }
            var transitionCancellation = new CancellationTokenSource();
            _transitionCtsByMonitor[monitor] = transitionCancellation;
            await transition.Apply(monitor, transitionCancellation.Token);
        }
        monitor.gameObject.SetActive(false);
        _activeMonitors.Remove(monitor);
    }

    public void DeactivateAll()
    {
        for (var i = _activeMonitors.Count - 1; i >= 0; i--)
        {
            if (i >= _activeMonitors.Count) continue;
            var activeMonitor = _activeMonitors[i];
            Deactivate(activeMonitor);
        }
    }

    public TMonitor Get<TMonitor>() where TMonitor : MonitorBase
    {
        return (TMonitor)_monitorByType[typeof(TMonitor)];
    }

    public bool IsActive<TMonitor>() where TMonitor : MonitorBase
    {
        var monitor = Get<TMonitor>();
        return _activeMonitors.Contains(monitor);
    }

    public int GetActiveMonitorCount()
    {
        var count = 0;
        foreach (var activeMonitor in _activeMonitors)
        {
            if (activeMonitor.AlwaysActivated) continue;
            count++;
        }
        return count;
    }

    public void DisableDragging(object owner = null)
    {
        if (owner != null) _draggingOwners.Add(owner);
        _eventSystem.pixelDragThreshold = 100000;
    }

    public void RestoreDragging(object owner = null)
    {
        if (owner != null) _draggingOwners.Remove(owner);
        if (_draggingOwners.Count > 0) return;
        _eventSystem.pixelDragThreshold = _originalPixelDragThreshold;
    }

    public void DisableRaycaster(object owner = null)
    {
        _eventSystem.currentInputModule.DeactivateModule();
        Get<RaycastBlockerMonitor>().Block(owner);
    }

    public void RestoreRaycaster(object owner = null)
    {
        _eventSystem.currentInputModule.ActivateModule();
        Get<RaycastBlockerMonitor>().Unblock(owner);
    }

    public bool IsRaycasterActive()
    {
        return !IsActive<RaycastBlockerMonitor>();
    }

    public void SetClickableArea(RectTransform clickableArea)
    {
        Get<RaycastBlockerMonitor>().SetClickableArea(clickableArea);
    }

    public void ApplyActivateMonitorTransition(IMonitor monitor, ITransition transition = null)
    {
        if (_transitionCtsByMonitor.TryGetValue(monitor, out var cts))
        {
            cts.Cancel();
            _transitionCtsByMonitor.Remove(monitor);
        }

        var transitionCancellation = new CancellationTokenSource();
        if (transition == null)
        {
            if (_isAlphaVisible)
            {
                var defaultFadeTransition = new FadeTransition
                {
                    CanvasGroup = _canvasGroup,
                    Duration = .2f
                };
                defaultFadeTransition.Apply(monitor, transitionCancellation.Token);
            }
        }
        else
            transition.Apply(monitor, transitionCancellation.Token);

        _transitionCtsByMonitor[monitor] = transitionCancellation;

        if (monitor is not StatefulView view)
            return;

        if (view.HasObject(ObjectRole.Popup))
        {
            var popupTransition = new PopupTransition { Duration = .25f };
            popupTransition.Apply(monitor, transitionCancellation.Token);
        }
    }

    public void SetAlphaVisibility(bool visibility)
    {
        _isAlphaVisible = visibility;
        _canvasGroup.alpha = visibility ? 1f : 0f;
    }

    public void ToggleAlphaVisibility()
    {
        SetAlphaVisibility(!_isAlphaVisible);
    }
}



public interface ITransition
{
    public UniTask Apply(IMonitor monitor, CancellationToken cancellationToken = default);
}

public struct NoTransition : ITransition
{
    public UniTask Apply(IMonitor monitor, CancellationToken cancellationToken = default) => UniTask.CompletedTask;

    public static NoTransition Instance = new();
}

public struct FadeTransition : ITransition
{
    public CanvasGroup CanvasGroup;
    public float Duration;

    public UniTask Apply(IMonitor monitor, CancellationToken cancellationToken = default)
    {
        CanvasGroup.alpha = 0f;
        var task = LMotion.Create(0f, 1f, Duration)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAlpha(CanvasGroup)
            .ToUniTask(cancellationToken: cancellationToken);

        return task;
    }
}

public struct SlideTransition : ITransition
{
    public Direction Direction;
    public bool Activating;
    public float Duration;

    public UniTask Apply(IMonitor monitor, CancellationToken cancellationToken = default)
    {
        if (monitor is not StatefulView view)
            return UniTask.CompletedTask;

        if (!view.HasObject(ObjectRole.Page))
            return UniTask.CompletedTask;

        var page = view.GetObject(ObjectRole.Page);
        var pageT = (page.transform as RectTransform)!;
        var f = 500f;
        var from = Activating ? Direction == Direction.Left ? -pageT.rect.width - f : pageT.rect.width + f : 0f;
        var to = Activating ? 0f : Direction == Direction.Left ? -pageT.rect.width - f : pageT.rect.width + f;
        var task = LMotion.Create(from, to, Duration)
            .WithEase(Ease.InOutSine)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithOnComplete(() => pageT.anchoredPosition = Vector2.zero)
            .BindToAnchoredPositionX(pageT)
            .AddTo(view)
            .ToUniTask(cancellationToken: cancellationToken);

        return task;
    }
}

public struct PopupTransition : ITransition
{
    public float Duration;

    public UniTask Apply(IMonitor monitor, CancellationToken cancellationToken = default)
    {
        if (monitor is not StatefulView view)
            return UniTask.CompletedTask;

        if (!view.HasObject(ObjectRole.Popup))
            return UniTask.CompletedTask;

        var popup = view.GetObject(ObjectRole.Popup);
        var popupT = popup.transform;
        var task = LMotion.Create(Vector3H.AlmostZero, Vector3.one, Duration)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.OutBack)
            .BindToLocalScale(popupT)
            .AddTo(view)
            .ToUniTask(cancellationToken: cancellationToken);

        return task;
    }
}
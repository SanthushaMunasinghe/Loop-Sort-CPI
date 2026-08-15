using StatefulUISupport.Scripts.Components;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

public abstract class MonitorBase : StatefulView, IMonitor, IInitializable, ITickable
{
    [field: SerializeField] public bool AlwaysActivated { get; private set; }

    [Inject] protected Monitors Monitors;
    [Inject] protected Camera Camera;
    [Inject] protected AssetModule AssetModule;

    public Canvas Canvas { get; private set; }
    public CanvasScaler CanvasScaler { get; private set; }
    public RectTransform RectTransform => transform as RectTransform;

    private bool _setup;
    private MonitorVariantBase _monitorVariant;
    private ContainerBase[] _containers;
    private ElementBase[] _elements;

    protected virtual void OnEnable()
    {
        if (_setup) OnActivated();
    }

    protected virtual void OnDisable()
    {
        if (_setup) OnDeactivated();
    }

    public void Initialize()
    {
        Setup();
    }

    public virtual void Tick()
    {
        if (!gameObject.activeSelf) return;
        Render();
    }

    public virtual void Setup()
    {
        Canvas = GetComponent<Canvas>();
        Canvas.worldCamera = Camera;
        Canvas.planeDistance = 1f;
        SetRenderMode(RenderMode.ScreenSpaceCamera);
        CanvasScaler = GetComponent<CanvasScaler>();
        _setup = true;

        var monitorScope = Monitors.GetComponent<MonitorScope>();

        _containers = GetComponentsInChildren<ContainerBase>(includeInactive: true);
        foreach (var container in _containers)
        {
            monitorScope.Container.Inject(container);
            container.Setup();
        }

        _elements = GetComponentsInChildren<ElementBase>(includeInactive: true);
        foreach (var element in _elements)
        {
            monitorScope.Container.Inject(element);
            element.Setup();
        }

        _monitorVariant = GetComponent<MonitorVariantBase>();
        if (_monitorVariant == null) return;
        monitorScope.Container.Inject(_monitorVariant);
        _monitorVariant.Setup();
    }

    public virtual void Render()
    {
        _monitorVariant?.Render();
        foreach (var container in _containers) container.Render();
        foreach (var element in _elements) element.Render();
    }

    public virtual void OnActivated()
    {
        _monitorVariant?.OnActivated();
        foreach (var element in _elements) element.OnActivated();
    }

    public virtual void OnDeactivated()
    {
        _monitorVariant?.OnDeactivated();
        foreach (var element in _elements) element.OnDeactivated();
    }

    public void SetRenderMode(RenderMode renderMode)
    {
        Canvas.renderMode = renderMode;
    }

    public void SetScaleMode(CanvasScaler.ScaleMode scaleMode)
    {
        CanvasScaler.uiScaleMode = scaleMode;
    }

    public Vector3 WorldToCanvasSpace(Vector3 worldPosition)
    {
        if (Canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            var screenPoint = Camera.WorldToScreenPoint(worldPosition);
            var worldPoint = Camera.ScreenToWorldPoint(position: screenPoint.WithZ(Canvas.planeDistance));
            return worldPoint;
        }

        if (Canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            var screenPoint = Camera.WorldToScreenPoint(worldPosition);
            return screenPoint;
        }

        return Vector3.zero;
    }

    public void WorldToCanvasSpace(Transform t, Vector3 worldPosition)
    {
        t.localRotation = Quaternion.identity;
        t.position = WorldToCanvasSpace(worldPosition);
    }

    public Vector3 ScreenToCanvasSpace(Vector3 screenPosition)
    {
        if (Canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            var worldPoint = Camera.ScreenToWorldPoint(position: screenPosition.WithZ(Canvas.planeDistance));
            return worldPoint;
        }

        if (Canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return screenPosition;
        }

        return Vector3.zero;
    }

    public void ScreenToCanvasSpace(Transform t, Vector3 screenPosition)
    {
        t.localRotation = Quaternion.identity;
        t.position = ScreenToCanvasSpace(screenPosition);
    }
}

public interface IMonitor
{
    public void Setup();
    public void Render();
    public void OnActivated();
    public void OnDeactivated();
}
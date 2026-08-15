using StatefulUISupport.Scripts.Components;
using VContainer;

public abstract class MonitorVariantBase : StatefulView, IMonitor
{
    [Inject] protected Monitors Monitors;

    public virtual void Setup()
    {
    }

    public virtual void Render()
    {
    }

    public virtual void OnActivated()
    {
    }

    public virtual void OnDeactivated()
    {
    }
}
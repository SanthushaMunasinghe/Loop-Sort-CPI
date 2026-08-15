using StatefulUISupport.Scripts.Components;

public abstract class ElementBase : StatefulView, IMonitor
{
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
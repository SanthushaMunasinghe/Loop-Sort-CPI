using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;

public abstract class TutorialBase : MonitorBase
{
    [Inject] protected HighlightMonitor Highlight;
    [Inject] protected InteractionModule Interaction;

    public abstract UniTask<bool> Play();

    public virtual void SetCustomScope(LifetimeScope scope) {}
    public virtual string GetSaveKey() => gameObject.name;
}
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using VContainer;

public abstract class InstallerBase
{
    public virtual UniTask Initialize(Dictionary<string, string> args) => default;
    public virtual UniTask PostBuild() => default;

    public virtual void Install(IContainerBuilder builder)
    {
    }
}
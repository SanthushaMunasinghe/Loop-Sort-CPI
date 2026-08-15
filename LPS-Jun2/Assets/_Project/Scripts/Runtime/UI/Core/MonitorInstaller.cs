using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class MonitorInstaller : InstallerBase
{
    private Monitors _monitorsInstance;

    public override async UniTask Initialize(Dictionary<string, string> args)
    {
        var monitorsPrefab = (Monitors)await Resources.LoadAsync<Monitors>("Monitors");
        _monitorsInstance = Object.Instantiate(monitorsPrefab);
        Object.DontDestroyOnLoad(_monitorsInstance.gameObject);
    }

    public override void Install(IContainerBuilder builder)
    {
        builder.RegisterComponent(_monitorsInstance).AsSelf();
    }

    public override UniTask PostBuild()
    {
        LifetimeScopeH.InjectIfNotScoped<BootstrapScope>(_monitorsInstance.gameObject);
        _monitorsInstance.GetComponent<MonitorScope>().Build();
        return default;
    }
}
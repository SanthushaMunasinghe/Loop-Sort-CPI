using VContainer;
using VContainer.Unity;

public sealed class MonitorScope : LifetimeScope
{
    [Inject] private Monitors _monitors;
    [Inject] private MonitorContainer _monitorContainer;

    protected override void Configure(IContainerBuilder builder)
    {
        base.Configure(builder);

        foreach (var monitorPrefab in _monitorContainer.Collection)
        {
            var monitorInstance = Instantiate(monitorPrefab, transform);
            builder.RegisterComponent(monitorInstance).AsSelf().AsImplementedInterfaces();
            _monitors.Register(monitorInstance);
        }
    }
}
using VContainer;

public sealed class ModuleInstaller : InstallerBase
{
    public override void Install(IContainerBuilder builder)
    {
        var moduleBaseType = typeof(ModuleBase);
        using var p = moduleBaseType.GetDerivedClassTypes(out var derivedModuleTypes);
        foreach (var moduleType in derivedModuleTypes)
        {
            builder.Register(moduleType, Lifetime.Singleton).AsSelf().AsImplementedInterfaces();
        }
    }
}
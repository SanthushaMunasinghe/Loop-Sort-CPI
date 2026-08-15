using VContainer;
using VContainer.Unity;

public sealed class GameMachineInstaller : InstallerBase
{
    public override void Install(IContainerBuilder builder)
    {
        builder.RegisterEntryPoint<GameMachine>().AsSelf();

        var baseGameStateType = typeof(GameStateBase);
        using var p = baseGameStateType.GetDerivedClassTypes(out var derivedGameStateClassTypes);
        foreach (var stateType in derivedGameStateClassTypes)
        {
            builder.Register(stateType, Lifetime.Singleton).AsSelf().AsImplementedInterfaces();
        }
    }
}
using MessagePipe;
using VContainer;

public sealed class MessagePipeInstaller : InstallerBase
{
    public override void Install(IContainerBuilder builder)
    {
        builder.RegisterMessagePipe();
        builder.RegisterBuildCallback(container =>
        {
            GlobalMessagePipe.SetProvider(container.AsServiceProvider());
        });
    }
}
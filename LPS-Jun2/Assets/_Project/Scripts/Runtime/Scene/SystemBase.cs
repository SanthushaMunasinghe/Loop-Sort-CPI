using System;
using System.Threading;
using MessagePipe;
using Scellecs.Morpeh;
using VContainer;

public abstract class SystemBase : ISystem
{
    [Inject] private SceneModule _sceneModule;

    public World World { get; set; }
    public CancellationToken SceneLoadToken { get; private set; }

    private IDisposable _messageDisposable;

    public virtual void OnAwake()
    {
        LifetimeScopeH.InjectIfNotScoped<SceneScope>(this);

        SceneLoadToken = _sceneModule.SceneLoadToken;

        var bag = DisposableBag.CreateBuilder();
        BuildMessages(bag);
        _messageDisposable = bag.Build();
    }

    public virtual void OnUpdate(float deltaTime)
    {
    }

    public virtual void Dispose()
    {
        _messageDisposable?.Dispose();
    }

    protected virtual void BuildMessages(DisposableBagBuilder bag)
    {
    }
}
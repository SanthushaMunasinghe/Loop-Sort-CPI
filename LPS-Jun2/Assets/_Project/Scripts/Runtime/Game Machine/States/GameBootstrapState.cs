using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine.Pool;
using VContainer;
using Object = UnityEngine.Object;

public sealed class GameBootstrapState : GameStateBase
{
    [Inject] private IReadOnlyList<IProviderInitialize> _providers;
    [Inject] private IPublisher<LoginMessage> _loginPub;

    public override async void OnEnter()
    {
        base.OnEnter();

        if (!Prefs.InstallTimestamp.HasValue())
            Prefs.InstallTimestamp.Set(DateTimeOffset.UtcNow);

        await UniTask.NextFrame();

        using var p = ListPool<UniTask>.Get(out var tasks);
        foreach (var provider in _providers)
        {
            var uniTask = provider.InitializeSdk();
            tasks.Add(uniTask);
        }
        await UniTask.WhenAll(tasks);

        _loginPub.Publish(new LoginMessage());
        Machine.RequestStateChange<GameStartState>();
    }
}

public interface IProviderInitialize
{
    public UniTask InitializeSdk();
}

public struct LoginMessage
{
}

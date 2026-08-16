using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using Object = UnityEngine.Object;

public sealed class SceneModule : ModuleBase
{
    public SceneScope Scope { get; private set; }
    public IObjectResolver Container { get; private set; }
    public bool IsSceneReady { get; private set; }
    public CancellationToken SceneLoadToken => (_sceneLoadCts ??= new CancellationTokenSource()).Token;

    private CancellationTokenSource _sceneLoadCts;

    [Inject] private IPublisher<ScenePreLoadMessage> _scenePreLoad;
    [Inject] private IPublisher<ScenePostLoadMessage> _scenePostLoad;
    [Inject] private IAsyncPublisher<ScenePreLoadMessage> _scenePreLoadAsync;
    [Inject] private IAsyncPublisher<ScenePostLoadMessage> _scenePostLoadAsync;

    public async UniTask Load(bool waitPostLoad = false, bool cleanUpResources = false)
    {
        // Scope = null;
        // Container = null;
        IsSceneReady = false;

        _sceneLoadCts?.Cancel();
        _sceneLoadCts = new CancellationTokenSource();
        await _scenePreLoadAsync.PublishAsync(new ScenePreLoadMessage(), cancellationToken: _sceneLoadCts.Token);
        _scenePreLoad.Publish(new ScenePreLoadMessage());

        using (TimeTracker.Begin("Scene loaded in {0:0.000}s"))
        {
            await SceneManager.LoadSceneAsync("_Project/Scenes/Game").ToUniTask(cancellationToken: _sceneLoadCts.Token);

            if (cleanUpResources)
            {
                Resources.UnloadUnusedAssets();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized);
            }

            Scope = Object.FindObjectOfType<SceneScope>();
            Container = Scope.Container;
            await UniTask.NextFrame(cancellationToken: _sceneLoadCts.Token);
            IsSceneReady = true;
        }

        var postSceneLoadTask = _scenePostLoadAsync.PublishAsync(new ScenePostLoadMessage(), cancellationToken: _sceneLoadCts.Token);
        if (waitPostLoad) await postSceneLoadTask;
        _scenePostLoad.Publish(new ScenePostLoadMessage());
    }

}

public struct ScenePreLoadMessage
{
}

public struct ScenePostLoadMessage
{
}
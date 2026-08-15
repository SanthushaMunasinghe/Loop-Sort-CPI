using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class PrefabReturnerOnDisable : MonoBehaviour
{
    private static PrefabModule _prefabModule;

    private void OnDisable()
    {
        ReturnPrefabAfterDelay().Forget();
    }

    private async UniTaskVoid ReturnPrefabAfterDelay()
    {
        await UniTask.NextFrame();

        if (Application.exitCancellationToken.IsCancellationRequested) return;
        if (this == null) return;
        if (gameObject == null) return;

        ResolvePrefabModule();
        if (_prefabModule == null) return;
        _prefabModule.Return(gameObject);
    }

    private static void ResolvePrefabModule()
    {
        if (_prefabModule != null) return;

        var scope = LifetimeScopeH.FindScope<BootstrapScope>();
        if (scope == null || scope.Container == null) return;
        _prefabModule = scope.Container.Resolve<PrefabModule>();
    }
}
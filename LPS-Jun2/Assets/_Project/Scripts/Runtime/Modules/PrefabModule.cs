using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using uPools;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

public sealed class PrefabModule : ModuleBase, IInitializable
{
    private readonly Dictionary<GameObject, Stack<GameObject>> _pools = new();
    private readonly Dictionary<GameObject, Stack<GameObject>> _cloneReferences = new();

    private Transform _rootParent;
    private readonly Dictionary<GameObject, Transform> _poolParents = new();
    private readonly Dictionary<GameObject, Transform> _poolReferences = new();

    private static Scene ActiveScene => SceneManager.GetActiveScene();

    [Inject] private ISubscriber<ScenePreLoadMessage> _scenePreLoadSub;

    public void Initialize()
    {
        _scenePreLoadSub.Subscribe(OnScenePreLoad);
    }

    private void OnScenePreLoad(ScenePreLoadMessage m)
    {
        if (_cloneReferences.Count == 0) return;

        using var instances = _cloneReferences.Keys.ToList().GetEnumerator();
        while (instances.MoveNext())
        {
            var instance = instances.Current;
            Return(instance);
        }
    }

    public GameObject Rent(GameObject original)
    {
        if (original == null) throw new ArgumentNullException(nameof(original));

        var pool = GetOrCreatePool(original);

        GameObject obj;
        while (true)
        {
            if (!pool.TryPop(out obj))
            {
                obj = Object.Instantiate(original);
                break;
            }
            else if (obj != null)
            {
                obj.transform.SetParent(null);
                SceneManager.MoveGameObjectToScene(obj, ActiveScene);
                obj.SetActive(true);
                break;
            }
        }

        _cloneReferences.Add(obj, pool);

        var poolParent = GetOrCreatePoolParent(original);
        _poolReferences.Add(obj, poolParent);

        PrefabModuleCallbackHelper.InvokeOnRent(obj);

        return obj;
    }

    public GameObject Rent(GameObject original, Transform parent)
    {
        if (original == null) throw new ArgumentNullException(nameof(original));

        var pool = GetOrCreatePool(original);

        GameObject obj;
        while (true)
        {
            if (!pool.TryPop(out obj))
            {
                obj = Object.Instantiate(original, parent);
                break;
            }
            else if (obj != null)
            {
                obj.transform.SetParent(parent);
                obj.SetActive(true);
                break;
            }
        }

        _cloneReferences.Add(obj, pool);

        var poolParent = GetOrCreatePoolParent(original);
        _poolReferences.Add(obj, poolParent);

        PrefabModuleCallbackHelper.InvokeOnRent(obj);

        return obj;
    }

    public GameObject Rent(GameObject original, Vector3 position, Quaternion rotation)
    {
        if (original == null) throw new ArgumentNullException(nameof(original));

        var pool = GetOrCreatePool(original);

        GameObject obj;
        while (true)
        {
            if (!pool.TryPop(out obj))
            {
                obj = Object.Instantiate(original, position, rotation);
                break;
            }
            else if (obj != null)
            {
                obj.transform.SetParent(null);
                SceneManager.MoveGameObjectToScene(obj, ActiveScene);
                obj.transform.SetPositionAndRotation(position, rotation);
                obj.SetActive(true);
                break;
            }
        }

        _cloneReferences.Add(obj, pool);

        var poolParent = GetOrCreatePoolParent(original);
        _poolReferences.Add(obj, poolParent);

        PrefabModuleCallbackHelper.InvokeOnRent(obj);

        return obj;
    }

    public GameObject Rent(GameObject original, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (original == null) throw new ArgumentNullException(nameof(original));

        var pool = GetOrCreatePool(original);

        GameObject obj;
        while (true)
        {
            if (!pool.TryPop(out obj))
            {
                obj = Object.Instantiate(original, position, rotation, parent);
                break;
            }
            else if (obj != null)
            {
                obj.transform.SetParent(parent);
                obj.transform.SetPositionAndRotation(position, rotation);
                obj.SetActive(true);
                break;
            }
        }

        _cloneReferences.Add(obj, pool);

        var poolParent = GetOrCreatePoolParent(original);
        _poolReferences.Add(obj, poolParent);

        PrefabModuleCallbackHelper.InvokeOnRent(obj);

        return obj;
    }

    public void Return(GameObject instance)
    {
        // if (instance == null) throw new ArgumentNullException(nameof(instance));
        if (instance == null) return;

        if (!_cloneReferences.TryGetValue(instance, out var pool))
        {
            // Object.Destroy(instance);
            return;
        }

        instance.SetActive(false);
        pool.Push(instance);
        _cloneReferences.Remove(instance);

        var poolParent = _poolReferences[instance];
        var instanceT = instance.transform;
        instanceT.SetParent(poolParent);
        instanceT.position = Vector3.zero;
        instanceT.rotation = Quaternion.identity;
        _poolReferences.Remove(instance);

        PrefabModuleCallbackHelper.InvokeOnReturn(instance);
    }

    public void Prewarm(GameObject original, int maxCount)
    {
        if (original == null) throw new ArgumentNullException(nameof(original));

        var pool = GetOrCreatePool(original);
        var poolParent = GetOrCreatePoolParent(original);

        for (var i = pool.Count; i < maxCount; i++)
        {
            var obj = Object.Instantiate(original, poolParent);
            obj.SetActive(false);
            pool.Push(obj);

            PrefabModuleCallbackHelper.InvokeOnReturn(obj);
        }
    }

    private Stack<GameObject> GetOrCreatePool(GameObject original)
    {
        if (!_pools.TryGetValue(original, out var pool))
        {
            pool = new Stack<GameObject>();
            _pools.Add(original, pool);
        }

        return pool;
    }

    private Transform GetOrCreatePoolParent(GameObject original)
    {
        if (_rootParent == null)
        {
            var rootParent = new GameObject("Prefab Module - Pools");
            rootParent.SetActive(false);
            Object.DontDestroyOnLoad(rootParent);
            _rootParent = rootParent.transform;
        }

        if (!_poolParents.TryGetValue(original, out var poolParent))
        {
            poolParent = new GameObject(original.name + " - Pool").transform;
            poolParent.SetParent(_rootParent);
            // poolParent.gameObject.SetActive(false);
            _poolParents.Add(original, poolParent);
        }

        return poolParent;
    }

    public bool IsGameObjectPooled(GameObject instance)
    {
        return _poolReferences.ContainsKey(instance);
    }
}

internal static class PrefabModuleCallbackHelper
{
    // private static readonly List<IPoolCallbackReceiver> ComponentsBuffer = new();

    public static void InvokeOnRent(GameObject obj)
    {
#if UNITY_EDITOR
        UnityEditor.SceneVisibilityManager.instance.Show(obj, true);
#endif
        using var p = ListPool<IPoolCallbackReceiver>.Get(out var receivers);
        obj.GetComponents(receivers);
        foreach (var receiver in receivers) receiver.OnRent();
    }

    public static void InvokeOnReturn(GameObject obj)
    {
        using var p = ListPool<IPoolCallbackReceiver>.Get(out var receivers);
        obj.GetComponents(receivers);
        foreach (var receiver in receivers) receiver.OnReturn();
    }
}

public static class PrefabModuleExtensions
{
    public static TComponent Rent<TComponent>(this PrefabModule m, TComponent original) where TComponent : Component
    {
        return m.Rent(original.gameObject).GetComponent<TComponent>();
    }

    public static TComponent Rent<TComponent>(this PrefabModule m, TComponent original, Vector3 position,
        Quaternion rotation, Transform parent) where TComponent : Component
    {
        return m.Rent(original.gameObject, position, rotation, parent).GetComponent<TComponent>();
    }

    public static TComponent Rent<TComponent>(this PrefabModule m, TComponent original, Vector3 position,
        Quaternion rotation) where TComponent : Component
    {
        return m.Rent(original.gameObject, position, rotation).GetComponent<TComponent>();
    }

    public static TComponent Rent<TComponent>(this PrefabModule m, TComponent original, Transform parent)
        where TComponent : Component
    {
        return m.Rent(original.gameObject, parent).GetComponent<TComponent>();
    }
}
using System;
using System.Collections.Generic;
using UnityEngine.Pool;

public partial class SceneRegistry
{
    public PooledObject<List<T>> ActiveOf<T>(out List<T> result) where T : GameBehaviourBase
    {
        var p = ListPool<T>.Get(out result);
        if (_map.TryGetValue(typeof(T), out var set) && set.Count > 0)
        {
            var hashSet = (HashSet<T>)set.Raw;
            foreach (var b in hashSet)
            {
                if (!b.gameObject.activeSelf) continue;
                result.Add(b);
            }
        }
        return p;
    }

    public PooledObject<List<T>> AllOf<T>(out List<T> result) where T : GameBehaviourBase
    {
        var p = ListPool<T>.Get(out result);
        if (_map.TryGetValue(typeof(T), out var set) && set.Count > 0)
        {
            var hashSet = (HashSet<T>)set.Raw;
            result.AddRange(hashSet);
        }
        return p;
    }

    public void ForEach<T>(Action<T> action) where T : GameBehaviourBase
    {
        if (!_map.TryGetValue(typeof(T), out var set) || set.Count == 0) return;
        var hashSet = (HashSet<T>)set.Raw;
        foreach (var b in hashSet) action(b);
    }

    public T Any<T>() where T : GameBehaviourBase
    {
        if (!_map.TryGetValue(typeof(T), out var set) || set.Count == 0)
            return null;

        if (set.TryGetFirst(out var result))
            return (T)result;

        return null;
    }

    public bool TryAny<T>(out T result) where T : GameBehaviourBase
    {
        result = null;

        if (!_map.TryGetValue(typeof(T), out var set) || set.Count == 0)
            return false;

        if (set.TryGetFirst(out var first))
        {
            result = (T)first;
            return true;
        }

        return false;
    }

    public int CountOf<T>() where T : GameBehaviourBase
    {
        return _map.TryGetValue(typeof(T), out var set) ? set.Count : 0;
    }

    public int ActiveCountOf<T>() where T : GameBehaviourBase
    {
        if (!_map.TryGetValue(typeof(T), out var set)) return 0;
        var count = 0;
        var hashSet = (HashSet<T>)set.Raw;
        foreach (var b in hashSet)
            if (b.gameObject.activeSelf)
                count++;
        return count;
    }
}

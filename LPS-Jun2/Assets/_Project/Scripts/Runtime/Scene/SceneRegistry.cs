using System;
using System.Collections;
using System.Collections.Generic;
using MessagePipe;
using VContainer;
using VContainer.Unity;

public sealed partial class SceneRegistry : IStartable, IDisposable
{
    [Inject] private ISubscriber<GameBehaviourRentedMessage> _gameBehaviourRentedSub;
    [Inject] private ISubscriber<GameBehaviourReturnedMessage> _gameBehaviourReturnedSub;

    private readonly Dictionary<Type, IBehaviourSet> _map = new();
    private IDisposable _messages;

    public void Start()
    {
        var bag = DisposableBag.CreateBuilder();
        _gameBehaviourRentedSub.Subscribe(OnGameBehaviourRented).AddTo(bag);
        _gameBehaviourReturnedSub.Subscribe(OnGameBehaviourReturned).AddTo(bag);
        _messages = bag.Build();
    }

    public void Dispose()
    {
        _messages?.Dispose();
        foreach (var (_, set) in _map) set.Clear();
    }

    private void OnGameBehaviourRented(GameBehaviourRentedMessage m)
    {
        var type = m.Behaviour.GetType();
        for (var t = type; t != typeof(GameBehaviourBase) && t != null; t = t.BaseType)
        {
            if (!_map.TryGetValue(t, out var set))
            {
                var genericSetType = typeof(BehaviourSet<>).MakeGenericType(t);
                var newSet = Activator.CreateInstance(genericSetType);
                set = (IBehaviourSet)newSet;
                _map[t] = set;
            }

            set.Add(m.Behaviour);
        }
    }

    private void OnGameBehaviourReturned(GameBehaviourReturnedMessage m)
    {
        var type = m.Behaviour.GetType();
        for (var t = type; t != typeof(GameBehaviourBase) && t != null; t = t.BaseType)
        {
            if (!_map.TryGetValue(t, out var set)) continue;
            set.Remove(m.Behaviour);
        }
    }
}

internal interface IBehaviourSet
{
    public IEnumerable Raw { get; }
    public int Count { get; }

    public void Add(GameBehaviourBase b);
    public void Remove(GameBehaviourBase b);
    public void Clear();
    public bool TryGetFirst(out GameBehaviourBase result);
}

internal sealed class BehaviourSet<T> : IBehaviourSet where T : GameBehaviourBase
{
    private readonly HashSet<T> _set = new();

    public IEnumerable Raw => _set;
    public int Count => _set.Count;

    public void Add(GameBehaviourBase b) => _set.Add((T)b);
    public void Remove(GameBehaviourBase b) => _set.Remove((T)b);
    public void Clear() => _set.Clear();

    public bool TryGetFirst(out GameBehaviourBase result)
    {
        using var enumerator = _set.GetEnumerator();
        if (enumerator.MoveNext())
        {
            result = enumerator.Current;
            return true;
        }

        result = null;
        return false;
    }
}
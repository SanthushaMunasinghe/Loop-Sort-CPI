using System;
using System.Collections.Generic;
using System.Threading;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine;
using uPools;
using VContainer;

public abstract partial class GameBehaviourBase : MonoBehaviour, IPoolCallbackReceiver
{
    private int EntityID => _entity.IsNullOrDisposed() ? -1 : _entity.Id;
    private static readonly Dictionary<int, Entity> InstanceIDToEntity = new();

    private Entity _entity;
    private bool _isRented;
    private IDisposable _messageDisposable;
    private CancellationTokenSource _returnCts;

    [Inject] protected World World { get; private set; }

    [Inject] private IPublisher<GameBehaviourRentedMessage> _gameBehaviourRentedPub;
    [Inject] private IPublisher<GameBehaviourReturnedMessage> _gameBehaviourReturnedPub;

    public CancellationToken ReturnToken
    {
        get
        {
            _returnCts ??= new CancellationTokenSource();
            return _returnCts.Token;
        }
    }

    public CancellationToken SceneLoadToken { get; private set; }

    protected virtual void Awake()
    {
        OnRent();
    }

    protected virtual void Start()
    {
    }

    protected virtual void OnEnable()
    {
        SetEntityViewEnabled();
    }

    protected virtual void OnDisable()
    {
        SetEntityViewDisabled();
    }

    protected virtual void OnDestroy()
    {
        if (Application.exitCancellationToken.IsCancellationRequested)
            return;

        if (PrefabModule.IsGameObjectPooled(gameObject))
            throw new Exception($"There is a PrefabModule leak. {gameObject.name}");

        OnReturn();
    }

    public virtual void OnRent()
    {
        if (_isRented) return;
        _isRented = true;

        LifetimeScopeH.InjectIfNotScoped<SceneScope>(this);

        var instanceID = gameObject.GetInstanceID();
        _entity = InstanceIDToEntity.TryGetValue(instanceID, out var entity) ? entity : World.CreateEntity();
        InstanceIDToEntity[instanceID] = _entity;

        var bag = DisposableBag.CreateBuilder();
        BuildMessages(bag);
        _messageDisposable = bag.Build();
        _returnCts = null;
        SceneLoadToken = SceneModule.SceneLoadToken;

        SetEntityViewEnabled();

        _gameBehaviourRentedPub.Publish(new GameBehaviourRentedMessage { Behaviour = this });
    }

    public virtual void OnReturn()
    {
        _isRented = false;

        World.RemoveEntity(_entity);
        var instanceID = gameObject.GetInstanceID();
        InstanceIDToEntity.Remove(instanceID);

        _rendererPropertyRegistries.Clear();
        _messageDisposable?.Dispose();
        _returnCts?.Cancel();
        _objectMotionHandle?.Cancel();

        _gameBehaviourReturnedPub.Publish(new GameBehaviourReturnedMessage { Behaviour = this });
    }

    protected virtual void BuildMessages(DisposableBagBuilder bag)
    {
    }

    public void RegisterView<T>() where T : GameBehaviourBase
    {
        if (World.IsNullOrDisposed()) return;
        var behaviourStash = World.GetStash<BehaviourView<T>>();
        if (behaviourStash.Has(_entity)) return;
        behaviourStash.Add(_entity, new BehaviourView<T> { Behaviour = (T)this });
    }

    private void SetEntityViewEnabled()
    {
        if (World.IsNullOrDisposed()) return;
        if (World.IsDisposed(_entity)) return;
        var stash = World.GetStash<Enabled>();
        if (stash.Has(_entity)) return;
        stash.Add(_entity);
    }

    private void SetEntityViewDisabled()
    {
        if (World.IsNullOrDisposed()) return;
        if (World.IsDisposed(_entity)) return;
        var stash = World.GetStash<Enabled>();
        if (!stash.Has(_entity)) return;
        stash.Remove(_entity);
    }

    public static implicit operator Entity(GameBehaviourBase b) => b._entity;
}

public struct Enabled : IComponent
{
}

public struct BehaviourView<T> : IComponent where T : GameBehaviourBase
{
    public T Behaviour;

    public static implicit operator T(BehaviourView<T> b) => b.Behaviour;
}

public struct GameBehaviourRentedMessage
{
    public GameBehaviourBase Behaviour;
}

public struct GameBehaviourReturnedMessage
{
    public GameBehaviourBase Behaviour;
}
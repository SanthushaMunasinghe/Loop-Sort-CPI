using UnityHFSM;
using VContainer;
using VContainer.Unity;

public sealed class GameMachine : StateMachine, IInitializable, ITickable
{
    [Inject] private IObjectResolver _resolver;

    public void Initialize()
    {
        var baseGameStateType = typeof(GameStateBase);
        using var p = baseGameStateType.GetDerivedClassTypes(out var derivedGameStateClassTypes);
        foreach (var stateType in derivedGameStateClassTypes)
        {
            var stateInstance = _resolver.Resolve(stateType) as State;
            AddState(stateType.Name, stateInstance);
        }

        SetStartState(nameof(GameBootstrapState));
        Init();
    }

    public void Tick()
    {
        OnLogic();
    }

    public void RequestStateChange<TState>() where TState : GameStateBase
    {
        var requestedStateName = typeof(TState).Name;
        if (ActiveStateName == requestedStateName) return;
        RequestStateChange(requestedStateName);
    }

    public bool IsInState<TState>() where TState : GameStateBase
    {
        return ActiveStateName == typeof(TState).Name;
    }
}
using MessagePipe;
using UnityHFSM;
using VContainer;

public abstract class GameStateBase : State
{
    [Inject] protected GameMachine Machine;

    [Inject] private IPublisher<GameStateEnterMessage> _gameStateEnterPub;
    [Inject] private IPublisher<GameStateExitMessage> _gameStateExitPub;

    protected GameStateBase() : base(needsExitTime: true)
    {
    }

    public override void OnEnter()
    {
        base.OnEnter();

        _gameStateEnterPub.Publish(new GameStateEnterMessage { State = this });
    }

    public override void OnExit()
    {
        base.OnExit();

        _gameStateExitPub.Publish(new GameStateExitMessage { State = this });
    }

    public override void OnExitRequest()
    {
        base.OnExitRequest();

        fsm.StateCanExit();
    }
}

public struct GameStateEnterMessage
{
    public GameStateBase State;
}

public struct GameStateExitMessage
{
    public GameStateBase State;
}
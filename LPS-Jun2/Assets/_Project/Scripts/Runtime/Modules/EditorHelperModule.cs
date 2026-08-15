using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class EditorHelperModule : ModuleBase, ITickable
{
    [Inject] private GameMachine _gameMachine;

    private PlayerPrefsInt _level = Prefs.Level;
    private float _pausedTimeScale;

    public void Tick()
    {
        if (InputH.GetKeyDown(KeyCode.W)) _gameMachine.RequestStateChange<GameWinState>();
        if (InputH.GetKeyDown(KeyCode.L)) _gameMachine.RequestStateChange<GameReviveState>();
        if (InputH.GetKeyDown(KeyCode.R)) _gameMachine.RequestStateChange<GameStartState>();

        if (InputH.GetKeyDown(KeyCode.A))
        {
            _level.Set(_level.Value - 1);
            _gameMachine.RequestStateChange<GameStartState>();
        }
        if (InputH.GetKeyDown(KeyCode.D))
        {
            _level.Set(_level.Value + 1);
            _gameMachine.RequestStateChange<GameStartState>();
        }

        if (InputH.GetKeyDown(KeyCode.UpArrow)) Time.timeScale *= 2f;
        if (InputH.GetKeyDown(KeyCode.DownArrow)) Time.timeScale /= 2f;
        if (InputH.GetKeyDown(KeyCode.LeftArrow)) Time.timeScale = 1f;
        if (InputH.GetKeyDown(KeyCode.P))
        {
            if (Time.timeScale > 0f)
            {
                _pausedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = _pausedTimeScale;
            }
        }
        if (InputH.GetKeyDown(KeyCode.O)) StepOneFrame();
    }

    private async UniTaskVoid StepOneFrame()
    {
        Time.timeScale = 1;
        await UniTask.NextFrame();
        Time.timeScale = 0;
    }
}

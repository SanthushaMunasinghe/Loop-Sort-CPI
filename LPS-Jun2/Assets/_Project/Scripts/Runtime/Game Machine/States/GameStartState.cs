using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using VContainer;

public sealed class GameStartState : GameStateBase
{
    [Inject] private SceneModule _sceneModule;
    [Inject] private TimeModule _timeModule;
    [Inject] private AudioModule _audioModule;
    [Inject] private SheetContainer _sheetContainer;
    [Inject] private Monitors _monitors;

    private readonly PlayerPrefsInt _level = Prefs.Level;

    public override void OnEnter()
    {
        base.OnEnter();

        OnEnterAsync();
    }

    private async UniTaskVoid OnEnterAsync()
    {
        _timeModule.Pause();
        await _sceneModule.Load();
        Machine.RequestStateChange<GamePlayingState>();
        _timeModule.Resume();

        var maxLevelOffset = _sheetContainer.Constants.GetInt(GameConstant.MaxLevelOffset);
        var level = _sheetContainer.Levels.GetCurrent(maxLevelOffset);
        var music = level.Theme is ThemeType.Hard or ThemeType.SuperHard
            ? _audioModule.Sounds.HardLevelMusic
            : _audioModule.Sounds.PlayingMusic;
        _audioModule.ChangeMusic(music);
        _audioModule.RestartMusic();

        var winMonitor = _monitors.Get<WinMonitor>();
        winMonitor.SetResults(default);

        TrackStartGame(level);
    }

    private async UniTaskVoid TrackStartGame(LevelSheet.Level level)
    {
        // delay for carrier & block initialization
        await UniTask.DelayFrame(5, cancellationToken: _sceneModule.SceneLoadToken);
    }
}
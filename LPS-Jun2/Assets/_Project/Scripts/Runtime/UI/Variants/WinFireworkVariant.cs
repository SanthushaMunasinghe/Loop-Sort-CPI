using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;
using VContainer;

public sealed class WinFireworkVariant : MonitorVariantBase
{
    [Inject] private GameMachine _gameMachine;
    [Inject] private AudioModule _audioModule;
    [Inject] private WinMonitor _winMonitor;

    public override void Setup()
    {
        base.Setup();

        GetButton(ButtonRole.Next).onClick.AddListener(OnNextClicked);
    }

    public override void OnActivated()
    {
        base.OnActivated();

        // GetText(TextRole.Reward).SetText(_winMonitor.Results.Gold.ToString());
        GetText(TextRole.CurrentLevel).SetText($"LEVEL {_winMonitor.Results.Level.ToString()}");
        try
        {
            _ = HandleMotion();
        }
        catch
        {
            // ignored
        }
    }

    private void OnNextClicked()
    {
        _gameMachine.RequestStateChange(nameof(GameStartState));
    }

    private async UniTaskVoid HandleMotion()
    {
        _audioModule.GetPlayer().WithVolumeScale(.9f).Play(_audioModule.Sounds.Win);

        SetState(StateRole.Text);
        var completedTextT = GetObject(ObjectRole.LevelCompleted).transform;
        completedTextT.localScale = Vector3H.AlmostZero;

        await UniTask.Delay(300, ignoreTimeScale: true);

        _audioModule.GetPlayer().WithVolumeScale(.9f).Play(_audioModule.Sounds.WinText);
        LMotion.Create(Vector3H.AlmostZero, Vector3.one, .8f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.InOutBack)
            .BindToLocalScaleNonNegative(completedTextT)
            .AddTo(this);

        await UniTask.Delay(1400, ignoreTimeScale: true);

        LMotion.Create(Vector3.one, Vector3H.AlmostZero, .5f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.InOutBack)
            .BindToLocalScaleNonNegative(completedTextT)
            .AddTo(this);

        await UniTask.Delay(600, ignoreTimeScale: true);

        SetState(StateRole.Reward);
        _audioModule.GetPlayer().WithVolumeScale(.9f).Play(_audioModule.Sounds.WinReward);
    }
}
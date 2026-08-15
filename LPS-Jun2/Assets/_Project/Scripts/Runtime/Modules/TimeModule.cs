using System;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;

public sealed class TimeModule : ModuleBase, IDisposable
{
    private IDisposable _timeDisposable;

    public UniTask Pause(float duration = 0f, float delayInSeconds = 0f) =>
        AdjustTimeScale(0f, duration, delayInSeconds);

    public UniTask Resume(float duration = 0f, float delayInSeconds = 0f) =>
        AdjustTimeScale(1f, duration, delayInSeconds);

    public async UniTask AdjustTimeScale(float newTimeScale, float duration = 0f, float delayInSeconds = 0f)
    {
        if (delayInSeconds > 0)
            await UniTask.Delay(TimeSpan.FromSeconds(delayInSeconds));

        _timeDisposable?.Dispose();

        MotionHandle timeMotion = default;
        if (duration > 0)
        {
            var currentTimeScale = Time.timeScale;
            timeMotion = LMotion.Create(currentTimeScale, newTimeScale, duration)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToTimeScale();
        }
        else
        {
            Time.timeScale = newTimeScale;
        }

        _timeDisposable = timeMotion.ToDisposable();
        await timeMotion.ToUniTask();
    }

    public void Dispose()
    {
        _timeDisposable?.Dispose();
    }
}
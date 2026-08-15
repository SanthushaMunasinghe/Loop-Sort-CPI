using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using StatefulUI.Runtime.Core;
using StatefulUISupport.Scripts.Components;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

public static class UIMotionExtensions
{
    public static MotionHandle ApplyTempColor(Graphic graphic, Color color, float duration)
    {
        var mainColor = graphic.color;
        var tempHandle = LMotion.Create(mainColor, color, .25f).BindToColor(graphic);
        var mainHandle = LMotion.Create(color, mainColor, .25f).BindToColor(graphic);
        return LSequence.Create().Append(tempHandle).AppendInterval(duration).Append(mainHandle).Run().AddTo(graphic);
    }

    public static MotionHandle ApplyTempColor(this StatefulComponent view, TextRole role, Color color, float duration)
    {
        var tmp = view.GetText(role).TMP;
        return ApplyTempColor(tmp, color, duration);
    }

    public static MotionHandle ApplyTempColor(this StatefulComponent view, ImageRole role, Color color, float duration)
    {
        var image = view.GetImage(role);
        return ApplyTempColor(image, color, duration);
    }

    public static MotionHandle ApplyTempColor(this StatefulComponent view, ButtonRole role, Color color, float duration)
    {
        var image = view.GetButton(role).GetComponent<Image>();
        return ApplyTempColor(image, color, duration);
    }

    public static MotionHandle ApplyPunch(this StatefulComponent view, ButtonRole role)
    {
        var t = view.GetButton(role).transform;
        return LMotion.Punch.Create(t.localScale, Vector3.one * .05f, .3f)
            .BindToLocalScale(t)
            .AddTo(view);
    }

    public static async UniTask ApplyOrderedOpen(this StatefulComponent view, ObjectRole role, float duration, CancellationToken token = default)
    {
        var t = view.GetObject(role).Object.transform;
        var childCount = t.childCount;
        var delay = 0f;
        using var p = ListPool<UniTask>.Get(out var tasks);
        for (var i = 0; i < childCount; i++)
        {
            var child = t.GetChild(i);
            child.transform.localScale = Vector3H.AlmostZero;
            var task = LMotion.Create(Vector3H.AlmostZero, Vector3.one, duration)
                .WithEase(Ease.InOutBack)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithDelay(delay)
                .BindToLocalScaleNonNegative(child)
                .ToUniTask(cancellationToken: token);
            delay += duration;
            tasks.Add(task);
        }

        await UniTask.WhenAll(tasks);
    }
}
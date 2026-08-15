using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using StatefulUI.Runtime.Core;
using StatefulUISupport.Scripts.Components;
using UnityEngine;

public partial class PlayingMonitor
{
    private async UniTaskVoid ApplyLevelPreviewMotion(StatefulComponent view)
    {
        view.gameObject.SetActive(true);

        var canvasGroup = view.GetComponent<CanvasGroup>();
        LMotion.Create(0f, 1f, .3f)
            .BindToAlpha(canvasGroup);

        var rootT = view.GetObject(ObjectRole.Root).Transform as RectTransform;
        LMotion.Create(Vector3H.AlmostZero, Vector3.one, .7f)
            .WithEase(Ease.InOutBack)
            .BindToLocalScale(rootT);

        LMotion.Create(Vector2.up * 1000f, Vector2.zero, .6f)
            .WithEase(Ease.InOutBack)
            .BindToAnchoredPosition(rootT);

        await UniTask.Delay(700);

        // LMotion.Punch.Create(Vector3.one, Vector3.one * .25f, .25f)
        //     .WithDampingRatio(1f)
        //     .WithFrequency(1)
        //     .BindToLocalScale(rootT);

        await UniTask.Delay(500);

        LMotion.Create(Vector2.zero, Vector2.up * 1000f, .6f)
            .WithEase(Ease.InOutBack)
            .BindToAnchoredPosition(rootT);

        LMotion.Create(1f, 0f, .4f)
            .BindToAlpha(canvasGroup);

        LMotion.Create(Vector3.one, Vector3H.AlmostZero, .6f)
            .WithEase(Ease.InBack)
            .BindToLocalScale(rootT);

        await UniTask.Delay(600);

        view.gameObject.SetActive(false);
    }
}
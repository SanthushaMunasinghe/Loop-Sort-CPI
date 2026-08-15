using System.Collections.Generic;
using AssetKits.ParticleImage;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using Spine.Unity;
using StatefulUISupport.Scripts.Components;
using UnityEngine;
using VContainer;

public sealed class GiftBoxElement : ElementBase
{
    [Inject] private Features _features;
    [Inject] private SheetContainer _sheetContainer;
    [Inject] private HapticModule _hapticModule;
    [Inject] private AudioModule _audioModule;

    public struct Item
    {
        public Sprite Icon;
        public string DisplayName;
    }

    public async UniTaskVoid ApplyProgressMotion()
    {
        var currentProgress = _sheetContainer.Features.GetCurrentProgress();
        if (!currentProgress.HasProgress) return;

        var data = _features.Get(currentProgress.NextFeature.Id);

        var itemContainer = GetContainer(ContainerRole.Item);
        itemContainer.Clear();
        var itemView = itemContainer.AddStatefulComponent();
        var iconImage = itemView.GetImage(ImageRole.Icon);
        iconImage.sprite = data.Icon;
        iconImage.transform.localScale = Vector3.one * data.IconScaleMultiplier;
        itemView.SetText(TextRole.Count, string.Empty);
        var featureName = currentProgress.NextFeature.Id.ToString().ToLowerInvariant();
        var displayName = Localization.Get($"feature_{featureName}_name");
        SetText(TextRole.Feature, displayName);

        var particle = GetObject(ObjectRole.Particle).GetComponent<ParticleImage>();
        particle.Clear();

        var featureText = GetText(TextRole.Feature).TMP;

        itemContainer.gameObject.SetActive(false);
        featureText.gameObject.SetActive(false);

        var skeleton = GetObject(ObjectRole.Skeleton);
        skeleton.gameObject.SetActive(false);

        var fill = GetObject(ObjectRole.Fill);
        fill.SetActive(true);

        var fillImage = GetImage(ImageRole.Fill);
        var progressText = GetText(TextRole.Progress).TMP;
        progressText.transform.localScale = Vector3.one;

        // LMotion.Punch.Create(Vector3.one, Vector3.one * .15f, .2f)
        //     .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
        //     .WithDelay(.6f)
        //     .WithFrequency(1)
        //     .BindToLocalScale(fill.transform);

        LMotion.Create(currentProgress.FromPercent, currentProgress.ToPercent, 1f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithDelay(.7f)
            .Bind(x =>
            {
                var text = $"{Mathf.CeilToInt(x * 100)} %";
                fillImage.fillAmount = x;
                progressText.SetText(text);
            });

        await UniTask.Delay(1700, ignoreTimeScale: true);

        var unlocked = currentProgress.ToPercent >= 1f;
        if (unlocked) ApplyUnlockMotion().Forget();
    }

    public async UniTask ApplyCustomMotion(List<Item> items)
    {
        var itemContainer = GetContainer(ContainerRole.Item);
        itemContainer.Clear();
        foreach (var item in items)
        {
            var itemView = itemContainer.AddStatefulComponent();
            itemView.SetImage(ImageRole.Icon, item.Icon);
            itemView.SetText(TextRole.Count, item.DisplayName);
        }

        SetText(TextRole.Feature, string.Empty);

        var featureText = GetText(TextRole.Feature).TMP;

        itemContainer.gameObject.SetActive(false);
        featureText.gameObject.SetActive(false);

        var gift = GetObject(ObjectRole.Skeleton);
        gift.SetActive(false);

        var fill = GetObject(ObjectRole.Fill);
        fill.SetActive(true);

        var progressText = GetText(TextRole.Progress).TMP;
        progressText.gameObject.SetActive(false);

        await ApplyUnlockMotion();
    }

    private async UniTask ApplyUnlockMotion()
    {
        var fill = GetObject(ObjectRole.Fill);
        var progressText = GetText(TextRole.Progress).TMP;
        var skeleton = GetObject(ObjectRole.Skeleton).GetComponent<SkeletonGraphic>();
        var skeletonT = skeleton.transform;
        var featureText = GetText(TextRole.Feature).TMP;
        var particle = GetObject(ObjectRole.Particle).GetComponent<ParticleImage>();

        particle.Clear();
        _hapticModule.PlaySuccess();

        LMotion.Create(Vector3.one, Vector3H.AlmostZero, .2f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithDelay(.2f)
            .BindToLocalScale(progressText.transform);

        await UniTask.Delay(400, ignoreTimeScale: true);

        fill.SetActive(false);
        skeleton.gameObject.SetActive(true);
        skeletonT.localScale = Vector3.one;

        var trackEntry = skeleton.AnimationState.SetAnimation(0, "open", loop: false);
        await trackEntry.WaitUntilFirstEvent();

        _hapticModule.PlaySuccess();
        _audioModule.GetPlayer().Play(_audioModule.Sounds.GiftUnlock);
        particle.Play();

        var itemContainer = GetContainer(ContainerRole.Item);
        itemContainer.gameObject.SetActive(true);
        LMotion.Create(Vector3H.AlmostZero, Vector3.one, .4f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.OutBack)
            .BindToLocalScale(itemContainer.transform);

        featureText.gameObject.SetActive(true);
        LMotion.Create(Vector3H.AlmostZero, Vector3.one, .4f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithDelay(.3f)
            .WithEase(Ease.OutBack)
            .BindToLocalScale(featureText.transform);

        await trackEntry.WaitUntilComplete();

        LMotion.Create(Vector3.one, Vector3H.AlmostZero, .4f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithDelay(.2f)
            .BindToLocalScale(skeletonT);
    }
}
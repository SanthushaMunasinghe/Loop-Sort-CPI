using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class FeatureTutorial : TutorialBase
{
    [Inject] private Features _features;
    [Inject] private SheetContainer _sheetContainer;

    private FeatureType _unlockedFeature;

    public override void SetCustomScope(LifetimeScope scope)
    {
        base.SetCustomScope(scope);

        _unlockedFeature = scope.Container.Resolve<FeatureType>();
    }

    public override async UniTask<bool> Play()
    {
        var data = _features.Get(_unlockedFeature);
        var feature = _sheetContainer.Features[_unlockedFeature];

        var featureName = feature.Id.ToString().ToLowerInvariant();
        var title = Localization.Get($"feature_{featureName}_name");
        SetText(TextRole.Title, title);
        var desc = Localization.Get($"feature_{featureName}_desc");
        SetText(TextRole.Desc, desc);
        var iconImage = GetImage(ImageRole.Icon);
        iconImage.sprite = data.Icon;
        iconImage.transform.localScale = Vector3.one * data.IconScaleMultiplier;

        var button = GetButton(ButtonRole.Claim);
        Monitors.SetClickableArea(button.transform as RectTransform);
        await button.OnClickAsync();

        return false;
    }
}
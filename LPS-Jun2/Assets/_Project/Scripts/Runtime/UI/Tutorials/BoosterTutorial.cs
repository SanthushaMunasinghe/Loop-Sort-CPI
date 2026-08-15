using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class BoosterTutorial : TutorialBase
{
    [Inject] private Boosters _boosters;
    [Inject] private SheetContainer _sheetContainer;

    private BoosterType _unlockedBooster;

    public override void SetCustomScope(LifetimeScope scope)
    {
        base.SetCustomScope(scope);

        _unlockedBooster = scope.Container.Resolve<BoosterType>();
    }

    public override async UniTask<bool> Play()
    {
        var data = _boosters.Get(_unlockedBooster);
        var booster = _sheetContainer.Boosters[_unlockedBooster];

        SetText(TextRole.Title, booster.Id.ToLocalizedName());
        SetText(TextRole.Desc, booster.Id.ToLocalizedDesc());
        SetImage(ImageRole.Background, data.Background);
        SetImage(ImageRole.Icon, data.Icon);

        var button = GetButton(ButtonRole.Claim);
        Monitors.SetClickableArea(button.transform as RectTransform);
        await button.OnClickAsync();

        return false;
    }
}

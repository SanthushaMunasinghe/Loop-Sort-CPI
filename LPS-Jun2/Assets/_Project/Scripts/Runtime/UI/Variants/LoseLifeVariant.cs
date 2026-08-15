using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using VContainer;

public sealed class LoseLifeVariant : MonitorVariantBase
{
    [Inject] private GameMachine _gameMachine;

    private VerticalLayoutGroup _panelLayoutGroup;

    public override void OnActivated()
    {
        base.OnActivated();

        GetObject(ObjectRole.Life).SetActive(false);
        ApplyActivateMotion().Forget();
    }

    public override void Setup()
    {
        base.Setup();

        _panelLayoutGroup = GetComponentInChildren<VerticalLayoutGroup>();
    }

    private async UniTaskVoid ApplyActivateMotion()
    {
        _panelLayoutGroup.enabled = true;
        await UniTask.NextFrame();
        _panelLayoutGroup.enabled = false;

        View.ApplyOrderedOpen(ObjectRole.Panel, .35f);
    }
}

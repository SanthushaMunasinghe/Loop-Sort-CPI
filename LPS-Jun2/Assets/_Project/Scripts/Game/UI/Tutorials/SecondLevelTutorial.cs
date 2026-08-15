using Cysharp.Threading.Tasks;
using Scellecs.Morpeh;
using UnityEngine;
using VContainer;

public sealed class SecondLevelTutorial : TutorialBase
{
    [Inject] private SceneModule _sceneModule;
    [Inject] private SheetContainer _sheetContainer;
    [Inject] private Camera _camera;

    public override async UniTask<bool> Play()
    {
        var resolver = _sceneModule.Scope.Container;
        var world = resolver.Resolve<World>();
        var stash = world.GetStash<BehaviourView<Carrier>>();
        var firstCarrier = stash.data[0].Behaviour;

        var cameraT = _camera.transform;
        var carrierT = firstCarrier.Pivot;
        var center = carrierT.position.WithX(cameraT.position.x);
        var textPosition = carrierT.position.WithX(cameraT.position.x) + Vector3.back * 3.25f;

        Interaction.SetRestrictedInteractable(firstCarrier);

        foreach (Carrier carrier in stash) carrier.Highlight.SetActive(true);
        Highlight.ShowMask(alpha: .75f);
        // Highlight.CreateWorldMask(center + Vector3.forward * 3.2f, new Vector2(7.205458f, 5.938236f));

        await Interaction.OnInteractAsync();

        Interaction.SetRestrictedInteractable(null);
        Highlight.ClearMask();

        var firstTargetCarrier = stash.data[1].Behaviour;
        await UniTask.WaitUntil(() => firstTargetCarrier.IsFull());

        var secondCarrier = stash.data[2].Behaviour;
        Interaction.SetRestrictedInteractable(secondCarrier);
        await Interaction.OnInteractAsync();

        var secondTargetCarrier = stash.data[0].Behaviour;
        await UniTask.WaitUntil(() => secondTargetCarrier.IsFull());

        return false;
    }
}
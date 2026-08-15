using Cysharp.Threading.Tasks;
using Scellecs.Morpeh;
using UnityEngine;
using VContainer;

public sealed class FirstLevelTutorial : TutorialBase
{
    [Inject] private SceneModule _sceneModule;
    [Inject] private SheetContainer _sheetContainer;
    [Inject] private Camera _camera;

    public override async UniTask<bool> Play()
    {
        var resolver = _sceneModule.Scope.Container;
        var world = resolver.Resolve<World>();
        var stash = world.GetStash<BehaviourView<Carrier>>();
        var clickCarrier = stash.data[0].Behaviour;
        var targetCarrier = stash.data[1].Behaviour;

        Interaction.SetRestrictedInteractable(clickCarrier);

        foreach (Carrier carrier in stash) carrier.Highlight.SetActive(true);
        Highlight.ShowMask(alpha: .75f);
        // Highlight.CreateWorldMask(center + Vector3.forward * 3.2f, new Vector2(5.422925f, 5.938236f));

        await Interaction.OnInteractAsync();

        Highlight.ClearMask();

        await UniTask.WaitUntil(() => targetCarrier.IsFull());

        return false;
    }
}
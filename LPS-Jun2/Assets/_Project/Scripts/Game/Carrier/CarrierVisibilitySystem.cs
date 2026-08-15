using System;
using MessagePipe;
using Scellecs.Morpeh;
using VContainer;

public sealed class CarrierVisibilitySystem : SystemBase
{
    [Inject] private LevelSheet.Level _level;
    [Inject] private ISubscriber<LevelBuildCompleteMessage> _levelBuildCompleteSub;

    private Stash<BehaviourView<Carrier>> _carriers;

    public override void OnAwake()
    {
        base.OnAwake();

        _carriers = World.GetStash<BehaviourView<Carrier>>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _levelBuildCompleteSub.Subscribe(OnLevelBuildComplete).AddTo(bag);
    }

    private void OnLevelBuildComplete(LevelBuildCompleteMessage obj)
    {
        UpdateCarrierVisibility();
    }

    private void UpdateCarrierVisibility()
    {
        if (Prefs.DisableCapacity.Value) return;

        var carriersRef = _level.Carriers.Ref;
        foreach (Carrier carrier in _carriers)
        {
            var active = carriersRef.Colors.ContainsKey(carrier.Type);
            carrier.gameObject.SetActive(active);
        }
    }
}
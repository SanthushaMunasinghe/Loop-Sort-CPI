using VContainer;

public sealed class CapacityBooster : BoosterBase
{
    [Inject] private CapacityBoosterSystem _capacityBoosterSystem;

    protected override void Start()
    {
        base.Start();

        Apply();
    }

    public override bool CanSelect(IObjectResolver resolver)
    {
        return resolver.TryResolve<CapacityBoosterSystem>(out var system) && system.HasCapacityCarrier();
    }

    private void Apply()
    {
        _capacityBoosterSystem.IncreaseCapacity();
        System.Complete();
    }
}
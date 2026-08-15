using VContainer;

public sealed class UndoBooster : BoosterBase
{
    [Inject] private UndoBoosterSystem _undoBoosterSystem;

    protected override void Start()
    {
        base.Start();

        Apply();
    }

    public override bool CanSelect(IObjectResolver resolver)
    {
        return resolver.TryResolve<UndoBoosterSystem>(out var system) && system.CanUndo();
    }

    private void Apply()
    {
        _undoBoosterSystem.Undo();
        System.Complete();
    }
}
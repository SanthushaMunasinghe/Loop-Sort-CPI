using System.Collections.Generic;
using VContainer;

public sealed class GridContainer : GameBehaviourBase
{
    [Inject] private GridConfig _config;
    [Inject] private LevelSheet.Level _level;

    private readonly List<Grid> _grids = new();

    protected override void Awake()
    {
        base.Awake();

        var grids = GetComponentsInChildren<Grid>();
        InitializeGrids(grids);
    }

    private void InitializeGrids(Grid[] grids)
    {
        foreach (var grid in grids)
        {
            var layoutsRef = _level.Grids.Ref;
            var layout = layoutsRef.GetLayout(grid.LayoutType);
            grid.Initialize(layout);
            _grids.Add(grid);
        }
    }

    public List<Grid> GetGrids()
    {
        return _grids;
    }
}
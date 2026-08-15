using System.Collections.Generic;
using UnityEngine;

public sealed class RectCell : CellBase
{
    private readonly List<CellBase> _neighborCells = new();

    private static readonly Vector2Int[] NeighborDirections =
    {
        new(0, 1),
        new(0, -1),
        new(1, 0),
        new(-1, 0),
    };

    public override List<CellBase> GetNeighbors()
    {
        if (_neighborCells.Count == 0)
        {
            foreach (var direction in NeighborDirections)
            {
                var neighbor = GetNeighborAt(direction);
                if (neighbor == null) continue;
                _neighborCells.Add(neighbor);
            }
        }

        return _neighborCells;
    }
}
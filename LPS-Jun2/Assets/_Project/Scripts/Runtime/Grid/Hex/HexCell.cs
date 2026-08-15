using System.Collections.Generic;
using UnityEngine;

public sealed partial class HexCell : CellBase
{
    private readonly List<CellBase> _neighborCells = new();

    private static readonly Vector3Int[] AxialNeighborDirections =
    {
        new(-1, 1, 0),
        new(0, 1, -1),
        new(1, 0, -1),
        new(1, -1, 0),
        new(0, -1, 1),
        new(-1, 0, 1),
    };

    public override List<CellBase> GetNeighbors()
    {
        if (_neighborCells.Count == 0)
        {
            foreach (var direction in AxialNeighborDirections)
            {
                var axialCoordinate = ToAxialCoordinate();
                var coordinate = AxialToOffset(axialCoordinate + direction);
                var neighbor = Grid.GetCellAt(coordinate);
                if (neighbor == null) continue;
                _neighborCells.Add(neighbor);
            }
        }

        return _neighborCells;
    }
}
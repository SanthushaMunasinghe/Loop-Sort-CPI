using UnityEngine;

public partial class HexCell
{
    public Vector3Int ToAxialCoordinate()
    {
        var axial = new Vector3Int
        {
            x = Coordinate.x,
            y = Coordinate.y - Coordinate.x / 2,
        };
        axial.z = -axial.y - axial.x;
        return axial;
    }

    public static Vector2Int AxialToOffset(Vector3Int axial)
    {
        var y = axial.y + axial.x / 2;
        return new Vector2Int(axial.x, y);
    }
}
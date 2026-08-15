using UnityEngine;

public sealed class GridConfig : Data
{
    public GridType Type;
    public Grid GridPrefab;
    public CellBase CellPrefab;
    public float OuterRadius;
    public float InnerRadius => Type == GridType.Hex ? OuterRadius * .866025404f : OuterRadius;
    public Vector2Int MinSize;
    public Vector2Int MaxSize = new(100, 100);
}

public enum GridType
{
    Rect = 0,
    Hex = 1,
}
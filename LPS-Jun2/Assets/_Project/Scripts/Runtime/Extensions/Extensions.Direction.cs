using UnityEngine;

public static partial class Extensions
{
    public static Vector2Int ToVector2Int(this Direction direction)
    {
        return direction switch
        {
            Direction.Left => new Vector2Int(-1, 0),
            Direction.Right => new Vector2Int(1, 0),
            Direction.Up => new Vector2Int(0, 1),
            Direction.Down => new Vector2Int(0, -1),
            Direction.Forward => new Vector2Int(0, 1),
            Direction.Backward => new Vector2Int(0, -1),
            _ => Vector2Int.zero,
        };
    }

    public static Quaternion ToLookRotation(this Direction direction)
    {
        return Quaternion.LookRotation(direction switch
        {
            Direction.Left => Vector3.left,
            Direction.Right => Vector3.right,
            Direction.Up => Vector3.up,
            Direction.Down => Vector3.down,
            Direction.Forward => Vector3.forward,
            Direction.Backward => Vector3.back,
            _ => Vector3.forward,
        });
    }
}

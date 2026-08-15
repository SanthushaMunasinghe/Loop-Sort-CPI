using UnityEngine;

public struct SheetVector2
{
    public float X { get; private set; }
    public float Y { get; private set; }

    public static implicit operator Vector2(SheetVector2 vector) => new(vector.X, vector.Y);
}
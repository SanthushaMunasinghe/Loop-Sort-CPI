using System.Runtime.CompilerServices;
using UnityEngine;

public static partial class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Add(this Vector2 v, Vector2 direction, float value) => v + direction.normalized * value;

    public static Vector2 AddX(this Vector2 v, float value) => v.Add(Vector2.right, value);

    public static Vector2 AddY(this Vector2 v, float value) => v.Add(Vector2.up, value);

    public static Vector2 WithX(this Vector2 v, float value) => new(value, v.y);

    public static Vector2 WithY(this Vector2 v, float value) => new(v.x, value);

    public static float DistanceTo(this Vector2 from, Vector2 to) => Vector2.Distance(from, to);
}
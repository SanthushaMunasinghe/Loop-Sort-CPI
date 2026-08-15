using System.Runtime.CompilerServices;
using UnityEngine;

public static partial class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Add(this Vector3 v, Vector3 direction, float value) => v + direction.normalized * value;

    public static Vector3 AddX(this Vector3 v, float value) => v.Add(Vector3.right, value);

    public static Vector3 AddY(this Vector3 v, float value) => v.Add(Vector3.up, value);

    public static Vector3 AddZ(this Vector3 v, float value) => v.Add(Vector3.forward, value);

    public static Vector3 WithX(this Vector3 v, float value) => new(value, v.y, v.z);

    public static Vector3 WithY(this Vector3 v, float value) => new(v.x, value, v.z);

    public static Vector3 WithZ(this Vector3 v, float value) => new(v.x, v.y, value);

    public static float DistanceTo(this Vector3 from, Vector3 to) => Vector3.Distance(from, to);
}
using System.Runtime.CompilerServices;
using UnityEngine;

public static partial class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 AsVector3(this Vector3Int v) => new(v.x, v.y, v.z);
}
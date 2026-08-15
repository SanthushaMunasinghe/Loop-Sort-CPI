using System.Collections.Generic;
using UnityEngine;

public static partial class Extensions
{
    public static float DistanceTo(this Transform from, Transform to) => from.position.DistanceTo(to.position);

    public static Vector3 GetCenterPoint<T>(this List<T> components) where T : Component
    {
        var center = Vector3.zero;
        foreach (var component in components)
        {
            var t = component.transform;
            center += t.position;
        }
        return center / components.Count;
    }

    /// <summary>
    /// Scales the target around an arbitrary point by scaleFactor.
    /// This is relative scaling, meaning using scale Factor of Vector3.one
    /// will not change anything and new Vector3(0.5f, 0.5f, 0.5f) will reduce
    /// the object size by half.
    /// The pivot is assumed to be the world position.
    /// Scaling is applied to localScale of target.
    /// </summary>
    /// <param name="t">The transform to scale.</param>
    /// <param name="pivot">The point to scale around in the world space.</param>
    /// <param name="scaleFactor">The factor with which the current localScale of the target will be multiplied with.</param>
    public static void ScaleAroundRelative(this Transform t, Vector3 pivot, Vector3 scaleFactor)
    {
        // pivot
        var pivotDelta = t.position - pivot;
        pivotDelta.Scale(scaleFactor);
        t.position = pivot + pivotDelta;

        // scale
        var finalScale = t.localScale;
        finalScale.Scale(scaleFactor);
        t.localScale = finalScale;
    }

    /// <summary>
    /// Scales the target around an arbitrary pivot.
    /// This is absolute scaling, meaning using for example a scale factor of
    /// Vector3.one will set the localScale of target to x=1, y=1 and z=1.
    /// The pivot is assumed to be the world position.
    /// Scaling is applied to localScale of target.
    /// </summary>
    /// <param name="t">The transform to scale.</param>
    /// <param name="pivot">The point to scale around in the world space.</param>
    /// <param name="newScale">The new localScale the target object will have after scaling.</param>
    public static void ScaleAround(this Transform t, Vector3 pivot, Vector3 newScale)
    {
        // pivot
        var pivotDelta = t.position - pivot; // diff from object pivot to desired pivot/origin
        var scaleFactor = new Vector3(
            newScale.x / t.localScale.x,
            newScale.y / t.localScale.y,
            newScale.z / t.localScale.z);
        pivotDelta.Scale(scaleFactor);
        t.position = pivot + pivotDelta;

        t.localScale = newScale;
    }
}
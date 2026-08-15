using UnityEngine;

public static partial class Extensions
{
    public static RectTransform GetRectTransform(this GameObject gameObject)
    {
        return gameObject.transform as RectTransform;
    }
}
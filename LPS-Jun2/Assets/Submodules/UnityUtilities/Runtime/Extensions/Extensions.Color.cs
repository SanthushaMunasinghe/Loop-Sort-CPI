using UnityEngine;

public static partial class Extensions
{
    public static Color ToColor(this string strColor)
    {
        var rgba = strColor.Substring(5, strColor.Length - 6).Split(", ");
        var color = new Color(float.Parse(rgba[0]), float.Parse(rgba[1]), float.Parse(rgba[2]), float.Parse(rgba[3]));
        return color;
    }
}
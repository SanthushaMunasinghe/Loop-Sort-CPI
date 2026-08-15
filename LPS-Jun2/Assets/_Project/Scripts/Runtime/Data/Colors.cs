using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class Colors : Data
{
    public GenericDictionary<ColorType, Data> Collection;

    private readonly Dictionary<Material, ColorType> _colorTypeByMaterial = new();

    public Data Get(ColorType colorType)
    {
        return Collection.TryGetValue(colorType, out var data) ? data : default;
    }

    public ColorType FindColorType(Material material)
    {
        if (_colorTypeByMaterial.TryGetValue(material, out var type))
            return type;

        foreach (var (colorType, data) in Collection)
            if (data.Material == material)
            {
                _colorTypeByMaterial[data.Material] = colorType;
                return colorType;
            }

        return default;
    }

    [Serializable]
    public struct Data
    {
        public Material Material;
        public Sprite Sprite;
        public Color Color;
    }
}

public enum BaseColor
{
    None = 0,

    Red = 1, R = 1,
    Blue = 2, B = 2,
    Green = 3, G = 3,
    Yellow = 4, Y = 4,
    Purple = 5, P = 5,
    Orange = 6, O = 6,
    Brown = 7, Br = 7,
    Pink = 8, Pnk = 8,
    Cyan = 9, C = 9,
    Magenta = 10, M = 10,
    Grey = 11, Gray = 11, Gr = 11,
    White = 12, W = 12,
    Black = 13, Bl = 13,
}

public enum ColorShade
{
    Default = 0,
    Light = 1,
    Dark = 2
}

[Serializable]
public struct ColorType : IEquatable<ColorType>
{
    [field: SerializeField] public BaseColor Base { get; private set; }
    [field: SerializeField] public ColorShade Shade { get; private set; }

    public ColorType(BaseColor baseColor, ColorShade shade = ColorShade.Default)
    {
        Base = baseColor;
        Shade = shade;
    }

    public ColorType(string color)
    {
        Shade = ColorShade.Default;

        const StringComparison comparisonType = StringComparison.OrdinalIgnoreCase;
        if (color.Contains("Light", comparisonType))
        {
            Shade = ColorShade.Light;
            color = color.Replace("Light", string.Empty, comparisonType);
        }
        else if (color.Contains("Dark", comparisonType))
        {
            Shade = ColorShade.Dark;
            color = color.Replace("Dark", string.Empty, comparisonType);
        }
        else if (color.StartsWith("L", comparisonType))
        {
            Shade = ColorShade.Light;
            color = color[1..];
        }
        else if (color.StartsWith("D", comparisonType))
        {
            Shade = ColorShade.Dark;
            color = color[1..];
        }

        Enum.TryParse<BaseColor>(color, ignoreCase: true, out var baseColor);
        Base = baseColor;
    }

    public override string ToString() => Shade == ColorShade.Default ? Base.ToString() : $"{Shade}{Base}";
    public bool Equals(ColorType other) => Base == other.Base && Shade == other.Shade;
    public static bool operator ==(ColorType left, ColorType right) => left.Equals(right);
    public static bool operator !=(ColorType left, ColorType right) => !(left == right);
    public override bool Equals(object obj) => obj is ColorType other && Equals(other);
    public override int GetHashCode()
    {
        unchecked
        {
            return ((int)Base * 397) ^ (int)Shade;
        }
    }
}

public static partial class Extensions
{
    private const int MinColor = (int)BaseColor.Red;
    private const int MaxColor = (int)BaseColor.Black;
    private const int ColorCount = MaxColor - MinColor + 1;

    public static ColorType ShiftColor(this ColorType colorType, int shift)
    {
        var wrappedBase = WrapBaseColor(colorType.Base, shift);
        return new ColorType(wrappedBase, colorType.Shade);
    }

    private static BaseColor WrapBaseColor(BaseColor baseColor, int shift)
    {
        var index = (int)baseColor - MinColor;
        var shifted = ((index + shift) % ColorCount + ColorCount) % ColorCount;
        return (BaseColor)(shifted + MinColor);
    }
}
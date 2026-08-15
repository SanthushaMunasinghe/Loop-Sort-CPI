using System;
using System.Globalization;
using UnityEngine;

public interface IPrefsSerializer<T>
{
    public string Serialize(T value);
    public T Deserialize(string serialized);
}

public class EnumSerializer<T> : IPrefsSerializer<T> where T : struct, Enum
{
    public static readonly EnumSerializer<T> Instance = new();

    public string Serialize(T value) => value.ToString("D");
    public T Deserialize(string serialized) => Enum.TryParse<T>(serialized, out var result) ? result : default;
}

public class DateTimeOffsetSerializer : IPrefsSerializer<DateTimeOffset>
{
    public static readonly DateTimeOffsetSerializer Instance = new();

    public string Serialize(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);

    public DateTimeOffset Deserialize(string serialized)
    {
        if (DateTimeOffset.TryParse(serialized, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
            return result;
        return DateTimeOffset.UtcNow;
    }
}

public class JsonSerializer<T> : IPrefsSerializer<T>
{
    public static readonly JsonSerializer<T> Instance = new();

    public string Serialize(T value) => value.ToString();

    public T Deserialize(string serialized)
    {
        if (string.IsNullOrEmpty(serialized)) return default;
        var result = JsonUtility.FromJson<T>(serialized);
        return result;
    }
}

public class TimeSpanSerializer : IPrefsSerializer<TimeSpan>
{
    public static readonly TimeSpanSerializer Instance = new();

    public string Serialize(TimeSpan value) => value.ToString("c", CultureInfo.InvariantCulture);

    public TimeSpan Deserialize(string serialized)
    {
        if (TimeSpan.TryParse(serialized, CultureInfo.InvariantCulture, out var result))
            return result;
        return TimeSpan.Zero;
    }
}
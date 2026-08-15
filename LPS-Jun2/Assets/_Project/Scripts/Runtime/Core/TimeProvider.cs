using System;

public static class TimeProvider
{
    public static DateTimeOffset UtcNow => DateTimeOffset.UtcNow.Add(Offset.Value);
    public static DateTimeOffset Now => DateTimeOffset.Now.Add(Offset.Value);
    public static PlayerPrefsT<TimeSpan> Offset = new(nameof(TimeProvider) + nameof(Offset), TimeSpanSerializer.Instance);
}
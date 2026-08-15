using System;
using System.Text;

public static partial class Extensions
{
    private static readonly StringBuilder Builder = new();

    public static string ToReadableString(this TimeSpan timeSpan)
    {
        Builder.Clear();
        if (timeSpan.Days > 0)
        {
            Builder.Append(timeSpan.Days);
            Builder.Append(Localization.Get("time_day_firstletter"));
        }

        if (timeSpan.Hours > 0)
        {
            if (Builder.Length > 0) Builder.Append(" ");
            Builder.Append(timeSpan.Hours);
            Builder.Append(Localization.Get("time_hour_firstletter"));
        }

        if (Builder.Length >= 5) return Builder.ToString();

        if (timeSpan.Minutes > 0)
        {
            if (Builder.Length > 0) Builder.Append(" ");
            Builder.Append(timeSpan.Minutes);
            Builder.Append(Localization.Get("time_minute_firstletter"));
        }

        if (Builder.Length >= 5) return Builder.ToString();

        if (timeSpan.Seconds > 0)
        {
            if (Builder.Length > 0) Builder.Append(" ");
            Builder.Append(timeSpan.Seconds);
            Builder.Append(Localization.Get("time_second_firstletter"));
        }

        return Builder.Length == 0 ? $"0{Localization.Get("time_second_firstletter")}" : Builder.ToString();
    }
}
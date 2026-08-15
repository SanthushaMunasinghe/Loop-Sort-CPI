using System;
using UnityEngine;

public struct TimeTracker : IDisposable
{
    private const string Syntax = "<b>Time Tracker</b>: {0}";
    private const string DefaultFormat = "{0:0.000}s";
    private static double CurrentTime => Time.realtimeSinceStartupAsDouble;

    private double _timestamp;
    private string _format;

    public static TimeTracker Begin(string format = null) => new()
    {
        _timestamp = CurrentTime,
        _format = string.IsNullOrEmpty(format) ? DefaultFormat : format,
    };

    public void Dispose()
    {
#if !RELEASE_BUILD
        var elapsed = CurrentTime - _timestamp;
        var format = string.Format(Syntax, _format);
        Debug.LogFormat(format, elapsed);
#endif
    }
}
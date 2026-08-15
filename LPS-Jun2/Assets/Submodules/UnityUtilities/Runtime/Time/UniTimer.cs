using UnityEngine;

public struct UniTimer
{
    private float _targetTimestamp;
    private bool _unscaledTime;
    private bool _fixedUpdate;

    private readonly float _offset;

    private UniTimer(float seconds) : this()
    {
        _offset = seconds;
        Reset();
    }

    public bool IsRunning => !IsExpired;
    public bool IsExpired => GetTime() > _targetTimestamp;
    public float RemainingTime => _targetTimestamp - GetTime();
    public float Progress => Mathf.Clamp01(1f - RemainingTime / _offset);

    private float GetTime()
    {
        if (_fixedUpdate) return _unscaledTime ? Time.fixedUnscaledTime : Time.fixedTime;
        return _unscaledTime ? Time.unscaledTime : Time.time;
    }

    public UniTimer Reset()
    {
        _targetTimestamp = GetTime() + _offset;
        return this;
    }

    public UniTimer Expire()
    {
        _targetTimestamp = 0f;
        return this;
    }

    public UniTimer SetUnscaled()
    {
        _unscaledTime = true;
        Reset();
        return this;
    }

    public UniTimer SetFixedUpdate()
    {
        _fixedUpdate = true;
        Reset();
        return this;
    }

    public static UniTimer Expired => CreateFromSeconds(0f);
    public static UniTimer CreateFromSeconds(float seconds) => new(seconds);
}
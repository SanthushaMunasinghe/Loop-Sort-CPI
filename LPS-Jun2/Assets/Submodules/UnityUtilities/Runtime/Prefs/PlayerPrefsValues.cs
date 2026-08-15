using System;
using System.Collections.Generic;
using UnityEngine;

public struct PlayerPrefsInt
{
    private static readonly Dictionary<string, int> Cache = new();

    private readonly string _key;
    private readonly int _defaultValue;

    public PlayerPrefsInt(string key, int defaultValue = 0)
    {
        _key = key;
        _defaultValue = defaultValue;
    }

    public int Value
    {
        get
        {
            if (Cache.TryGetValue(_key, out var value)) return value;
            value = PlayerPrefs.GetInt(_key, _defaultValue);
            Cache[_key] = value;
            return value;
        }
        set
        {
            Cache[_key] = value;
            PlayerPrefs.SetInt(_key, value);
        }
    }

    public void DeleteValue()
    {
        PlayerPrefs.DeleteKey(_key);
        Cache.Remove(_key);
    }

    public void Set(int value) => Value = value;
    public bool HasValue() => PlayerPrefs.HasKey(_key);
    public static implicit operator int(PlayerPrefsInt playerPrefs) => playerPrefs.Value;
    public override string ToString() => Value.ToString();
}

public struct PlayerPrefsFloat
{
    private static readonly Dictionary<string, float> Cache = new();

    private readonly string _key;
    private readonly float _defaultValue;

    public PlayerPrefsFloat(string key, float defaultValue = 0f)
    {
        _key = key;
        _defaultValue = defaultValue;
    }

    public float Value
    {
        get
        {
            if (Cache.TryGetValue(_key, out var value)) return value;
            value = PlayerPrefs.GetFloat(_key, _defaultValue);
            Cache[_key] = value;
            return value;
        }
        set
        {
            Cache[_key] = value;
            PlayerPrefs.SetFloat(_key, value);
        }
    }

    public void DeleteValue()
    {
        PlayerPrefs.DeleteKey(_key);
        Cache.Remove(_key);
    }

    public void Set(float value) => Value = value;
    public bool HasValue() => PlayerPrefs.HasKey(_key);
    public static implicit operator float(PlayerPrefsFloat playerPrefs) => playerPrefs.Value;
    public override string ToString() => Value.ToString();
}

public struct PlayerPrefsString
{
    private static readonly Dictionary<string, string> Cache = new();

    private readonly string _key;
    private readonly string _defaultValue;

    public PlayerPrefsString(string key, string defaultValue = null)
    {
        _key = key;
        _defaultValue = defaultValue;
    }

    public string Value
    {
        get
        {
            if (Cache.TryGetValue(_key, out var value)) return value;
            value = PlayerPrefs.GetString(_key, _defaultValue);
            Cache[_key] = value;
            return value;
        }
        set
        {
            Cache[_key] = value;
            PlayerPrefs.SetString(_key, value);
        }
    }

    public void DeleteValue()
    {
        PlayerPrefs.DeleteKey(_key);
        Cache.Remove(_key);
    }

    public void Set(string value) => Value = value;
    public bool HasValue() => PlayerPrefs.HasKey(_key);
    public static implicit operator string(PlayerPrefsString playerPrefs) => playerPrefs.Value;
    public override string ToString() => Value;
}

public struct PlayerPrefsBool
{
    private static readonly Dictionary<string, bool> Cache = new();

    private readonly string _key;
    private readonly bool _defaultValue;

    public PlayerPrefsBool(string key, bool defaultValue = false)
    {
        _key = key;
        _defaultValue = defaultValue;
    }

    public bool Value
    {
        get
        {
            if (Cache.TryGetValue(_key, out var value)) return value;
            value = PlayerPrefs.GetInt(_key, _defaultValue ? 1 : 0) == 1;
            Cache[_key] = value;
            return value;
        }
        set
        {
            Cache[_key] = value;
            PlayerPrefs.SetInt(_key, value ? 1 : 0);
        }
    }

    public void DeleteValue()
    {
        PlayerPrefs.DeleteKey(_key);
        Cache.Remove(_key);
    }

    public void Set(bool value) => Value = value;
    public bool HasValue() => PlayerPrefs.HasKey(_key);
    public static implicit operator bool(PlayerPrefsBool playerPrefs) => playerPrefs.Value;
    public override string ToString() => Value.ToString();
}

public struct PlayerPrefsEnum<T> where T : struct, Enum
{
    private readonly PlayerPrefsT<T> _playerPrefsT;

    public PlayerPrefsEnum(string key, T defaultValue = default)
    {
        _playerPrefsT = new PlayerPrefsT<T>(key, EnumSerializer<T>.Instance, defaultValue);
    }

    public T Value
    {
        get => _playerPrefsT.Value;
        set => _playerPrefsT.Set(value);
    }

    public void Set(T value) => Value = value;
    public bool HasValue() => _playerPrefsT.HasValue();
    public void DeleteValue() => _playerPrefsT.DeleteValue();
    public static implicit operator T(PlayerPrefsEnum<T> playerPrefs) => playerPrefs.Value;
    public override string ToString() => Value.ToString();
}

public struct PlayerPrefsT<T> where T : struct
{
    private static readonly Dictionary<string, T> Cache = new();

    private readonly string _key;
    private readonly T _defaultValue;
    private readonly IPrefsSerializer<T> _serializer;

    public PlayerPrefsT(string key, IPrefsSerializer<T> serializer, T defaultValue = default)
    {
        _key = key;
        _serializer = serializer;
        _defaultValue = defaultValue;
    }

    public T Value
    {
        get
        {
            if (Cache.TryGetValue(_key, out var value)) return value;
            if (_serializer == null) Debug.LogWarning($"[PlayerPrefsT] serializer not set for type ({typeof(T)})");
            var serialized = PlayerPrefs.GetString(_key, _defaultValue.ToString());
            value = _serializer?.Deserialize(serialized) ?? _defaultValue;
            Cache[_key] = value;
            return value;
        }
        set
        {
            Cache[_key] = value;
            var serialized = _serializer?.Serialize(value) ?? value.ToString();
            PlayerPrefs.SetString(_key, serialized);
        }
    }

    public void DeleteValue()
    {
        PlayerPrefs.DeleteKey(_key);
        Cache.Remove(_key);
    }

    public void Set(T value) => Value = value;
    public bool HasValue() => PlayerPrefs.HasKey(_key);
    public static implicit operator T(PlayerPrefsT<T> playerPrefs) => playerPrefs.Value;
    public override string ToString() => _serializer?.Serialize(Value) ?? Value.ToString();
}
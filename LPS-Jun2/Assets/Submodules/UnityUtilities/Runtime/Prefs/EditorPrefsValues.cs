#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public struct EditorPrefsInt
{
    private readonly string _key;
    private readonly int _defaultValue;

    public EditorPrefsInt(string key, int defaultValue = 0)
    {
        _key = key;
        _defaultValue = defaultValue;
    }

    public int Value
    {
        get => EditorPrefs.GetInt(_key, _defaultValue);
        set => EditorPrefs.SetInt(_key, value);
    }

    public void Set(int value) => Value = value;
    public bool HasValue() => EditorPrefs.HasKey(_key);
    public void DeleteValue() => EditorPrefs.DeleteKey(_key);
    public static implicit operator int(EditorPrefsInt editorPrefs) => editorPrefs.Value;
    public override string ToString() => Value.ToString();
}

public struct EditorPrefsFloat
{
    private readonly string _key;
    private readonly float _defaultValue;

    public EditorPrefsFloat(string key, float defaultValue = 0f)
    {
        _key = key;
        _defaultValue = defaultValue;
    }

    public float Value
    {
        get => EditorPrefs.GetFloat(_key, _defaultValue);
        set => EditorPrefs.SetFloat(_key, value);
    }

    public void Set(float value) => Value = value;
    public bool HasValue() => EditorPrefs.HasKey(_key);
    public void DeleteValue() => EditorPrefs.DeleteKey(_key);
    public static implicit operator float(EditorPrefsFloat editorPrefs) => editorPrefs.Value;
    public override string ToString() => Value.ToString();
}

public struct EditorPrefsString
{
    private readonly string _key;
    private readonly string _defaultValue;

    public EditorPrefsString(string key, string defaultValue = null)
    {
        _key = key;
        _defaultValue = defaultValue;
    }

    public string Value
    {
        get => EditorPrefs.GetString(_key, _defaultValue);
        set => EditorPrefs.SetString(_key, value);
    }

    public void Set(string value) => Value = value;
    public bool HasValue() => EditorPrefs.HasKey(_key);
    public void DeleteValue() => EditorPrefs.DeleteKey(_key);
    public static implicit operator string(EditorPrefsString editorPrefs) => editorPrefs.Value;
    public override string ToString() => Value;
}

public struct EditorPrefsBool
{
    private readonly string _key;
    private readonly bool _defaultValue;

    public EditorPrefsBool(string key, bool defaultValue = false)
    {
        _key = key;
        _defaultValue = defaultValue;
    }

    public bool Value
    {
        get => EditorPrefs.GetBool(_key, _defaultValue);
        set => EditorPrefs.SetBool(_key, value);
    }

    public void Set(bool value) => Value = value;
    public bool HasValue() => EditorPrefs.HasKey(_key);
    public void DeleteValue() => EditorPrefs.DeleteKey(_key);
    public static implicit operator bool(EditorPrefsBool editorPrefs) => editorPrefs.Value;
    public override string ToString() => Value.ToString();
}

public struct EditorPrefsEnum<T> where T : struct, Enum
{
    private readonly EditorPrefsT<T> _editorPrefsT;

    public EditorPrefsEnum(string key, T defaultValue = default)
    {
        _editorPrefsT = new EditorPrefsT<T>(key, EnumSerializer<T>.Instance, defaultValue);
    }

    public T Value
    {
        get => _editorPrefsT.Value;
        set => _editorPrefsT.Set(value);
    }

    public void Set(T value) => Value = value;
    public bool HasValue() => _editorPrefsT.HasValue();
    public void DeleteValue() => _editorPrefsT.DeleteValue();
    public static implicit operator T(EditorPrefsEnum<T> editorPrefs) => editorPrefs.Value;
    public override string ToString() => Value.ToString();
}

public struct EditorPrefsT<T> where T : struct
{
    private readonly string _key;
    private readonly T _defaultValue;
    private readonly IPrefsSerializer<T> _serializer;

    public EditorPrefsT(string key, IPrefsSerializer<T> serializer, T defaultValue = default)
    {
        _key = key;
        _serializer = serializer;
        _defaultValue = defaultValue;
    }

    public T Value
    {
        get
        {
            if (_serializer == null) Debug.LogWarning($"[EditorPrefsT] serializer not set for type ({typeof(T)})");
            var serialized = EditorPrefs.GetString(_key, _defaultValue.ToString());
            var value = _serializer?.Deserialize(serialized) ?? _defaultValue;
            return value;
        }
        set
        {
            var serialized = _serializer?.Serialize(value) ?? value.ToString();
            EditorPrefs.SetString(_key, serialized);
        }
    }

    public void Set(T value) => Value = value;
    public bool HasValue() => EditorPrefs.HasKey(_key);
    public void DeleteValue() => EditorPrefs.DeleteKey(_key);
    public static implicit operator T(EditorPrefsT<T> editorPrefs) => editorPrefs.Value;
    public override string ToString() => _serializer?.Serialize(Value) ?? Value.ToString();
}
#endif
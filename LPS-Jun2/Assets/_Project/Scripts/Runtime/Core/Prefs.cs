using System;
using UnityEngine;

public static class Prefs
{
    public static PlayerPrefsEnum<SystemLanguage> Language = new(nameof(Language));
    public static PlayerPrefsString DataContainerPaths = new(nameof(DataContainerPaths));
    public static PlayerPrefsInt Level = new(nameof(Level));
    public static PlayerPrefsBool DisableCapacity = new(nameof(DisableCapacity));
    public static PlayerPrefsBool Sound = new(nameof(Sound), defaultValue: true);
    public static PlayerPrefsBool Music = new(nameof(Music), defaultValue: !Application.isEditor);
    public static PlayerPrefsBool ColorBlind = new(nameof(ColorBlind));
    public static PlayerPrefsInt WinStreak = new(nameof(WinStreak));
    public static PlayerPrefsInt MaxWinStreak = new(nameof(MaxWinStreak));
    public static PlayerPrefsBool GreenLevel = new(nameof(GreenLevel));
    public static PlayerPrefsT<DateTimeOffset> InstallTimestamp = new(nameof(InstallTimestamp), DateTimeOffsetSerializer.Instance);

#if UNITY_EDITOR
    public static EditorPrefsBool StartSceneBootstrap = new(nameof(StartSceneBootstrap), defaultValue: true);
    public static EditorPrefsBool HideDDOL = new(nameof(HideDDOL));
    public static EditorPrefsEnum<EditorSdkType> EditorSdk = new(nameof(EditorSdk));
#endif

    public enum EditorSdkType
    {
        Editor,
    }
}

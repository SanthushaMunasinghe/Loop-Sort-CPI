using System.Collections.Generic;
using StatefulUI.Runtime.Localization;
using TMPro;
using UnityEngine;

public static class Localization
{
    private static readonly HashSet<SystemLanguage> AvailableLanguages = new()
    {
        SystemLanguage.English,
        SystemLanguage.Turkish,
        SystemLanguage.French,
        SystemLanguage.Japanese,
        SystemLanguage.ChineseSimplified,
        SystemLanguage.ChineseTraditional,
        SystemLanguage.Korean,
        SystemLanguage.Indonesian,
        SystemLanguage.German,
        SystemLanguage.Spanish,
        SystemLanguage.Italian,
        SystemLanguage.Portuguese,
        SystemLanguage.Russian
    };

    public static string Get(string key)
    {
        return LocalizationUtils.GetPhrase(key, string.Empty);
    }

    public static string Format(string key, object value)
    {
        var phrase = Get(key);
        return string.Format(phrase, value);
    }

    public static void Set(SystemLanguage language)
    {
        var finalLanguage = language switch
        {
            SystemLanguage.Unknown => SystemLanguage.English,
            SystemLanguage.Chinese => SystemLanguage.ChineseSimplified,
            _ => language
        };

        if (!AvailableLanguages.Contains(finalLanguage))
            finalLanguage = SystemLanguage.English;

        Prefs.Language.Set(finalLanguage);
        Debug.Log($"Language set to {finalLanguage.ToString()}.");

        UpdateFallbackFont();
    }

    public static void UpdateFallbackFont()
    {
        var language = Prefs.Language.Value;
        var fallbackName = language switch
        {
            SystemLanguage.ChineseSimplified => "SC",
            SystemLanguage.ChineseTraditional => "TC",
            SystemLanguage.Japanese => "JP",
            SystemLanguage.Korean => "KR",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(fallbackName)) return;
        var fallbackFontAssets = TMP_Settings.fallbackFontAssets;
        if (fallbackFontAssets == null) return;
        var fallbackFont = fallbackFontAssets.Find(x => x.name.Contains(fallbackName));
        if (fallbackFont == null) return;
        var idxOfFallbackFont = fallbackFontAssets.IndexOf(fallbackFont);
        if (1 >= idxOfFallbackFont) return;
        fallbackFontAssets.Swap(1, idxOfFallbackFont);
    }

    public static bool IsDefaultLanguage()
    {
        return Prefs.Language.Value == SystemLanguage.English;
    }
}
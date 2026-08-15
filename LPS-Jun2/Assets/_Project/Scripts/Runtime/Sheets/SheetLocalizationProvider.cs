using StatefulUI.Runtime.Localization;
using UnityEngine;
using VContainer;

public sealed class SheetLocalizationProvider : ILocalizationProvider
{
    private static LocalizationSheet _localization;

    public string GetPhrase(string key, string defaultValue)
    {
        if (!Application.isPlaying)
            return defaultValue;

        if (_localization == null)
        {
            var scope = LifetimeScopeH.FindScope<BootstrapScope>();
            if (scope == null) return defaultValue;
            var sheetContainer = scope.Container.Resolve<SheetContainer>();
            if (sheetContainer == null) return defaultValue;
            _localization = sheetContainer.Localization;
            if (_localization == null) return defaultValue;
        }

        var language = Prefs.Language.Value;
        var keyData = _localization.Find(key);
        if (keyData == null) return defaultValue;

        var translations = keyData.Translations;
        if (!translations.TryGetValue(language, out var translation))
            translation = translations[SystemLanguage.English];

        if (string.IsNullOrEmpty(translation))
            return defaultValue;

        return translation;
    }
}
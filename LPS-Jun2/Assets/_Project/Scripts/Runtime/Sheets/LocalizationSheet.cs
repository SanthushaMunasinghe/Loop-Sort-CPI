using System.Collections.Generic;
using Cathei.BakingSheet;
using UnityEngine;

public sealed class LocalizationSheet : Sheet<string, LocalizationSheet.Row>
{
    public class Row : SheetRow<string>
    {
        public Dictionary<SystemLanguage, string> Translations { get; private set; }
    }

    public override void PostLoad(SheetConvertingContext context)
    {
        base.PostLoad(context);

        if (!Application.isPlaying) return;
#if RELEASE_BUILD
        SetSystemLanguage();
#else
        SetDefaultLanguageIfNeeded();
#endif
    }

    private static void SetDefaultLanguageIfNeeded()
    {
        if (Prefs.Language.HasValue()) return;
        SetSystemLanguage();
    }

    private static void SetSystemLanguage()
    {
        Localization.Set(Application.systemLanguage);
    }
}
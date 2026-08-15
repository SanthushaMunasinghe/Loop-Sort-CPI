using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cathei.BakingSheet;
using Cathei.BakingSheet.Unity;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class CJKFontCharacterUpdater
{
    private const string FontsPath = "Assets/_Project/Arts/Fonts/";

    private static readonly List<LanguageInfo> Languages = new()
    {
        new LanguageInfo { Language = SystemLanguage.Japanese, FontPath = "Noto Sans JP/NotoSansJP-Bold SDF.asset" },
        new LanguageInfo { Language = SystemLanguage.Korean, FontPath = "Noto Sans KR/NotoSansKR-Bold SDF.asset" },
        new LanguageInfo { Language = SystemLanguage.ChineseSimplified, FontPath = "Noto Sans SC/NotoSansSC-Bold SDF.asset" },
        new LanguageInfo { Language = SystemLanguage.ChineseTraditional, FontPath = "Noto Sans TC/NotoSansTC-Bold SDF.asset" },
    };

    public struct LanguageInfo
    {
        public SystemLanguage Language;
        public string FontPath;
    }

    [MenuItem("Tools/Fonts/Update CJK characters", priority = -99)]
    public static async void UpdateCharacters()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Updating CJK characters", "Loading localization sheets...", 0f);

            var emptyDictionary = new Dictionary<string, string>();
            var fileSystem = new CustomFileSystem(emptyDictionary);
            var converters = new ISheetImporter[] { new JsonSheetConverter("Sheets/Local", fileSystem) };
            var sheetContainer = new SheetContainer(converters, new UnityLogger());
            await sheetContainer.Bake(converters);

            var totalSteps = Mathf.Max(1, Languages.Count + 1);
            for (var languageIndex = 0; languageIndex < Languages.Count; languageIndex++)
            {
                var languageInfo = Languages[languageIndex];
                var language = languageInfo.Language;
                var progress = (float)(languageIndex + 1) / totalSteps;
                EditorUtility.DisplayProgressBar(
                    "Updating CJK characters",
                    $"Updating {language} ({languageIndex + 1}/{Languages.Count})",
                    progress);

                var fontAssetPath = languageInfo.FontPath;
                var databaseFontPath = Path.Combine(FontsPath, fontAssetPath);
                var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(databaseFontPath);
                if (fontAsset == null)
                {
                    Debug.LogError($"Font asset was not found at '{databaseFontPath}'.");
                    continue;
                }

                var characters = new HashSet<uint>();
                foreach (var locale in sheetContainer.Localization)
                {
                    var translation = locale.Translations[language];
                    AddCodePoints(characters, translation);
                }

                if (characters.Count == 0)
                {
                    Debug.LogWarning($"No characters were found for {language}.");
                    continue;
                }

                var orderedCharacters = new List<uint>(characters);
                orderedCharacters.Sort();

                fontAsset.ReadFontAssetDefinition();
                var creationSettings = fontAsset.creationSettings;
                creationSettings.characterSequence = BuildCharacterSequence(orderedCharacters);
                fontAsset.creationSettings = creationSettings;

                var missingUnicodes = Array.Empty<uint>();
                var originalPopulationMode = fontAsset.atlasPopulationMode;

                try
                {
                    fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                    fontAsset.ClearFontAssetData();
                    fontAsset.TryAddCharacters(orderedCharacters.ToArray(), out missingUnicodes);
                }
                finally
                {
                    fontAsset.atlasPopulationMode = originalPopulationMode;
                }

                EditorUtility.SetDirty(fontAsset);
                if (fontAsset.material != null)
                    EditorUtility.SetDirty(fontAsset.material);

                foreach (var atlasTexture in fontAsset.atlasTextures ?? Array.Empty<Texture2D>())
                    if (atlasTexture != null)
                        EditorUtility.SetDirty(atlasTexture);

                if (missingUnicodes.Length > 0)
                {
                    Debug.Log(
                        $"Updated '{fontAsset.name}' but {missingUnicodes.Length} characters could not be added. " +
                        $"The asset now contains {fontAsset.characterTable.Count} total characters.",
                        fontAsset);
                }
                else
                {
                    Debug.Log($"Updated '{fontAsset.name}' with {fontAsset.characterTable.Count} total characters.", fontAsset);
                }
            }

            EditorUtility.DisplayProgressBar("Updating CJK characters", "Saving assets...", 1f);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void AddCodePoints(ISet<uint> characters, string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        for (var i = 0; i < text.Length; i++)
        {
            var currentCharacter = text[i];
            uint codePoint;

            if (char.IsHighSurrogate(currentCharacter) &&
                i + 1 < text.Length &&
                char.IsLowSurrogate(text[i + 1]))
            {
                codePoint = (uint)char.ConvertToUtf32(currentCharacter, text[i + 1]);
                i++;
            }
            else
            {
                codePoint = currentCharacter;
            }

            if (ShouldIncludeCodePoint(codePoint))
                characters.Add(codePoint);
        }
    }

    private static bool ShouldIncludeCodePoint(uint codePoint)
    {
        return codePoint != ' ' &&
               !IsLatinLetterOrDigit(codePoint) &&
               !IsAsciiPunctuationOrSymbol(codePoint);
    }

    private static bool IsLatinLetterOrDigit(uint codePoint)
    {
        return codePoint is >= '0' and <= '9'
            or >= 'A' and <= 'Z'
            or >= 'a' and <= 'z';
    }

    private static bool IsAsciiPunctuationOrSymbol(uint codePoint)
    {
        return codePoint is >= '!' and <= '/'
            or >= ':' and <= '@'
            or >= '[' and <= '`'
            or >= '{' and <= '~';
    }

    private static string BuildCharacterSequence(IEnumerable<uint> characters)
    {
        var builder = new StringBuilder();
        foreach (var character in characters)
            builder.Append(char.ConvertFromUtf32((int)character));

        return builder.ToString();
    }
}
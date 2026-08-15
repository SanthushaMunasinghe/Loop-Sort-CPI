using TMPro;

public static partial class Extensions
{
    public static void SetTextWithoutInvalidChars(this TMP_Text text, string sourceText)
    {
        if (text == null) return;

        if (string.IsNullOrEmpty(sourceText))
        {
            text.SetText(string.Empty);
            return;
        }

        var characterLookupTable = text.font.characterLookupTable;
        for (var i = sourceText.Length - 1; i >= 0; i--)
        {
            var c = sourceText[i];
            var unicode = (uint)c;
            if (characterLookupTable.ContainsKey(unicode)) continue;
            sourceText = sourceText.Remove(i, 1);
        }

        text.SetText(sourceText);
    }
}
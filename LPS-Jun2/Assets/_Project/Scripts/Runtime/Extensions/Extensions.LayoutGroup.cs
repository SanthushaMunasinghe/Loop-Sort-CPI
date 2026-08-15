using UnityEngine;
using UnityEngine.UI;

public static partial class Extensions
{
    public static void RebuildLayout(this LayoutGroup layoutGroup)
    {
        layoutGroup.enabled = true;
        LayoutRebuilder.ForceRebuildLayoutImmediate(layoutGroup.transform as RectTransform);
        layoutGroup.enabled = false;
    }
}
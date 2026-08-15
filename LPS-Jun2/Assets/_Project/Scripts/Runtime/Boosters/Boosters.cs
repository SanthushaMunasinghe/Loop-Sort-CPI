using System;
using UnityEngine;

public sealed class Boosters : Data
{
    public GenericDictionary<BoosterType, Data> Collection;

    public Data Get(BoosterType boosterType)
    {
        return Collection.TryGetValue(boosterType, out var data) ? data : default;
    }

    [Serializable]
    public struct Data
    {
        public BoosterBase BoosterPrefab;
        public Sprite Icon;
        public Sprite Background;
        public Sprite BackgroundDisabled;
    }
}

public enum BoosterType
{
    None = 0,

    Hammer = 1,
    Shuffle = 2,
    Select = 3,
    Capacity = 4,
    Undo = 5,
}

public static partial class Extensions
{
    public static string ToAnalyticsName(this BoosterType booster)
    {
        return booster switch
        {
            BoosterType.Shuffle => "booster_2",
            BoosterType.Undo => "booster_3",
            BoosterType.Capacity => "booster_1",
            _ => "unknown_booster"
        };
    }

    public static string ToInGamePurchasePlacement(this BoosterType booster)
    {
        return $"in_game_{booster.ToAnalyticsName()}_purchase";
    }

    public static string ToLocalizedName(this BoosterType booster)
    {
        return Localization.Get($"booster_{booster.ToString().ToLowerInvariant()}_name");
    }

    public static string ToLocalizedDesc(this BoosterType booster)
    {
        return Localization.Get($"booster_{booster.ToString().ToLowerInvariant()}_desc");
    }
}
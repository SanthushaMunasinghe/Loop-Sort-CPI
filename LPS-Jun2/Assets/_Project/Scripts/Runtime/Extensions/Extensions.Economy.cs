using System.Collections.Generic;

public static partial class Extensions
{
    private static readonly Dictionary<BoosterType, Item> BoosterItemByBoosterType = new()
    {
        { BoosterType.Shuffle, Item.ShuffleBooster },
        { BoosterType.Capacity, Item.CapacityBooster },
        { BoosterType.Undo, Item.UndoBooster }
    };

    public static Item ToEconomyItem(this BoosterType boosterType)
    {
        return BoosterItemByBoosterType[boosterType];
    }

    public static bool IsBooster(this Item item)
    {
        return BoosterItemByBoosterType.ContainsValue(item);
    }
}
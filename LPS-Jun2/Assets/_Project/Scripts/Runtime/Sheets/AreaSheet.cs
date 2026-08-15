using Cathei.BakingSheet;

public sealed class AreaSheet : Sheet<int, AreaSheet.Row>
{
    public class Row : SheetRow<int>
    {
        public AreaType Type { get; private set; }
        public int UnlockLevel { get; private set; }

        private PlayerPrefsBool _isUnlocked;

        public bool IsUnlocked => _isUnlocked.Value;
        public void Unlock() => _isUnlocked.Set(true);

        public override void PostLoad(SheetConvertingContext context)
        {
            base.PostLoad(context);

            _isUnlocked = new PlayerPrefsBool("Area_" + Index + "_Unlocked", defaultValue: Index == 0);
        }
    }

    public Row GetCurrentArea()
    {
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            var item = Items[i];
            if (Prefs.Level.Value + 1 >= item.UnlockLevel)
                return item;
        }
        return null;
    }

    public Row GetPreviousArea()
    {
        var currentArea = GetCurrentArea();
        if (currentArea == null) return null;
        return currentArea.Index == 0 ? null : Items[currentArea.Index - 1];
    }

    public bool TryGetCompletedArea(int level, out Row data)
    {
        foreach (var item in Items)
        {
            if (level != item.UnlockLevel) continue;
            if (item.IsUnlocked) continue;
            data = item;
            return true;
        }

        data = null;
        return false;
    }
}

public enum AreaType
{
    None = 0,
    NewYork = 1,
    Paris = 2,
    London = 3,
    Rome = 4,
    Amsterdam = 5,
    Tokyo = 6,
    Istanbul = 7,
    Dubai = 8,
    Barcelona = 9,
    SanFrancisco = 10,
    HongKong = 11,
    Prague = 12,
    Seoul = 13,
    Singapore = 14,
    Milano = 15,
    Madrid = 16,
    Athens = 17,
    Sydney = 18,
    Bangkok = 19,
}

public static partial class Extensions
{
    public static string ToLocalizedName(this AreaType type)
    {
        const string format = "area_name_{0}";
        var key = string.Format(format, type.ToString().ToLowerInvariant());
        return Localization.Get(key);
    }
}
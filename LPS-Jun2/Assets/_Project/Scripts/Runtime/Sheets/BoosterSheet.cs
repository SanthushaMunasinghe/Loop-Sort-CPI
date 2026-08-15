using Cathei.BakingSheet;

public sealed class BoosterSheet : Sheet<BoosterType, BoosterSheet.Booster>
{
    public class Booster : SheetRow<BoosterType>
    {
        public int UnlockLevel { get; private set; }
        public int Cost { get; private set; }
        public int PurchaseUnits { get; private set; }
        public int FreeAtStart { get; private set; }

        private PlayerPrefsInt _count;
        private PlayerPrefsBool _lock;

        private bool _freeUse;

        public bool IsUnlocked() => !_lock.Value;
        public bool IsAtUnlockLevel() => Prefs.Level.Value + 1 >= UnlockLevel;
        public void Unlock() => _lock.Set(false);

        public bool IsAvailable() => GetCount() > 0;
        public int GetCount() => _count.Value;

        public void AllowFreeUse() => _freeUse = true;
        public void DisallowFreeUse() => _freeUse = false;
        public bool IsFreeUseAllowed() => _freeUse;

        public void AddCount(int count) => _count.Set(_count.Value + count);

        public override void PostLoad(SheetConvertingContext context)
        {
            base.PostLoad(context);

            _count = Id.ToEconomyItem().GetPrefs();
            _lock = new PlayerPrefsBool("LockOf_" + Id, defaultValue: true);
        }
    }

    public bool TryGetUnlockedBooster(int level, out BoosterType unlockedBooster)
    {
        foreach (var booster in Items)
        {
            if (booster.UnlockLevel != level) continue;
            unlockedBooster = booster.Id;
            return true;
        }

        unlockedBooster = BoosterType.None;
        return false;
    }
}
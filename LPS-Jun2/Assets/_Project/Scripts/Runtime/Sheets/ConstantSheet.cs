using System;
using System.Globalization;
using Cathei.BakingSheet;

public sealed class ConstantSheet : ConstantSheetBase<ConstantSheetRow>
{
}

public abstract class ConstantSheetBase<TRow> : Sheet<GameConstant, TRow> where TRow : ConstantSheetRow, new()
{
    public int GetInt(GameConstant key) => int.Parse(Find(key).Value, NumberFormatInfo.InvariantInfo);

    public float GetFloat(GameConstant key) => float.Parse(Find(key).Value, NumberFormatInfo.InvariantInfo);

    public string GetString(GameConstant key) => Find(key).Value;

    public bool TryGetInt(string key, out int value)
    {
        value = 0;
        if (!Enum.TryParse<GameConstant>(key, ignoreCase: true, out var gameConstant)) return false;
        value = GetInt(gameConstant);
        return true;
    }
}

public class ConstantSheetRow : SheetRow<GameConstant>
{
    public string Value { get; private set; }
}

public enum GameConstant
{
    MaxLevelOffset,
    ThemeFrequency,
    MainMenuOffset,
    WinReward,
    HardLevelReward,
    SuperHardLevelReward,
    RefillLives,
    Revive,
    SecondRevive,
    PiggyBankMultiplier,
    PiggyBankTargetGold,
    PiggyBankPopupInterval,
    DailyReward,
    MinimumProductCost,
    BattlePassKeyReward,
    HardBattlePassKeyReward,
    SuperHardBattlePassKeyReward,
    BattlePassTokenPayout,
    BattlePassTokenHardPayout,
    BattlePassTokenSuperHardPayout,
    GoldInitialValue,
    ShopFreeCoins,
    LevelSheets,
    LavaQuestGrandPrize,
    LavaQuestFailCooldown,
    CollectionBarTokenPayout,
    CollectionBarTokenHardPayout,
    CollectionBarTokenSuperHardPayout,
}
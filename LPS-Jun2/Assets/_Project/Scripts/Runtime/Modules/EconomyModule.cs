using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MessagePipe;
using StatefulUI.Runtime.Core;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class EconomyModule : ModuleBase, IInitializable
{
    [Inject] private SheetContainer _sheetContainer;

    [Inject] private IPublisher<EconomyAddMessage> _addPub;
    [Inject] private IPublisher<EconomyConsumeMessage> _consumePub;
    [Inject] private IPublisher<EconomyUpdateMessage> _updatePub;
    [Inject] private IPublisher<EconomyConsumeFailMessage> _consumeFailPub;

    private bool _updateInterceptorActive;

    private readonly Dictionary<Item, PlayerPrefsInt> _items = new();
    private readonly PlayerPrefsInt _level = Prefs.Level;
    private readonly Dictionary<Item, AutoResetUniTaskCompletionSource> _updateCompletionByItem = new();

    public struct Transaction
    {
        public int? LevelNumber;
        public Item Item;
        public Item? ItemUsed;
        public ItemType? ItemType;
        public int Units;
        public int? UnitCost;

        public Vector3? WorldPosition;
        public float? ParticleCount;
        public StatefulComponent View;
    }

    public void Initialize()
    {
        foreach (Item item in Enum.GetValues(typeof(Item)))
        {
            if (_items.ContainsKey(item)) continue;
            _sheetContainer.Constants.TryGetInt($"{item.ToString()}InitialValue", out var initialValue);
            var prefs = item.GetPrefs(initialValue);
            _items.Add(item, prefs);
        }
    }

    public void Set(Transaction transaction)
    {
        var data = _items[transaction.Item];
        var previousValue = data.Value;
        data.Set(transaction.Units);
        PublishEconomyUpdate(new EconomyUpdateMessage
        {
            Transaction = transaction,
            PreviousAmount = previousValue,
            NewAmount = data.Value
        });
    }

    public void GrantGoldRequest()
    {
        GrantGold(100);
    }

    public void GrantMissingGold(Transaction transaction)
    {
        if (transaction.Item != Item.Gold) return;

        var missingUnits = Math.Max(0, transaction.Units - GetAmount(Item.Gold));
        if (missingUnits == 0) return;

        GrantGold(missingUnits);
    }

    private void GrantGold(int units)
    {
        Add(new Transaction
        {
            Item = Item.Gold,
            Units = units,
            ItemUsed = Item.None
        });
    }

    public void Add(Transaction transaction)
    {
        var data = _items[transaction.Item];
        data.Set(data.Value + transaction.Units);

        var isBooster = transaction.Item.IsBooster();
        transaction.LevelNumber ??= _level.Value + 1;
        transaction.UnitCost ??= isBooster ? 0 : 1;
        transaction.ItemType ??= isBooster ? ItemType.Consumable : ItemType.SoftCurrency;

        var previousAmount = data.Value - transaction.Units;
        var newAmount = data.Value;
        _addPub.Publish(new EconomyAddMessage
        {
            Transaction = transaction,
            PreviousAmount = previousAmount,
            NewAmount = newAmount,
        });
        PublishEconomyUpdate(new EconomyUpdateMessage
        {
            Transaction = transaction,
            PreviousAmount = previousAmount,
            NewAmount = newAmount,
        });
    }

    public bool TryConsume(Transaction transaction)
    {
        var data = _items[transaction.Item];
        if (transaction.Units > data.Value)
        {
            _consumeFailPub.Publish(new EconomyConsumeFailMessage
            {
                Transaction = transaction,
                CurrentAmount = data.Value
            });
            return false;
        }
        data.Set(data.Value - transaction.Units);

        var isBooster = transaction.Item.IsBooster();
        transaction.LevelNumber ??= _level.Value + 1;
        transaction.UnitCost ??= isBooster ? 0 : 1;
        transaction.ItemType ??= isBooster ? ItemType.Consumable : ItemType.SoftCurrency;

        var previousAmount = data.Value + transaction.Units;
        var newAmount = data.Value;
        _consumePub.Publish(new EconomyConsumeMessage
        {
            Transaction = transaction,
            PreviousAmount = previousAmount,
            NewAmount = newAmount,
        });
        PublishEconomyUpdate(new EconomyUpdateMessage
        {
            Transaction = transaction,
            PreviousAmount = previousAmount,
            NewAmount = newAmount,
        });

        return true;
    }

    public bool CanConsume(Transaction transaction)
    {
        var data = _items[transaction.Item];
        return data.Value >= transaction.Units;
    }

    public int GetAmount(Item item)
    {
        return _items[item].Value;
    }

    public void TrackItems()
    {
    }

    private async UniTaskVoid PublishEconomyUpdate(EconomyUpdateMessage m)
    {
        if (_updateInterceptorActive)
        {
            var completionSource = AutoResetUniTaskCompletionSource.Create();
            _updateCompletionByItem[m.Transaction.Item] = completionSource;
            await completionSource.Task;
        }
        _updatePub.Publish(m);
    }

    public void EnableUpdateInterceptor(Item item)
    {
        _updateInterceptorActive = true;
        _updateCompletionByItem.TryAdd(item, null);
    }

    public void EnableUpdateInterceptor(List<Item> items)
    {
        _updateInterceptorActive = true;
        foreach (var item in items) _updateCompletionByItem.TryAdd(item, null);
    }

    public void DisableUpdateInterceptor()
    {
        _updateInterceptorActive = false;
        foreach (var completionSource in _updateCompletionByItem.Values)
            completionSource?.TrySetResult();
        _updateCompletionByItem.Clear();
    }

    public void ContinueUpdateFor(Item item)
    {
        if (!_updateCompletionByItem.TryGetValue(item, out var completionSource)) return;
        completionSource?.TrySetResult();
        _updateCompletionByItem.Remove(item);
    }

    public bool IsUpdateIntercepted(Item item)
    {
        return _updateCompletionByItem.ContainsKey(item);
    }
}

public enum Item
{
    None = 0, N = 0,
    IAP = 100,
    RV = 101,

    Gold = 1,
    Life = 2,
    NoAds = 3,
    PiggyBank = 4,

    ShuffleBooster = 10,
    UndoBooster = 11,
    CapacityBooster = 12,
    Boosters = 19,

    Block = 30,
    BattlePassKey = 40,
    CollectionBarToken = 50,
}

public enum ItemType
{
    None,
    SoftCurrency,
    Consumable,
}

public enum TransactionType
{
    In,
    Out
}

public struct EconomyAddMessage
{
    public EconomyModule.Transaction Transaction;
    public int PreviousAmount;
    public int NewAmount;
}

public struct EconomyConsumeMessage
{
    public EconomyModule.Transaction Transaction;
    public int PreviousAmount;
    public int NewAmount;
}

public struct EconomyUpdateMessage
{
    public EconomyModule.Transaction Transaction;
    public int PreviousAmount;
    public int NewAmount;
}

public struct EconomyConsumeFailMessage
{
    public EconomyModule.Transaction Transaction;
    public int CurrentAmount;
}

public static partial class Extensions
{
    public static Item NormalizeLegacyItem(this Item item)
    {
        return item == Item.Life ? Item.Gold : item;
    }

    public static string ToAnalyticsName(this Item item)
    {
        return item switch
        {
            Item.None => "none",
            Item.Gold => "coins",
            Item.Life => "coins",
            Item.NoAds => "no_ads",
            Item.PiggyBank => "piggy_bank",
            Item.ShuffleBooster => "booster_2",
            Item.UndoBooster => "booster_3",
            Item.CapacityBooster => "booster_1",
            Item.Boosters => "boosters",
            Item.Block => "block",
            Item.BattlePassKey => "battle_pass_key",
            Item.CollectionBarToken => "collection_bar_token",
            Item.IAP => "iap",
            Item.RV => "rewarded_video",
            _ => "unknown_item"
        };
    }

    public static PlayerPrefsInt GetPrefs(this Item item, int defaultValue = 0)
    {
        #region Old Data Migration

        if (item == Item.Gold)
        {
            var oldCoins = 0;
            var oldCoinsData = new PlayerPrefsInt("Coins");
            if (oldCoinsData.HasValue()) oldCoins += oldCoinsData.Value;
            var oldGoldData = new PlayerPrefsInt("Gold");
            if (oldGoldData.HasValue()) oldCoins += oldGoldData.Value;
            if (oldCoins > 0) defaultValue = oldCoins;
        }
        else
        {
            var oldData = new PlayerPrefsInt(item.ToString());
            if (oldData.HasValue()) defaultValue = oldData.Value;
        }

        #endregion

        var prefs = new PlayerPrefsInt($"economy_item_quantity_{(int)item}", defaultValue);
        return prefs;
    }
}

public struct RewardInfo
{
    public Item Item;
    public int Quantity;
}

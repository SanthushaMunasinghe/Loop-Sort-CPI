using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;

public sealed class GameWinState : GameStateBase
{
    [Inject] private Monitors _monitors;
    [Inject] private SceneModule _sceneModule;
    [Inject] private HapticModule _hapticModule;
    [Inject] private InteractionModule _interactionModule;
    [Inject] private TimeModule _timeModule;
    [Inject] private EconomyModule _economyModule;
    [Inject] private SheetContainer _sheetContainer;
    [Inject] private RemoteConfigModule _remoteConfigModule;

    [Inject] private IPublisher<LevelWinMessage> _levelWinPub;

    private LevelAnimationConfig _levelAnimationConfig;

    public override void Init()
    {
        base.Init();

        _levelAnimationConfig = _remoteConfigModule.GetDataClassNew<LevelAnimationConfig>();
    }

    public override void OnEnter()
    {
        base.OnEnter();

        OnEnterAsync();
    }

    private async UniTaskVoid OnEnterAsync()
    {
        Prefs.WinStreak.Value++;
        Prefs.MaxWinStreak.Value = Mathf.Max(Prefs.MaxWinStreak.Value, Prefs.WinStreak.Value);

        var rewards = new List<RewardInfo>();
        var sceneResolver = _sceneModule.Container;
        var themeType = sceneResolver.Resolve<ThemeType>();
        var goldRewardQuantity = GetGoldRewardQuantity(themeType);
        rewards.Add(new RewardInfo { Item = Item.Gold, Quantity = goldRewardQuantity });

#if PiggyBank
        // var piggyBankMultiplier = _sheetContainer.Constants.GetFloat(GameConstant.PiggyBankMultiplier);
        // var piggyBankCurrentGold = _economyModule.GetAmount(Item.PiggyBank);
        // var piggyBankTargetGold = _sheetContainer.Constants.GetInt(GameConstant.PiggyBankTargetGold);
        // var piggyBankReward = Mathf.RoundToInt(reward * piggyBankMultiplier);
        // var piggyBankFinalReward = Mathf.Min(piggyBankReward, piggyBankTargetGold - piggyBankCurrentGold);
#endif

        _levelWinPub.Publish(new LevelWinMessage
        {
            ThemeType = themeType,
            WinStreak = Prefs.WinStreak.Value,
            Rewards = rewards
        });

        var winSystem = sceneResolver.Resolve<WinSystem>();
        var remainingCarrierCount = winSystem.RemainingCarrierCount();

        var winData = new Dictionary<string, object>
        {
            { "golds", _economyModule.GetAmount(Item.Gold) },
            { "rewards", rewards },
            { "remaining_carrier_count", remainingCarrierCount }
        };

        _interactionModule.EnableRestriction(this);
        _monitors.DisableRaycaster(this);
        _economyModule.TrackItems();
        Prefs.Level.Value++;

        var levelTransition = sceneResolver.Resolve<LevelTransitionData>();
        if (levelTransition.Exit) return;

        var delayInSeconds = _levelAnimationConfig.Exit ? 3f : 1.5f;
        await UniTask.Delay(TimeSpan.FromSeconds(delayInSeconds));

        _hapticModule.PlaySuccess();

        _monitors.RestoreRaycaster(this);

        var winMonitor = _monitors.Get<WinMonitor>();
        winMonitor.SetResults(new WinMonitor.ResultArgs
        {
            Level = Prefs.Level.Value,
            Rewards = rewards,
        });
        _monitors.Activate<WinMonitor>();
    }

    public override void OnExit()
    {
        base.OnExit();

        _interactionModule.DisableRestriction(this);
        _monitors.RestoreRaycaster(this);
    }

    private int GetGoldRewardQuantity(ThemeType themeType)
    {
        var goldRewardConstant = themeType switch
        {
            ThemeType.Default => GameConstant.WinReward,
            ThemeType.Hard => GameConstant.HardLevelReward,
            ThemeType.SuperHard => GameConstant.SuperHardLevelReward,
            _ => GameConstant.WinReward
        };
        var goldRewardQuantity = _sheetContainer.Constants.GetInt(goldRewardConstant);
        return goldRewardQuantity;
    }
}

public struct LevelWinMessage
{
    public ThemeType ThemeType;
    public int WinStreak;
    public List<RewardInfo> Rewards;
}
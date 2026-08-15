using System.Collections.Generic;

public sealed class WinMonitor : MonitorBase
{
    public ResultArgs Results { get; private set; }

    public struct ResultArgs
    {
        public int Level;
        public List<RewardInfo> Rewards;
    }

    public void SetResults(ResultArgs args)
    {
        Results = args;
        if (args.Rewards == null) return;
        for (var i = args.Rewards.Count - 1; i >= 0; i--)
        {
            var reward = args.Rewards[i];
            var item = reward.Item;
            if (item == Item.Gold) continue;
            if (item == Item.BattlePassKey) continue;
            if (item == Item.CollectionBarToken) continue;
            args.Rewards.RemoveAt(i);
        }
    }
}
using System;

[Serializable]
public class RewardedAdsConfig
{
    // Triggers
    public bool Revive;
    public bool Booster;

    // Conditions
    public int ReviveStartLevel;
    public int BoosterStartLevel;
}
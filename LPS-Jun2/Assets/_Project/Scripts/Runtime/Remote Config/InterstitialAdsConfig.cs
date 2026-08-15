using System;

[Serializable]
public class InterstitialAdsConfig
{
    // Triggers
    public bool ShowOnLevelWinContinue = true;
    public bool ShowOnLevelLoseContinue;
    public bool ShowOnLevelExit;

    // Conditions
    public bool ShowForPayerUser = true;
    public int StartLevelToShowAds = 19;
    public float AdStartDelaySinceInstall = 21600;
    public int[] NoAdsOfferAfterInterstitialCounts = { 1, 3, 10 };

    // UI
    public bool ShowAdBreak;
}
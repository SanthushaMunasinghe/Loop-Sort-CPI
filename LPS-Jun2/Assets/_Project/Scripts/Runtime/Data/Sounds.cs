using UnityEngine;

public sealed class Sounds : Data
{
    [Header("Generic")]
    public AudioClip Click;
    public AudioClip Lose;
    public AudioClip Win;
    public AudioClip WinText;
    public AudioClip WinReward;
    public AudioClip Confetti;
    public AudioClip Coin;
    public AudioClip GiftUnlock;
    public AudioClip BoosterSelect;

    [Header("Music")]
    public AudioClip MainMenuMusic;
    public AudioClip PlayingMusic;
    public AudioClip HardLevelMusic;

    [Header("Texts")]
    public AudioClip Fabulous;
    public AudioClip Perfect;
    public AudioClip Great;
    public AudioClip Good;

    [Header("Game")]
    public AudioClip AddBlock;
    public AudioClip CarrierTransfer;
    public AudioClip CarrierComplete;
    public AudioClip CarrierTransferAb;
    public AudioClip CarrierCompleteAb;
}
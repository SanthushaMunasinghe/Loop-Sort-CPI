using System;
using System.Collections.Generic;
using StatefulUI.Runtime.Core;
using StatefulUISupport.Scripts.Components;

public sealed class RewardClaimMonitor : MonitorBase
{
    private Action _claimCallback;
    private ShowArgs _showArgs;

    public struct ItemArgs
    {
        public Item ItemType;
        public int Quantity;
    }

    public struct ShowArgs
    {
        public string Title;
        public string Description;
        public List<ItemArgs> Items;
        public string Placement;
    }

    public override void Setup()
    {
        base.Setup();

        SetButtonListener(ButtonRole.Claim, OnClaimClicked);
    }

    private void OnClaimClicked()
    {
        Monitors.Deactivate(this);

        var claimCallback = _claimCallback;
        _claimCallback = null;
        claimCallback?.Invoke();
    }

    public void Show(ShowArgs args)
    {
        _showArgs = args;
        Monitors.Additive<RewardClaimMonitor>();
        SetText(TextRole.Title, args.Title);
        SetText(TextRole.Desc, args.Description);
    }

    public void AddClaimListener(Action listener)
    {
        _claimCallback += listener;
    }
}

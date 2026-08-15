using System.Collections.Generic;
using AssetKits.ParticleImage;
using LitMotion;
using LitMotion.Extensions;
using MessagePipe;
using StatefulUI.Runtime.Core;
using StatefulUI.Runtime.References;
using StatefulUISupport.Scripts.Components;
using UnityEngine;
using VContainer;

public sealed class EconomyMonitor : MonitorBase
{
    [Inject] private EconomyModule _economyModule;
    [Inject] private HapticModule _hapticModule;
    [Inject] private AudioModule _audioModule;

    [Inject] private ISubscriber<EconomyAddMessage> _economyAddSub;
    [Inject] private ISubscriber<EconomyConsumeMessage> _economyConsumeSub;
    [Inject] private ISubscriber<EconomyUpdateMessage> _economyUpdateSub;

    private readonly Dictionary<Item, List<StatefulComponent>> _viewsByItem = new();
    private readonly Dictionary<StatefulComponent, MotionHandle> _motionByView = new();

    public override void OnDeactivated()
    {
        base.OnDeactivated();

        var goldContainer = GetContainer(ContainerRole.Gold);
        goldContainer.Clear();
    }

    public override void Setup()
    {
        base.Setup();

        _economyAddSub.Subscribe(OnEconomyAdd);
        _economyConsumeSub.Subscribe(OnEconomyConsume);
        _economyUpdateSub.Subscribe(OnEconomyUpdate);
    }

    public void AddItemView(Item item, StatefulComponent view)
    {
        if (!_viewsByItem.TryGetValue(item, out var views))
        {
            views = new List<StatefulComponent>();
            _viewsByItem[item] = views;
        }

        if (views.Contains(view)) return;
        views.Add(view);

        view.SetButtonListener(ButtonRole.Select, OnShopClicked);
        UpdateView(item, view);
    }

    public void UpdateViews(Item item)
    {
        var views = _viewsByItem[item];
        foreach (var view in views)
            UpdateView(item, view);
    }

    public void UpdateView(Item item, StatefulComponent view)
    {
        var amount = _economyModule.GetAmount(item);
        view.SetText(TextRole.Quantity, amount.ToString());
    }

    private void OnShopClicked()
    {
        _economyModule.GrantGoldRequest();
        _hapticModule.PlaySuccess();
    }

    private void OnEconomyAdd(EconomyAddMessage m)
    {
        var item = m.Transaction.Item;
        var targetView = m.Transaction.View;

        if (!m.Transaction.WorldPosition.HasValue) return;

        if (item == Item.Gold)
        {
            var worldPosition = m.Transaction.WorldPosition.Value;
            var particleCount = m.Transaction.ParticleCount;
            ApplyGoldParticleMotion(targetView, m.PreviousAmount, m.NewAmount, worldPosition, particleCount);

            if (!_viewsByItem.TryGetValue(Item.Gold, out var views)) return;
            foreach (var view in views)
            {
                if (view == targetView) continue;
                UpdateView(Item.Gold, view);
            }
        }
    }

    private void OnEconomyConsume(EconomyConsumeMessage m)
    {
        ApplyItemEconomyChangeMotion(m.Transaction.Item, m.PreviousAmount, m.NewAmount);
    }

    private void OnEconomyUpdate(EconomyUpdateMessage m)
    {
        if (m.Transaction.WorldPosition.HasValue) return;
        ApplyItemEconomyChangeMotion(m.Transaction.Item, m.PreviousAmount, m.NewAmount);
    }

    private void ApplyGoldParticleMotion(StatefulComponent view, int from, int to, Vector3 worldPosition, float? particleCount)
    {
        if (view == null) return;

        TextReference textRef = null;
        if (view.HasText(TextRole.Quantity))
            textRef = view.GetText(TextRole.Quantity);
        if (view.HasText(TextRole.Gold))
            textRef ??= view.GetText(TextRole.Gold);
        if (textRef == null) return;

        var targetT = view.GetObject(ObjectRole.GoldTarget).Transform;

        var goldParticle = GetContainer(ContainerRole.Gold).AddInstance<ParticleImage>();
        WorldToCanvasSpace(goldParticle.transform, worldPosition);
        goldParticle.attractorTarget = targetT;
        goldParticle.rateOverLifetime = particleCount.GetValueOrDefault(50f);
        goldParticle.Clear();
        goldParticle.Play();

        var requiredDelay = float.MaxValue;
        foreach (var particle in goldParticle.particles)
        {
            if (particle.Lifetime > requiredDelay) continue;
            requiredDelay = particle.Lifetime;
        }

        var motion = LMotion.Create(from, to, .6f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithDelay(requiredDelay, skipValuesDuringDelay: false)
            .WithImmediateBind()
            .WithOnComplete(() => goldParticle.gameObject.SetActive(false))
            .BindToText(textRef.TMP);

        _motionByView.TryGetValue(view, out var existingMotion);
        existingMotion.TryCancel();
        _motionByView[view] = motion;

        var loops = Mathf.Min(5, (int)goldParticle.rateOverLifetime);
        LMotion.Punch.Create(Vector3.one, Vector3.one * .2f, .1f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithDelay(requiredDelay)
            .WithOnLoopComplete(_ =>
            {
                _hapticModule.PlayLight();
                _audioModule.GetPlayer().Play(_audioModule.Sounds.Coin);
            })
            .WithLoops(loops)
            .BindToLocalScale(targetT);
    }

    private void ApplyItemEconomyChangeMotion(Item item, int from, int to)
    {
        if (!_viewsByItem.TryGetValue(item, out var views)) return;
        foreach (var view in views)
        {
            var textRef = view.GetText(TextRole.Quantity);
            var motion = LMotion.Create(from, to, 1f)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToText(textRef.TMP);
            _motionByView.TryGetValue(view, out var existingMotion);
            existingMotion.TryCancel();
            _motionByView[view] = motion;
        }
    }
}

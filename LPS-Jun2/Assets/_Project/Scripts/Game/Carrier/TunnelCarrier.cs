using System.Collections.Generic;
using LitMotion;
using LitMotion.Extensions;
using StatefulUI.Runtime.Core;
using StatefulUISupport.Scripts.Components;
using UnityEngine;
using VContainer;

public sealed class TunnelCarrier : GameBehaviourBase, IFeatureProcessor
{
    [SerializeField] private StatefulComponent View;
    [SerializeField] private List<Transform> Covers;

    [Inject] private Conveyor _conveyor;

    private Carrier _carrier;

    public override void OnRent()
    {
        base.OnRent();

        _carrier ??= GetComponent<Carrier>();
        RegisterView<TunnelCarrier>();
    }

    public void ProcessFeature(FeatureType featureType, string data)
    {
        _carrier.DisableGroupBlock();
        _carrier.SetCustomMaxBlockCount(_conveyor.GroupBlockCount);
        foreach (var cover in Covers) cover.transform.localScale = Vector3.one;
        _carrier.DisableTransferMotion();
    }

    public void UpdateView()
    {
        var blocks = _carrier.GetBlocks();
        var blockGroupCount = Mathf.Max(0, blocks.Count / _conveyor.GroupBlockCount - 1);
        View.SetText(TextRole.Count, blockGroupCount.ToString());
    }

    public void ApplyCoverMotion()
    {
        foreach (var cover in Covers)
        {
            LMotion.Create(1f, 0f, .25f)
                .BindToLocalScaleX(cover)
                .AddTo(this);

            LMotion.Create(0f, 1f, .25f)
                .WithDelay(.4f)
                .BindToLocalScaleX(cover)
                .AddTo(this);
        }
    }

    public void OnLevelAnimationStart()
    {
        transform.localScale = Vector3.zero;
    }

    public void OnLevelAnimationEnd()
    {
        LMotion.Create(Vector3.zero, Vector3.one, .25f)
            .BindToLocalScale(transform)
            .AddTo(this)
            .ToUniTask(SceneLoadToken);
    }
}
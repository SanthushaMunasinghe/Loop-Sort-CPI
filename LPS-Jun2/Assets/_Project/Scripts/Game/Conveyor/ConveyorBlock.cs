using System.Collections.Generic;
using Dreamteck.Splines;
using UnityEngine;
using VContainer;

public sealed class ConveyorBlock : GameBehaviourBase
{
    [SerializeField] private Transform BoneParent;

    [Inject] private Conveyor _conveyor;

    private readonly List<Transform> _bones = new();

    protected override void Awake()
    {
        base.Awake();

        BoneParent.GetComponentsInChildren(_bones);
    }

    public void Set(SplineComputer spline, ConveyorSlot first, ConveyorSlot last)
    {
        var from = first.SplineFollower.GetPercent();
        var to = last.SplineFollower.GetPercent() + .0001;
        HandleClosedSpline(spline, ref from, ref to);

        var distance = spline.CalculateLengthUnclamped(from, to);
        var interval = distance / _bones.Count;
        for (var i = 0; i < _bones.Count; i++)
        {
            var bone = _bones[i];
            var percent = spline.TravelUnclamped(from, interval * i);
            var splineSample = spline.Evaluate(percent);
            bone.position = splineSample.position;
            bone.rotation = splineSample.rotation;
        }
    }

    private void HandleClosedSpline(SplineComputer spline, ref double from, ref double to)
    {
        if (spline.isClosed) return;
        var distance = _conveyor.SlotCollisionDistance + .1f;
        var fromTravel = spline.Travel(from, distance, Spline.Direction.Backward);
        if (fromTravel <= 0) from = 0;
        var toTravel = spline.Travel(to, distance);
        if (toTravel >= 1) to = 1;
    }
}
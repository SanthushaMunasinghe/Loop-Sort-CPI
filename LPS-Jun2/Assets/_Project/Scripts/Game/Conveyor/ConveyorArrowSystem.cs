using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Dreamteck.Splines;
using MessagePipe;
using UnityEngine;
using VContainer;

public sealed class ConveyorArrowSystem : SystemBase
{
    [Inject] private ConveyorConfig _config;
    [Inject] private SplineComputer _splineComputer;
    [Inject] private PrefabModule _prefabModule;
    [Inject] private Conveyor _conveyor;

    [Inject] private ISubscriber<LevelBuildCompleteMessage> _levelBuildCompleteSub;

    public readonly List<SplineFollower> Arrows = new();

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _levelBuildCompleteSub.Subscribe(OnLevelBuildComplete).AddTo(bag);
    }

    private void OnLevelBuildComplete(LevelBuildCompleteMessage obj)
    {
        CreateArrows().Forget();
    }

    private async UniTaskVoid CreateArrows()
    {
        var parent = _splineComputer.transform;
        var length = _splineComputer.CalculateLength();
        var arrowCount = Mathf.FloorToInt(length / _config.ArrowPerDistance);
        for (var i = 0; i < arrowCount; i++)
        {
            var instance = _prefabModule.Rent(_config.ArrowPrefab, parent);
            instance.transform.localScale = Vector3.one;
            var splineFollower = instance.GetComponent<SplineFollower>();
            _conveyor.AddFollower(splineFollower);
            Arrows.Add(splineFollower);
        }

        await UniTask.NextFrame(SceneLoadToken);

        var arrowPerDistance = length / arrowCount;
        for (var i = 0; i < arrowCount; i++)
        {
            var distance = arrowPerDistance * i;
            var travel = _splineComputer.Travel(0d, distance);
            var splineFollower = Arrows[i];
            splineFollower.SetPercent(travel);
        }
    }
}
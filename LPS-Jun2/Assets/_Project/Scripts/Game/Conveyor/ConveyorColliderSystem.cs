using Dreamteck.Splines;
using MessagePipe;
using UnityEngine;
using VContainer;

public sealed class ConveyorColliderSystem : SystemBase
{
    [Inject] private ISubscriber<LevelBuildCompleteMessage> _levelBuildCompleteSub;

    [Inject] private ConveyorConfig _conveyorConfig;
    [Inject] private SplineComputer _splineComputer;
    [Inject] private SplineMesh _splineMesh;
    [Inject] private RemoteConfigModule _remoteConfigModule;

    private BlockPhysicsConfig _blockPhysicsConfig;

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        base.BuildMessages(bag);

        _blockPhysicsConfig = _remoteConfigModule.GetDataClassNew<BlockPhysicsConfig>();
        _levelBuildCompleteSub.Subscribe(OnLevelBuildComplete).AddTo(bag);
    }

    private void OnLevelBuildComplete(LevelBuildCompleteMessage m)
    {
        var instance = Object.Instantiate(_conveyorConfig.ColliderPrefab, _splineComputer.transform);
        var splineMesh = instance.GetComponent<SplineMesh>();
        splineMesh.spline = _splineComputer;
        var originalChannel = _splineMesh.GetChannel(0);
        var channelCount = splineMesh.GetChannelCount();
        for (var i = 0; i < channelCount; i++)
        {
            var channel = splineMesh.GetChannel(i);
            channel.count = originalChannel.count;
        }

        if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.SandLoop)
        {
            var ceilChannel = splineMesh.GetChannel(3);
            ceilChannel.minOffset = ceilChannel.minOffset.WithY(.388f);
        }

        if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.SandLoopLite)
        {
            var ceilChannel = splineMesh.GetChannel(3);
            ceilChannel.minOffset = ceilChannel.minOffset.WithY(.49f);
        }

        if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.FlatCubes)
        {
            var ceilChannel = splineMesh.GetChannel(3);
            ceilChannel.minOffset = ceilChannel.minOffset.WithY(.55f);
        }
    }
}
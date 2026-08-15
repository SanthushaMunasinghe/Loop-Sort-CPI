using System.Collections.Generic;
using Dreamteck.Splines;
using MessagePipe;
using UnityEngine;
using VContainer;

public sealed class ConveyorEndMeshSystem : SystemBase
{
    [Inject] private ConveyorConfig _conveyorConfig;
    [Inject] private PrefabModule _prefabModule;
    [Inject] private SplineComputer _splineComputer;
    [Inject] private RemoteConfigModule _remoteConfigModule;

    [Inject] private ISubscriber<LevelBuildCompleteMessage> _levelBuildCompleteSub;

    private BlockPhysicsConfig _blockPhysicsConfig;

    public readonly List<GameObject> EndMeshes = new();

    public override void OnAwake()
    {
        base.OnAwake();

        _blockPhysicsConfig = _remoteConfigModule.GetDataClassNew<BlockPhysicsConfig>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _levelBuildCompleteSub.Subscribe(OnLevelBuildComplete).AddTo(bag);
    }

    private void OnLevelBuildComplete(LevelBuildCompleteMessage m)
    {
        if (_splineComputer.isClosed)
            return;

        Vector3 teleportPosition;
        Transform enterMeshT;
        {
            var instance = _prefabModule.Rent(_conveyorConfig.EndPrefab);
            var instanceT = instance.transform;
            var splineSample = _splineComputer.Evaluate(0d);
            instanceT.position = splineSample.position;
            instanceT.rotation = splineSample.rotation;
            instanceT.localScale = Vector3.one;
            EndMeshes.Add(instance);
            teleportPosition = splineSample.position;
            enterMeshT = instanceT;
        }
        {
            var instance = _prefabModule.Rent(_conveyorConfig.EndPrefab);
            var instanceT = instance.transform;
            var splineSample = _splineComputer.Evaluate(1d);
            instanceT.position = splineSample.position;
            instanceT.rotation = splineSample.rotation * Quaternion.AngleAxis(180f, Vector3.up);
            instanceT.localScale = Vector3.one;
            EndMeshes.Add(instance);

            if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.None) return;

            if (!instance.TryGetComponent<BlockTrigger>(out var blockTrigger))
                blockTrigger = instance.AddComponent<BlockTrigger>();
            blockTrigger.AddListener(block =>
            {
                var blockT = block.transform;
                var offset = blockT.position - instanceT.position;
                var teleportAngle = Vector3.SignedAngle(instanceT.forward, enterMeshT.forward, Vector3.up);
                var teleportRotation = Quaternion.AngleAxis(teleportAngle, Vector3.up);
                offset = teleportRotation * offset;
                blockT.position = teleportPosition + offset;
                block.Rigidbody.linearVelocity = enterMeshT.forward * block.Rigidbody.linearVelocity.magnitude;
            });
        }
    }
}
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Dreamteck.Splines;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine;
using VContainer;

public sealed class ConveyorSlot : GameBehaviourBase, IBlockContainer
{
    [Inject] private ConveyorConfig _config;
    [Inject] private AudioModule _audioModule;
    [Inject] private HapticModule _hapticModule;
    [Inject] private BlockConfig _blockConfig;
    [Inject] private RemoteConfigModule _remoteConfigModule;
    [Inject] private Conveyor _conveyor;
    [Inject] private CarrierConfig _carrierConfig;
    [Inject] private SceneScope _sceneScope;

    [Inject] private IPublisher<ConveyorSlotAddBlockMessage> _conveyorSlotAddBlockPub;
    [Inject] private IPublisher<ConveyorSlotRemoveBlockMessage> _conveyorSlotRemoveBlockPub;

    public SplineFollower SplineFollower { get; private set; }

    private int _addingElementCount;
    private Stash<BlockPhysicsActive> _blockPhysicsActives;

    private BlockPhysicsConfig _blockPhysicsConfig;

    private readonly List<Block> _blocks = new();

    protected override void Awake()
    {
        base.Awake();

        SplineFollower = GetComponent<SplineFollower>();
        _blockPhysicsConfig = _remoteConfigModule.GetDataClassNew<BlockPhysicsConfig>();
    }

    public override void OnRent()
    {
        base.OnRent();

        RegisterView<ConveyorSlot>();

        _blockPhysicsActives = World.GetStash<BlockPhysicsActive>();
    }

    public override void OnReturn()
    {
        base.OnReturn();

        _addingElementCount = 0;
        _blocks.Clear();
    }

    public async UniTask AddBlock(Block block, float delay = 0f, bool motion = true)
    {
        SplineFollower.enabled = true;
        block.SetContainer(this);

        var idx = _blocks.Count;
        _blocks.Add(block);
        var localPosition = Vector3.right * (idx * _config.SlotElementOffset);
        localPosition += Vector3.left * ((_conveyor.SlotElementCount - 1) * _config.SlotElementOffset / 2f);
        localPosition += Vector3.up * _config.SlotElementOffset / 2f;
        if (!_blockPhysicsActives.Has(block)) _blockPhysicsActives.Add(block);

        var isPhysicsActive = _blockPhysicsConfig.Type != BlockPhysicsConfig.PhysicsType.None;
        var targetPosition =
            isPhysicsActive ? transform.localToWorldMatrix.MultiplyPoint3x4(localPosition) : localPosition;
        var targetRotation = Quaternion.identity;

        _addingElementCount++;
        var blockT = block.transform;
        // blockT.parent = null;
        await UniTask.NextFrame(SceneLoadToken);
        await UniTask.Delay(TimeSpan.FromSeconds(delay), delayTiming: PlayerLoopTiming.FixedUpdate, cancellationToken: SceneLoadToken);
        blockT.parent = isPhysicsActive ? null : transform;

        var configSize = _carrierConfig.Sizes[_blockPhysicsConfig.Type];
        blockT.localScale = Vector3.one * configSize.ScaleMultiplier * _sceneScope.ConveyorBlockScaleMultiplier;

        block.MeshFilter.sharedMesh = _blockConfig.BeveledMesh;

        if (isPhysicsActive)
        {
            block.MeshFilter.sharedMesh =
                _blockPhysicsConfig.Sphere ? _blockConfig.SphereMesh : _blockConfig.BeveledMesh;
        }

        if (motion)
        {
            // _audioModule.GetPlayer()
            //     .WithPitch(.8f)
            //     .WithPitchIncrease(.03f, .5f)
            //     .WithMaxPitch(1f)
            //     .WithSkipCount(1)
            //     .Play(_audioModule.Sounds.AddBlock);
            _hapticModule.PlaySoft();
        }

        // if (sizeTypeOne)
        // {
        //     targetPosition += Vector3.up * .5f;
        //     var rotation1 = Quaternion.AngleAxis(90, Vector3.up);
        //     var rotation2 = Quaternion.AngleAxis(-90f, Vector3.up);
        //     targetRotation = Vector3.Dot(transform.right, blockT.forward) > 0f ? rotation1 : rotation2;
        // }

        await block.ApplyMoveToSlotMotion(targetPosition, targetRotation);

        _addingElementCount--;

        block.CompleteContainer();

        _conveyorSlotAddBlockPub.Publish(new ConveyorSlotAddBlockMessage
        {
            ConveyorSlot = this,
            Block = block,
        });
    }

    public void RemoveBlock(Block block)
    {
        block.ClearContainer();
        _blocks.Remove(block);

        _conveyorSlotRemoveBlockPub.Publish(new ConveyorSlotRemoveBlockMessage
        {
            ConveyorSlot = this,
            Block = block,
        });
    }

    public int GetIndexOf(Block block)
    {
        return _blocks.IndexOf(block);
    }

    public Block GetFirstBlock()
    {
        return _blocks.Count > 0 ? _blocks[0] : null;
    }

    public List<Block> GetElements()
    {
        return _blocks;
    }

    public bool HasSpace()
    {
        return _conveyor.SlotElementCount > _blocks.Count;
    }

    public bool HasElement()
    {
        return _blocks.Count > 0;
    }

    public bool IsElementBeingAdded()
    {
        return _addingElementCount > 0;
    }

    public bool IsCompatibleWith(ConveyorSlot slot)
    {
        var block = GetFirstBlock();
        var slotBlock = slot.GetFirstBlock();
        return block != null && slotBlock != null && block.IsCompatibleWith(slotBlock);
    }
}

public struct ConveyorSlotAddBlockMessage
{
    public ConveyorSlot ConveyorSlot;
    public Block Block;
}

public struct ConveyorSlotRemoveBlockMessage
{
    public ConveyorSlot ConveyorSlot;
    public Block Block;
}
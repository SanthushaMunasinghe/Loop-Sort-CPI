using System;
using System.Collections.Generic;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public sealed class BlockCarrierMeshSystem : SystemBase
{
    [Inject] private BlockConfig _blockConfig;
    [Inject] private Conveyor _conveyor;
    [Inject] private RemoteConfigModule _remoteConfigModule;
    [Inject] private SceneScope _sceneScope;

    [Inject] private ISubscriber<CarrierAddBlockMessage> _carrierAddBlockSub;
    [Inject] private ISubscriber<CarrierRemoveBlockMessage> _carrierRemoveBlockSub;
    [Inject] private ISubscriber<BlockCarrierMeshUpdateMessage> _blockCarrierMeshUpdateSub;
    [Inject] private ISubscriber<CarrierBlockCreateCompleteMessage> _carrierBlockCreateCompleteSub;

    [Inject] private IPublisher<CarrierBlockGroupUpdateMessage> _carrierBlockGroupUpdatePub;

    private readonly List<Rule> _rules = new();

    private bool _isBlockCreateComplete;
    private BlockGroupConfig _blockGroupConfig;

    private struct Rule
    {
        public Vector3Int[] Directions;
        public Quaternion Rotation;
        public Mesh Mesh;
    }

    public override void OnAwake()
    {
        base.OnAwake();

        RegisterRules();

        _blockGroupConfig = _remoteConfigModule.GetDataClassNew<BlockGroupConfig>();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _carrierAddBlockSub.Subscribe(OnCarrierAddBlock).AddTo(bag);
        _carrierRemoveBlockSub.Subscribe(OnCarrierRemoveBlock).AddTo(bag);
        _blockCarrierMeshUpdateSub.Subscribe(OnBlockCarrierMeshUpdate).AddTo(bag);
        _carrierBlockCreateCompleteSub.Subscribe(OnCarrierBlockCreateComplete).AddTo(bag);
    }

    private void OnCarrierAddBlock(CarrierAddBlockMessage m)
    {
        if (!_isBlockCreateComplete) return;

        var carrier = m.Carrier;
        var block = m.Block;

        var blockCoordinate = carrier.GetBlockCoordinate(block);
        using var p = ListPool<Block>.Get(out var blocks);
        carrier.GetBlocksInLayer(blockCoordinate.z, blocks);
        carrier.GetBlocksInLayer(blockCoordinate.z - 1, blocks);

        foreach (var b in blocks)
        {
            if (b.IsContainerModified()) continue;
            SetCarrierBlockMesh(carrier, b);
        }

        if (carrier.IsTransferringOrAddingBlocks()) return;
        HandleGroupBlocks(carrier);
    }

    private void OnCarrierRemoveBlock(CarrierRemoveBlockMessage m)
    {
        m.Block.MeshRenderer.enabled = true;
        HandleGroupBlocks(m.Carrier);
    }

    private void OnBlockCarrierMeshUpdate(BlockCarrierMeshUpdateMessage m)
    {
        var carrier = m.Carrier;
        var blocks = carrier.GetBlocks();
        foreach (var block in blocks)
        {
            SetCarrierBlockMesh(carrier, block);
        }

        HandleGroupBlocks(carrier);
    }

    private void OnCarrierBlockCreateComplete(CarrierBlockCreateCompleteMessage m)
    {
        _isBlockCreateComplete = true;
        foreach (var carrier in _sceneScope.AllCarriers)
        {
            var blocks = carrier.GetBlocks();
            foreach (var b in blocks)
                SetCarrierBlockMesh(carrier, b);
            HandleGroupBlocks(carrier);
        }
    }

    private void SetCarrierBlockMesh(Carrier carrier, Block block)
    {
        var blockCoordinate = carrier.GetBlockCoordinate(block);

        foreach (var rule in _rules)
        {
            var found = true;
            foreach (var direction in rule.Directions)
            {
                var offsetCoordinate = blockCoordinate + direction;

                if (!_blockGroupConfig.Combine)
                {
                    if (blockCoordinate.z % 2 == 0)
                    {
                        if (blockCoordinate.z > offsetCoordinate.z) continue;
                    }
                    else
                    {
                        if (offsetCoordinate.z > blockCoordinate.z) continue;
                    }
                }

                if (!carrier.IsBlockExists(offsetCoordinate)) continue;
                var offsetBlock = carrier.GetBlockAt(offsetCoordinate);
                if (!offsetBlock.IsCompatibleWith(block)) continue;

                found = false;
                break;
            }
            if (!found) continue;

            block.MeshFilter.sharedMesh = rule.Mesh;
            block.transform.localRotation = rule.Rotation;
            // block.transform.localScale = Vector3.one;
            block.MeshRenderer.enabled = true;

            break;
        }
    }

    private void HandleGroupBlocks(Carrier carrier)
    {
        var isGroupBlockEnabled = carrier.IsGroupBlockEnabled();
        var blocks = carrier.GetBlocks();
        var groupBlockCount = _conveyor.GroupBlockCount;
        var groupCount = blocks.Count / groupBlockCount;
        for (var i = 0; i < carrier.GroupBlocks.Count; i++)
        {
            var isActive = groupCount > i;
            var groupBlock = carrier.GroupBlocks[i];
            var groupBlockFilter = carrier.GroupBlockFilters[i];
            var groupBlockT = groupBlock.transform;

            var blockOffset = i * groupBlockCount;
            if (blocks.Count > blockOffset)
            {
                var previousBlock = blockOffset - 1 > 0 ? blocks[blockOffset - 1] : null;
                var targetBlock = blocks[blockOffset];
                var lastBlockOffset = blockOffset + groupBlockCount - 1;
                var lastBlock = lastBlockOffset < blocks.Count ? blocks[lastBlockOffset] : null;
                var nextBlockOffset = blockOffset + groupBlockCount;
                var nextBlock = nextBlockOffset < blocks.Count ? blocks[nextBlockOffset] : null;
                var nextHalfBlockOffset = blockOffset + groupBlockCount + groupBlockCount / 2;
                var nextHalfBlock = nextHalfBlockOffset < blocks.Count ? blocks[nextHalfBlockOffset] : null;

                using var p = ListPool<Material>.Get(out var materials);
                targetBlock.MeshRenderer.GetSharedMaterials(materials);
                groupBlock.SetSharedMaterials(materials);
                groupBlockT.localRotation = Quaternion.identity;
                groupBlockFilter.sharedMesh = _blockConfig.GroupBeveledMesh;

                if (_blockGroupConfig.Combine)
                {
                    var previousCompatible = previousBlock != null && previousBlock.IsCompatibleWith(targetBlock);
                    var nextCompatible = nextBlock != null && nextBlock.IsCompatibleWith(targetBlock);
                    if (previousCompatible && nextCompatible)
                    {
                        groupBlockFilter.sharedMesh = _blockConfig.GroupCenterMesh;
                    }
                    else if (previousCompatible)
                    {
                        groupBlockFilter.sharedMesh = _blockConfig.GroupCornerMesh;
                        groupBlockT.localRotation = Quaternion.AngleAxis(180, Vector3.up);
                    }
                    else if (nextCompatible)
                    {
                        groupBlockFilter.sharedMesh = _blockConfig.GroupCornerMesh;
                    }

                    if (lastBlock == null || !targetBlock.IsCompatibleWith(lastBlock))
                        isActive = false;

                    if (nextCompatible && nextHalfBlock == null)
                        isActive = false;
                }
            }

            groupBlock.gameObject.SetActive(isActive && isGroupBlockEnabled);

            for (var j = 0; j < groupBlockCount; j++)
            {
                var idx = blockOffset + j;
                if (idx >= blocks.Count) break;
                var block = blocks[idx];
                block.MeshRenderer.enabled = !isActive || !isGroupBlockEnabled;
            }
        }

        _carrierBlockGroupUpdatePub.Publish(new CarrierBlockGroupUpdateMessage { Carrier = carrier });
    }

    private void RegisterRules()
    {
        // Corner + Backward
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.up, Vector3Int.left, Vector3Int.back },
            Rotation = Quaternion.AngleAxis(90, Vector3.up),
            Mesh = _blockConfig.CornerMesh
        });
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.up, Vector3Int.right, Vector3Int.back },
            Rotation = Quaternion.AngleAxis(0, Vector3.up),
            Mesh = _blockConfig.CornerMesh
        });
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.down, Vector3Int.left, Vector3Int.back },
            Rotation = Quaternion.Euler(-90, 0, 90),
            Mesh = _blockConfig.CornerMesh
        });
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.down, Vector3Int.right, Vector3Int.back },
            Rotation = Quaternion.Euler(0, 0, -90),
            Mesh = _blockConfig.CornerMesh
        });

        // Corner + Forward
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.up, Vector3Int.left, Vector3Int.forward },
            Rotation = Quaternion.AngleAxis(180, Vector3.up),
            Mesh = _blockConfig.CornerMesh
        });
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.up, Vector3Int.right, Vector3Int.forward },
            Rotation = Quaternion.AngleAxis(-90, Vector3.up),
            Mesh = _blockConfig.CornerMesh
        });
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.down, Vector3Int.left, Vector3Int.forward },
            Rotation = Quaternion.Euler(0, 90, 180),
            Mesh = _blockConfig.CornerMesh
        });
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.down, Vector3Int.right, Vector3Int.forward },
            Rotation = Quaternion.Euler(0, 180, 180),
            Mesh = _blockConfig.CornerMesh
        });

        // Side Top
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.up, Vector3Int.left },
            Rotation = Quaternion.AngleAxis(90, Vector3.up),
            Mesh = _blockConfig.MiddleMesh
        });
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.up, Vector3Int.right },
            Rotation = Quaternion.AngleAxis(-90, Vector3.up),
            Mesh = _blockConfig.MiddleMesh
        });

        // Side Bottom
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.down, Vector3Int.left },
            Rotation = Quaternion.Euler(0, 90, 180),
            Mesh = _blockConfig.MiddleMesh
        });
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.down, Vector3Int.right },
            Rotation = Quaternion.Euler(-90, 0, -90),
            Mesh = _blockConfig.MiddleMesh
        });

        // Side Backward
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.back, Vector3Int.up },
            Rotation = Quaternion.AngleAxis(0, Vector3.up),
            Mesh = _blockConfig.MiddleMesh
        });
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.back, Vector3Int.down },
            Rotation = Quaternion.Euler(-90, 0, 0),
            Mesh = _blockConfig.MiddleMesh
        });
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.back, Vector3Int.left },
            Rotation = Quaternion.Euler(0, 0, 90),
            Mesh = _blockConfig.MiddleMesh
        });
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.back, Vector3Int.right },
            Rotation = Quaternion.Euler(0, 0, -90),
            Mesh = _blockConfig.MiddleMesh
        });

        // Side Forward
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.forward, Vector3Int.up },
            Rotation = Quaternion.AngleAxis(180, Vector3.up),
            Mesh = _blockConfig.MiddleMesh
        });
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.forward, Vector3Int.down },
            Rotation = Quaternion.Euler(180, 0, 0),
            Mesh = _blockConfig.MiddleMesh
        });
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.forward, Vector3Int.left },
            Rotation = Quaternion.Euler(0, 90, 90),
            Mesh = _blockConfig.MiddleMesh
        });
        _rules.Add(new Rule
        {
            Directions = new[] { Vector3Int.forward, Vector3Int.right },
            Rotation = Quaternion.Euler(0, -90, -90),
            Mesh = _blockConfig.MiddleMesh
        });

        // Center
        _rules.Add(new Rule
        {
            Directions = Array.Empty<Vector3Int>(),
            Rotation = Quaternion.identity,
            Mesh = _blockConfig.FlatMesh
        });
    }
}

public struct CarrierBlockGroupUpdateMessage
{
    public Carrier Carrier;
}
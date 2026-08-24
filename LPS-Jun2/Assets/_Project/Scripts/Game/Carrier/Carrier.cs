using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Lean.Touch;
using MessagePipe;
using Scellecs.Morpeh;
using StatefulUI.Runtime.Core;
using StatefulUISupport.Scripts.Components;
using UnityEngine;
using VContainer;

public sealed partial class Carrier : GameBehaviourBase, ITouchInteractable, IBlockContainer
{
    [field: SerializeField] public GameObject ModelParent { get; private set; }
    [field: SerializeField] public Transform BlockParent { get; private set; }
    [field: SerializeField] public Transform FrontPoint { get; private set; }
    [field: SerializeField] public Renderer HeadRenderer { get; private set; }
    [field: SerializeField] public SkinnedMeshRenderer BackTopRenderer { get; private set; }
    [field: SerializeField] public SkinnedMeshRenderer BackRearRenderer { get; private set; }
    [field: SerializeField] public StatefulComponent View { get; private set; }
    [field: SerializeField] public GameObject Highlight { get; private set; }
    [field: SerializeField] public Transform LeftTire { get; private set; }
    [field: SerializeField] public Transform RightTire { get; private set; }
    [field: SerializeField] public Transform TransferProjectPoint { get; private set; }
    [field: SerializeField] public Transform Pivot { get; private set; }

    [field: Header("Group Blocks")]
    [field: SerializeField] public List<MeshRenderer> GroupBlocks { get; private set; }
    [field: SerializeField] public List<MeshFilter> GroupBlockFilters { get; private set; }

    [field: Header("Additional Models")]
    [field: Tooltip("Extra renderers to paint alongside the head/back-top when Apply Carrier Modes " +
                    "is pressed. Each one is set to whatever colour the head renderer ends up with.")]
    [field: SerializeField] public List<Renderer> AdditionalModels { get; private set; }

    // Serialized so carriers placed by hand in a level test scene can be typed in the inspector.
    // SetType still wins for carriers spawned from the sheet.
    [field: SerializeField] public CarrierSheet.CarrierType Type { get; private set; }

    [field: Tooltip("What the Sandbox's Apply Carrier Modes button does to this carrier. Default " +
                    "leaves it as generated, Start fills it up, Empty clears it out. Nothing at run " +
                    "time reads this.")]
    [field: SerializeField] public CarrierMode Mode { get; private set; }

    [field: Tooltip("Empty mode only: the color this sink accepts when Only Compatible Color is on. " +
                    "Apply Carrier Modes paints the head with it.")]
    [field: SerializeField] public ColorType CompatibleColor { get; private set; }

    [field: Tooltip("Empty mode only: take in Compatible Color and let every other color ride past. " +
                    "Off means the sink swallows anything that reaches it.")]
    [field: SerializeField] public bool OnlyCompatibleColor { get; private set; }

    [field: Header("Start Fill")]
    [field: Tooltip("Start mode only: total colour-group count Apply Carrier Modes fills this " +
                    "carrier to (blocks = this x the level's blocks-per-group). 4 matches the " +
                    "level's own sizing exactly, so leaving it at 4 changes nothing. Apply Carrier " +
                    "Modes clones GroupBlocks slot 1 to permanently add visual capacity when this " +
                    "is higher.")]
    [field: SerializeField] public int StartGroupCount { get; private set; } = 4;

    [field: Tooltip("Start mode only: paints this carrier's groups from Color Pattern below, starting " +
                    "at the last group (the front, the first one this carrier actually dispenses) and " +
                    "working backward. Groups the pattern's blocks don't reach — or every group, if " +
                    "the asset is missing or empty — fall back to Override Start Color / Max " +
                    "Consecutive Same Color Groups below instead of looping the pattern back to its " +
                    "start.")]
    [field: SerializeField] public bool UseColorPattern { get; private set; }

    [field: SerializeField] public StartCarrierColorPattern ColorPattern { get; private set; }

    [field: Tooltip("Start mode only: paint the whole carrier one color instead of drawing per group " +
                    "at random — used for any groups Color Pattern above doesn't reach (or every " +
                    "group, when Color Pattern is off). Override Start Color Index indexes Block " +
                    "Colors directly (the full list, unaffected by Scene Scope's Color Range).")]
    [field: SerializeField] public bool OverrideStartColor { get; private set; }

    [field: SerializeField] public int OverrideStartColorIndex { get; private set; }

    [field: Tooltip("Start mode only: caps how many consecutive groups can land on the same color " +
                    "when randomly drawn. 0 leaves it uncapped (Scene Scope's Prevent Single Color " +
                    "Carriers is still the only guard). Ignored when Override Start Color is on.")]
    [field: SerializeField] public int MaxConsecutiveSameColorGroups { get; private set; }

    [field: Header("Empty Fill")]
    [field: Tooltip("Empty mode only: caps how many of the level's colour groups this sink needs " +
                    "filled before it completes and leaves — e.g. 2 out of the level's 4 groups " +
                    "completes it half full. 0 leaves it uncapped, filling every group same as before " +
                    "this field existed.")]
    [field: SerializeField] public int EmptyGroupLimit { get; private set; } = 4;

    [field: Header("Close Motion Override")]
    [field: Tooltip("Override this carrier's close-motion timing instead of using the defaults baked " +
                    "into ApplyCloseBackMotion. Turn on to speed up how fast the top roof (and any " +
                    "other blend-shape renderer) closes after this carrier completes, and how long it " +
                    "waits before starting, so it can leave the row sooner.")]
    [field: SerializeField] public bool OverrideCloseMotion { get; private set; }

    [field: Tooltip("Override only: seconds to wait after this carrier completes before the roof " +
                    "starts closing. Lower to have it start closing sooner.")]
    [field: SerializeField] public float CloseWaitTimeOverride { get; private set; }

    [field: Tooltip("Override only: seconds the top roof blend shape takes to close (replaces the " +
                    "default 0.6s). Lower to close — and leave — faster.")]
    [field: SerializeField] public float CloseSpeedOverride { get; private set; } = .6f;

    [Inject] private CarrierConfig _config;
    [Inject] private AudioModule _audioModule;
    [Inject] private HapticModule _hapticModule;
    [Inject] private BlockConfig _blockConfig;
    [Inject] private Particles _particles;
    [Inject] private Conveyor _conveyor;
    [Inject] private RemoteConfigModule _remoteConfigModule;
    [Inject] private CarrierConfig _carrierConfig;
    [Inject] private SceneScope _sceneScope;

    [Inject] private IPublisher<CarrierSelectMessage> _carrierSelectPub;
    [Inject] private IPublisher<CarrierCompleteMessage> _carrierCompletePub;
    [Inject] private IPublisher<CarrierBackClosedMessage> _carrierBackClosedPub;
    [Inject] private IPublisher<CarrierAddBlockMessage> _carrierAddBlockPub;
    [Inject] private IPublisher<CarrierRemoveBlockMessage> _carrierRemoveBlockPub;
    [Inject] private IPublisher<CarrierInteractUpdateMessage> _carrierInteractUpdatePub;
    [Inject] private IPublisher<BlockCarrierMeshUpdateMessage> _blockCarrierMeshUpdatePub;

    [Inject] private ISubscriber<BlockTransferCompleteMessage> _blockTransferCompleteSub;

    private Material _originalMaterial;
    private BlockPhysicsConfig _blockPhysicsConfig;
    private SoundConfig _soundConfig;

    private bool _isTransferring;
    private bool _isComplete;
    private bool _isInteractionLocked;
    private bool _isTransferLocked;
    private bool _isGroupBlockDisabled;
    private bool _isTransferMotionDisabled;
    private int _addingBlockCount;
    private int _customMaxBlockCount;
    private Vector3? _originalPosition;

    private Stash<BlockPhysicsActive> _blockPhysicsActives;

    private readonly List<Block> _blocks = new();
    private readonly Dictionary<Block, int> _idxByBlock = new();
    private readonly List<IBlockTransferHandler> _transferHandlers = new();
    private readonly HashSet<GameObject> _interactionBlockers = new();
    private readonly HashSet<GameObject> _transferBlockers = new();

    protected override void Awake()
    {
        base.Awake();

        _originalMaterial = HeadRenderer.sharedMaterials[0];
        GetComponentsInChildren(_transferHandlers);
    }

    public override void OnRent()
    {
        base.OnRent();

        RegisterView<Carrier>();
        InjectMaterial(_originalMaterial);
        if (IsSink()) ApplyTruckColor();
        ApplyOpenBackMotion(immediate: true);
        View.GetImage(ImageRole.Checkmark).gameObject.SetActive(false);

        foreach (var groupBlock in GroupBlocks)
            groupBlock.gameObject.SetActive(false);

        CaptureGroupSlideOriginals();

        Highlight.SetActive(false);

        _blockPhysicsActives = World.GetStash<BlockPhysicsActive>();

        _blockPhysicsConfig = _remoteConfigModule.GetDataClassNew<BlockPhysicsConfig>();
        _soundConfig = _remoteConfigModule.GetDataClassNew<SoundConfig>();
    }

    public override void OnReturn()
    {
        base.OnReturn();

        _blocks.Clear();
        _idxByBlock.Clear();
        _isTransferring = false;
        _isComplete = false;
        _isInteractionLocked = false;
        _interactionBlockers.Clear();
        _isTransferLocked = false;
        _transferBlockers.Clear();
        _isGroupBlockDisabled = false;
        _isTransferMotionDisabled = false;
        _addingBlockCount = 0;
        _customMaxBlockCount = 0;
        _originalPosition = null;

        RestoreGroupSlidePositions();
    }

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        base.BuildMessages(bag);

        _blockTransferCompleteSub.Subscribe(OnBlockTransferComplete).AddTo(bag);
    }

    private void OnBlockTransferComplete(BlockTransferCompleteMessage m)
    {
        if (m.Carrier != this) return;

        ApplyGroupSlideMotion();
    }

    public void Interact(LeanFinger finger, RaycastHit hitInfo)
    {
        if (_isInteractionLocked)
            return;

        // A sink is not selectable at all — there is nothing to hand out.
        if (!CanTakeOutBlocks())
            return;

        RunClickTransferDelay().Forget();
    }

    /// <summary>
    /// Locks this carrier out of further clicks for SceneScope's CarrierTransferClickDelay, then
    /// selects it — the same _isInteractionLocked flag other components already use to gate Interact,
    /// so nothing further is needed to stop a second click landing mid-delay.
    /// </summary>
    private async UniTaskVoid RunClickTransferDelay()
    {
        DisableInteraction(gameObject);

        await UniTask.Delay(TimeSpan.FromSeconds(_sceneScope.CarrierTransferClickDelay), cancellationToken: SceneLoadToken);

        EnableInteraction(gameObject);

        _carrierSelectPub.Publish(new CarrierSelectMessage
        {
            Carrier = this
        });
    }

    public void SetType(CarrierSheet.CarrierType carrierType)
    {
        Type = carrierType;
    }

    /// <summary>
    /// Rewrites this sink's accepted color — field-only, the same way Block.OverrideColorType works.
    /// SceneScope calls this from its own Awake (-100), ahead of this carrier's own OnRent (0), so
    /// the reroll lands before anything renders. OnRent resolves the matching truck color and paints
    /// this carrier with it — see ApplyTruckColor.
    /// </summary>
    public void SetCompatibleColor(ColorType colorType)
    {
        CompatibleColor = colorType;
    }

#if UNITY_EDITOR
    /// <summary>Used by LevelSandboxGenerator; Type is serialized so it survives the scene save.</summary>
    public void EditorSetType(CarrierSheet.CarrierType carrierType)
    {
        Type = carrierType;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>Used by the Empty Carrier Rows generator to mark spawned carriers as sinks.</summary>
    public void EditorSetMode(CarrierMode mode)
    {
        Mode = mode;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>Used by the Empty Carrier Rows generator to set up a randomized colour sink.</summary>
    public void EditorSetCompatibleColor(ColorType colorType, bool onlyCompatibleColor)
    {
        CompatibleColor = colorType;
        OnlyCompatibleColor = onlyCompatibleColor;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>Used by the Empty Carrier Rows generator to set this sink's group fill cap.</summary>
    public void EditorSetEmptyGroupLimit(int groupLimit)
    {
        EmptyGroupLimit = groupLimit;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    /// <summary>
    /// Registers blocks that were generated into BlockParent in the editor.
    ///
    /// This is the no-motion, no-messages half of <see cref="AddBlock"/>: the generator has already
    /// done the parenting, positioning, scaling and mesh assignment, so all that is left is the
    /// bookkeeping and the physics state a block needs while it is sitting in a carrier. Called by
    /// LevelSandbox before the build messages go out.
    /// </summary>
    public void AdoptAuthoredBlocks()
    {
        if (BlockParent == null) return;

        _blocks.Clear();
        _idxByBlock.Clear();

        using var pooled = UnityEngine.Pool.ListPool<Block>.Get(out var authoredBlocks);
        GetAuthoredBlocksInOrder(BlockParent, authoredBlocks);

        foreach (var block in authoredBlocks)
        {
            block.SetContainer(this);

            block.Rigidbody.isKinematic = true;
            block.Rigidbody.detectCollisions = false;
            block.Collider.enabled = false;

            var index = _blocks.Count;
            _blocks.Add(block);
            _idxByBlock[block] = index;

            if (_blockPhysicsActives.Has(block)) _blockPhysicsActives.Remove(block);

            block.CompleteContainer();
        }

        if (CanComplete()) SetComplete();
    }

    /// <summary>
    /// Walks blockParent's children in hierarchy order collecting every Block, recursing into any
    /// child that isn't a Block itself. Flat carriers have blocks as direct children; the Sandbox's
    /// Apply Carrier Modes Start fill groups them one level deeper under a container per group block
    /// (see LevelSandboxGenerator.FillCarrier), so this keeps every reader of a carrier's authored
    /// blocks working the same way for both layouts.
    /// </summary>
    public static void GetAuthoredBlocksInOrder(Transform blockParent, List<Block> result)
    {
        for (var i = 0; i < blockParent.childCount; i++)
        {
            var child = blockParent.GetChild(i);
            if (child.TryGetComponent<Block>(out var block))
                result.Add(block);
            else
                GetAuthoredBlocksInOrder(child, result);
        }
    }

    public async UniTask AddBlock(Block block, float delay = 0f, bool motion = true)
    {
        block.SetContainer(this);

        block.Rigidbody.isKinematic = true;
        block.Rigidbody.detectCollisions = false;
        block.Collider.enabled = false;

        _addingBlockCount++;

        var index = _blocks.Count;
        _blocks.Add(block);
        _idxByBlock[block] = index;
        var coordinate = GetBlockCoordinate(index);

        if (_blockPhysicsActives.Has(block)) _blockPhysicsActives.Remove(block);

        if (CanComplete()) SetComplete();

        var blockT = block.transform;
        blockT.parent = null;
        if (motion) await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: SceneLoadToken);
        blockT.parent = BlockParent;

        if (motion)
        {
            if (_soundConfig.Stack)
            {
                _audioModule.GetPlayer()
                    .WithPitch(.85f)
                    .WithPitchIncrease(.01f, .5f)
                    .WithCooldown(.06f)
                    .WithVolumeScale(.5f)
                    .WithMaxPitch(1.2f)
                    .Play(_audioModule.Sounds.AddBlock);
            }
            else if (_soundConfig.Stackv2)
            {
                _audioModule.GetPlayer()
                    .WithPitch(.6f)
                    .WithPitchIncrease(.02f, 1f)
                    .WithCooldown(.06f)
                    .WithVolumeScale(.5f)
                    .WithMaxPitch(1.4f)
                    .Play(_audioModule.Sounds.AddBlock);
            }

            _hapticModule.PlaySoft();
        }

        var targetPosition = CoordinateToLocalPosition(coordinate);
        var targetRotation = Quaternion.identity;

        var configSize = _carrierConfig.Sizes[_blockPhysicsConfig.Type];
        blockT.localScale = Vector3.one * configSize.ScaleMultiplier;

        block.MeshFilter.sharedMesh = _blockConfig.BeveledMesh;
        if (motion)
        {
            await block.ApplyMoveToCarrierMotion(targetPosition, targetRotation);
            ApplyAddBlockMotion();

            if (IsSink()) _sceneScope.PlayBlockStoredSound();
        }
        else
        {
            blockT.localPosition = targetPosition;
            blockT.localRotation = Quaternion.identity;
        }

        _addingBlockCount--;

        block.CompleteContainer();

        _carrierAddBlockPub.Publish(new CarrierAddBlockMessage
        {
            Carrier = this,
            Block = block,
        });
    }

    public void RemoveBlock(Block block)
    {
        block.ClearContainer();
        _blocks.Remove(block);
        _idxByBlock.Remove(block);

        _carrierRemoveBlockPub.Publish(new CarrierRemoveBlockMessage
        {
            Carrier = this,
            Block = block,
        });
    }

    public List<Block> GetBlocks()
    {
        return _blocks;
    }

    public int GetBlockIdx(Block block)
    {
        return _idxByBlock[block];
    }

    private void SetComplete()
    {
        if (_isComplete) return;
        _isComplete = true;

        // var colorType = GetCompleteColorType();
        // InjectColorType(colorType);

        SetCompleteTransferDelay();

        _carrierCompletePub.Publish(new CarrierCompleteMessage
        {
            Carrier = this
        });
    }

    private async UniTaskVoid SetCompleteTransferDelay()
    {
        while (IsTransferringOrAddingBlocks())
            await UniTask.Yield(cancellationToken: SceneLoadToken);

        ApplyCheckmarkMotion();

        if (OverrideCloseMotion && CloseWaitTimeOverride > 0f)
            await UniTask.Delay(TimeSpan.FromSeconds(CloseWaitTimeOverride), cancellationToken: SceneLoadToken);

        var closeDuration = OverrideCloseMotion ? CloseSpeedOverride : .6f;
        await ApplyCloseBackMotion(closeWeight: GetCloseBackWeight(), closeDuration: closeDuration);

        _carrierBackClosedPub.Publish(new CarrierBackClosedMessage
        {
            Carrier = this
        });

        var carrierCompleteSound = _soundConfig.Stack
            ? _audioModule.Sounds.CarrierCompleteAb
            : _audioModule.Sounds.CarrierComplete;
        _audioModule.GetPlayer().Play(carrierCompleteSound);

        var checkmark = View.GetImage(ImageRole.Checkmark);
        PrefabModule.Rent(_particles.CarrierComplete, checkmark.transform.position, Quaternion.identity);
    }

    /// <summary>
    /// Looks up this sink's truck color — SceneScope's TruckColors entry at whatever index
    /// CompatibleColor sits at in BlockColors, a separate Colors asset entry from the color this
    /// sink actually accepts, then paints the head, back-top, back-rear and every AdditionalModels
    /// renderer with its material. Falls back to the accepted color itself when the index can't be
    /// resolved (CompatibleColor isn't in BlockColors, or TruckColors is shorter than BlockColors).
    /// </summary>
    private void ApplyTruckColor()
    {
        var blockColors = _sceneScope.BlockColors;
        var truckColors = _sceneScope.TruckColors;

        var truckColorType = CompatibleColor;
        for (var i = 0; i < blockColors.Count; i++)
        {
            if (blockColors[i] != CompatibleColor) continue;
            if (i < truckColors.Count) truckColorType = truckColors[i];
            break;
        }

        var material = Colors.Get(truckColorType).Material;
        if (material == null) return;

        ApplyTruckMaterial(HeadRenderer, material);
        ApplyTruckMaterial(BackTopRenderer, material);
        ApplyTruckMaterial(BackRearRenderer, material);

        if (AdditionalModels == null) return;
        foreach (var renderer in AdditionalModels)
            ApplyTruckMaterial(renderer, material);
    }

    /// <summary>
    /// One direct material-slot-0 swap, used identically for every truck-painted renderer (head,
    /// back-top, back-rear, AdditionalModels) so none of them can end up out of sync with the others.
    /// </summary>
    private static void ApplyTruckMaterial(Renderer renderer, Material material)
    {
        if (renderer == null) return;

        using var p = UnityEngine.Pool.ListPool<Material>.Get(out var materials);
        renderer.GetSharedMaterials(materials);
        if (materials.Count == 0) return;

        materials[0] = material;
        renderer.SetSharedMaterials(materials);
        renderer.SetPropertyBlock(null, 0);
    }

    public void EnableInteraction(GameObject source)
    {
        _interactionBlockers.Remove(source);
        if (_interactionBlockers.Count > 0) return;
        _isInteractionLocked = false;
        _carrierInteractUpdatePub.Publish(new CarrierInteractUpdateMessage
        {
            Carrier = this,
            CanInteract = CanInteract(),
        });
    }

    public void DisableInteraction(GameObject source)
    {
        _interactionBlockers.Add(source);
        _isInteractionLocked = true;
        _carrierInteractUpdatePub.Publish(new CarrierInteractUpdateMessage
        {
            Carrier = this,
            CanInteract = CanInteract(),
        });
    }

    public void EnableTransfer(GameObject source)
    {
        _transferBlockers.Remove(source);
        if (_transferBlockers.Count > 0) return;
        _isTransferLocked = false;
    }

    public void DisableTransfer(GameObject source)
    {
        _transferBlockers.Add(source);
        _isTransferLocked = true;
    }

    public void BeginTransfer()
    {
        _isTransferring = true;
        ApplyBeginTransferMotion();
    }

    public void EndTransfer()
    {
        _isTransferring = false;
        ApplyEndTransferMotion();
    }

    private int CoordinateToIndex(Vector3Int coordinate)
    {
        var x = coordinate.y % 2 == 0 ? coordinate.x : _conveyor.BlockSize.x - 1 - coordinate.x;
        var y = coordinate.y * _conveyor.BlockSize.x;
        var z = coordinate.z * _conveyor.BlockSize.x * _conveyor.BlockSize.y;
        return x + y + z;
    }

    public Vector3Int GetBlockCoordinate(int index)
    {
        var coordinate = Vector3Int.zero;
        var idx = index % (_conveyor.BlockSize.x * _conveyor.BlockSize.y);
        coordinate.x = idx % _conveyor.BlockSize.x;
        coordinate.y = idx / _conveyor.BlockSize.x;
        coordinate.z = index / (_conveyor.BlockSize.x * _conveyor.BlockSize.y);

        if (coordinate.y % 2 != 0)
            coordinate.x = _conveyor.BlockSize.x - 1 - idx % _conveyor.BlockSize.x;

        return coordinate;
    }

    public Vector3Int GetBlockCoordinate(Block block)
    {
        var idx = GetBlockIdx(block);
        return GetBlockCoordinate(idx);
    }

    public Vector3 CoordinateToLocalPosition(Vector3Int coordinate)
    {
        var configSize = _config.Sizes[_blockPhysicsConfig.Type];
        var size = configSize.ContainerSize;
        var x = Mathf.Lerp(0f, size.x, coordinate.x / (float)(_conveyor.BlockSize.x - 1));
        var y = Mathf.Lerp(0f, size.y, coordinate.y / (float)(_conveyor.BlockSize.y - 1));
        var z = Mathf.Lerp(0f, size.z, coordinate.z / (float)(_conveyor.BlockSize.z - 1));
        x = float.IsNaN(x) ? 0f : x;
        y = float.IsNaN(y) ? 0f : y;
        z = float.IsNaN(z) ? 0f : z;
        var offset = configSize.ContainerOffset;
        x += offset.x;
        y += offset.y;
        z += offset.z;
        return new Vector3(x, y, z);
    }

    public bool IsBlockExists(Vector3Int coordinate)
    {
        if (coordinate.x < 0 || coordinate.x >= _conveyor.BlockSize.x) return false;
        if (coordinate.y < 0 || coordinate.y >= _conveyor.BlockSize.y) return false;
        if (coordinate.z < 0 || coordinate.z >= _conveyor.BlockSize.z) return false;
        var idx = CoordinateToIndex(coordinate);
        return idx >= 0 && idx < _blocks.Count;
    }

    public Block GetBlockAt(Vector3Int coordinate)
    {
        var idx = CoordinateToIndex(coordinate);
        if (idx < 0 || idx >= _blocks.Count) return null;
        return _blocks[idx];
    }

    public void GetNeighborBlocks(Vector3Int coordinate, List<Block> neighbors)
    {
        var up = GetBlockAt(coordinate + Vector3Int.up);
        if (up != null) neighbors.Add(up);
        var down = GetBlockAt(coordinate + Vector3Int.down);
        if (down != null) neighbors.Add(down);
        var front = GetBlockAt(coordinate + Vector3Int.forward);
        if (front != null) neighbors.Add(front);
        var back = GetBlockAt(coordinate + Vector3Int.back);
        if (back != null) neighbors.Add(back);
        var right = GetBlockAt(coordinate + Vector3Int.right);
        if (right != null) neighbors.Add(right);
        var left = GetBlockAt(coordinate + Vector3Int.left);
        if (left != null) neighbors.Add(left);
    }

    public void GetBlocksInLayer(int z, List<Block> blocks)
    {
        if (z < 0 || z >= _conveyor.BlockSize.z) return;
        for (var x = 0; x < _conveyor.BlockSize.x; x++)
        for (var y = 0; y < _conveyor.BlockSize.y; y++)
        {
            var coordinate = new Vector3Int(x, y, z);
            var block = GetBlockAt(coordinate);
            if (block != null) blocks.Add(block);
        }
    }

    public void SetCustomMaxBlockCount(int count)
    {
        _customMaxBlockCount = count;
    }

    public bool IsFull()
    {
        var maxBlockCount = GetMaxBlockCount();
        return _blocks.Count >= maxBlockCount;
    }

    public bool HasReachedCompleteCount()
    {
        var maxBlockCount = _conveyor.MaxBlockCount;
        return _blocks.Count >= maxBlockCount;
    }

    public bool IsEmpty()
    {
        return _blocks.Count == 0;
    }

    public int GetMaxBlockCount()
    {
        if (_customMaxBlockCount > 0) return _customMaxBlockCount;

        var maxBlockCount = _conveyor.MaxBlockCount;
        if (Mode == CarrierMode.Empty && EmptyGroupLimit > 0 && _conveyor.GroupBlockCount > 0)
            maxBlockCount = Mathf.Min(maxBlockCount, EmptyGroupLimit * _conveyor.GroupBlockCount);

        return maxBlockCount;
    }

    /// <summary>Empty mode only: how far ApplyCloseBackMotion's top blend shape should close when this
    /// sink completes — scaled down from a full 100 by EmptyGroupLimit's fraction of the level's own
    /// group count, so a sink capped to half its groups visually closes only halfway.</summary>
    public float GetCloseBackWeight()
    {
        if (Mode != CarrierMode.Empty || EmptyGroupLimit <= 0 || _conveyor.GroupBlockCount <= 0)
            return 100f;

        var totalGroupCount = _conveyor.MaxBlockCount / (float)_conveyor.GroupBlockCount;
        if (totalGroupCount <= 0f) return 100f;

        return Mathf.Clamp01(EmptyGroupLimit / totalGroupCount) * 100f;
    }

    public int GetAvailableSpaceCount()
    {
        var maxBlockCount = GetMaxBlockCount();
        return maxBlockCount - _blocks.Count;
    }

    public ColorType GetNextColorType()
    {
        if (IsEmpty()) return new ColorType(BaseColor.None);
        var nextBlock = _blocks[^1];
        return nextBlock.ColorType;
    }

    public ColorType GetCompleteColorType()
    {
        return GetNextColorType();
    }

    public void FindBlocksWithSameColor(ICollection<Block> blocks)
    {
        var nextColorType = GetNextColorType();
        for (var i = _blocks.Count - 1; i >= 0; i--)
        {
            var block = _blocks[i];
            if (block.ColorType != nextColorType) break;
            blocks.Add(block);
        }
    }

    public void FindNextTransferBlocks(ICollection<Block> blocks)
    {
        var nextColorType = GetNextColorType();
        for (var i = _blocks.Count - 1; i >= 0; i--)
        {
            var block = _blocks[i];
            if (!block.CanBeginTransfer()) break;
            if (block.ColorType != nextColorType) break;
            blocks.Add(block);
            if (blocks.Count >= GetMaxBlockCount()) break;
        }
    }

    public Block GetNextFirstBlock()
    {
        return _blocks.Count > 0 ? _blocks[^1] : null;
    }

    public bool AreAllBlocksSameColor()
    {
        var nextColorType = GetNextColorType();
        foreach (var block in _blocks)
            if (block.ColorType != nextColorType)
                return false;

        return true;
    }

    /// <summary>A Start carrier is a source: its trigger never picks anything up.</summary>
    public bool CanTakeInBlocks()
    {
        return Mode != CarrierMode.Start;
    }

    /// <summary>
    /// An Empty carrier is a sink: what goes in never comes back out, and what it will have is its
    /// own business rather than the usual colour match — see <see cref="CanTransferBlock"/>.
    /// </summary>
    public bool IsSink()
    {
        return Mode == CarrierMode.Empty;
    }

    public bool CanTakeOutBlocks()
    {
        return !IsSink();
    }

    public bool CanComplete()
    {
        // A source only ever hands blocks out, so it never closes — not even when what is left in it
        // happens to be one colour.
        if (Mode == CarrierMode.Start) return false;

        // A sink takes any colour, so being full is the only completion rule that means anything.
        if (Mode == CarrierMode.Empty) return IsFull();

        return HasReachedCompleteCount() && AreAllBlocksSameColor();
    }

    public bool CanTransferBlock(Block block)
    {
        if (!CanTakeInBlocks()) return false;

        // A restricted sink takes its one colour and lets everything else ride past.
        if (IsSink() && OnlyCompatibleColor && block.ColorType != CompatibleColor) return false;

        if (_transferHandlers.Count == 0) return true;
        foreach (var handler in _transferHandlers)
            if (!handler.CanTransferBlock(block))
                return false;
        return true;
    }

    public bool IsBetterCarrier(Block block)
    {
        if (_transferHandlers.Count == 0) return false;
        foreach (var handler in _transferHandlers)
            if (!handler.IsBetterCarrier(block))
                return false;
        return true;
    }

    public bool CanBeginTransfer()
    {
        return !_isTransferLocked;
    }

    public bool IsTransferringOrAddingBlocks()
    {
        return _isTransferring || _addingBlockCount > 0;
    }

    public bool IsTransferring()
    {
        return _isTransferring;
    }

    public bool CanInteract()
    {
        return !_isInteractionLocked;
    }

    public bool IsComplete()
    {
        return _isComplete;
    }

    public MeshRenderer GetGroupBlockOf(Block block)
    {
        var blockIdx = GetBlockIdx(block);
        var groupBlockIdx = blockIdx / _conveyor.GroupBlockCount;
        return GroupBlocks[groupBlockIdx];
    }

    public void DisableGroupBlock()
    {
        _isGroupBlockDisabled = true;
        _blockCarrierMeshUpdatePub.Publish(new BlockCarrierMeshUpdateMessage { Carrier = this });
    }

    public void EnableGroupBlock()
    {
        _isGroupBlockDisabled = false;
        _blockCarrierMeshUpdatePub.Publish(new BlockCarrierMeshUpdateMessage { Carrier = this });
    }

    public void RefreshGroupBlock()
    {
        _blockCarrierMeshUpdatePub.Publish(new BlockCarrierMeshUpdateMessage { Carrier = this });
    }

    public bool IsGroupBlockEnabled()
    {
        return !_isGroupBlockDisabled;
    }

    public void EnableTransferMotion()
    {
        _isTransferMotionDisabled = false;
    }

    public void DisableTransferMotion()
    {
        _isTransferMotionDisabled = true;
    }
}

/// <summary>
/// What the Sandbox's Apply Carrier Modes button should make of a carrier. Purely an instruction to
/// that button — selecting, sorting and transfer treat every mode alike.
/// </summary>
public enum CarrierMode
{
    /// <summary>Left exactly as the level generated it.</summary>
    Default = 0,

    /// <summary>Filled to the carrier's full block count.</summary>
    Start = 1,

    /// <summary>Emptied of every block.</summary>
    Empty = 2,
}

public struct CarrierSelectMessage
{
    public Carrier Carrier;
}

public struct CarrierCompleteMessage
{
    public Carrier Carrier;
}

/// <summary>Published once ApplyCloseBackMotion's top blend shape finishes closing after a
/// completion — the signal EmptyCarrierRowExit waits on before starting a row carrier's exit.</summary>
public struct CarrierBackClosedMessage
{
    public Carrier Carrier;
}

public struct CarrierAddBlockMessage
{
    public Carrier Carrier;
    public Block Block;
}

public struct CarrierRemoveBlockMessage
{
    public Carrier Carrier;
    public Block Block;
}

public struct CarrierInteractUpdateMessage
{
    public Carrier Carrier;
    public bool CanInteract;
}
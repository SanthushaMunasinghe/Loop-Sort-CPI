using System;
using System.Collections.Generic;
using System.Linq;
using Dreamteck.Splines;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using VContainer.Unity;

/// <summary>
/// The one and only container for a level sandbox scene.
///
/// This used to be a child of MonitorScope under BootstrapScope, and pulled its data, modules and
/// sheets from there. It is now self sufficient: everything it needs is either a serialized
/// reference on this component or a component already sitting in the scene. There is no Bootstrap
/// scene, no UI stack, no DontDestroyOnLoad object and no Resources loading in the play path.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class SceneScope : LifetimeScope
{
    [Header("Data")]
    [Tooltip("Data assets to register, exactly as DataInstaller used to. Colors, Sounds, Particles, " +
             "BlockConfig, CarrierConfig and ConveyorConfig are the minimum for the transfer loop.")]
    [SerializeField] private Data[] _data;

    [Tooltip("Visual theme handed to every GameBehaviourBase.")]
    [SerializeField] private ThemeType _theme = ThemeType.Default;

    [Header("Colors")]
    [Tooltip("Palette the level is repainted with every time you press Play. Needs a matching entry " +
             "in the Colors asset above; entries that have none are skipped.")]
    [SerializeField] private List<ColorType> _blockColors = new();

    [Tooltip("Paint for an Empty carrier's own body (head/back-top/back-rear/AdditionalModels), " +
             "index-paired with Block Colors above — same length, entry N here is what an Empty " +
             "carrier looks like when it randomly draws Block Colors entry N as its compatible " +
             "color. Needs a matching entry in the Colors asset above, same as Block Colors.")]
    [SerializeField] private List<ColorType> _truckColors = new();

    [Tooltip("Reroll one of a carrier's color groups when the draw gives it every block in the same " +
             "color. Needs two usable Block Colors and two groups in the carrier to do anything.")]
    [SerializeField] private bool _preventSingleColorCarriers = true;

    [Tooltip("Restricts random color draws (Start carrier groups, Empty carrier compatible colors, " +
             "and the row/carrier consecutive-cap rerolls) to a sub-range of Block Colors, by index, " +
             "inclusive. -1 on either end means no cap on that side. Explicit color overrides (Row " +
             "Color Override, Carrier Override Start Color) always index the full list regardless.")]
    [SerializeField] private int _colorRangeStart = -1;
    [SerializeField] private int _colorRangeEnd = -1;

    [Header("Carriers")]
    [Tooltip("Start mode: how long the group slide-forward animation takes after a transfer batch " +
             "fully drains a colour group.")]
    [SerializeField] private float _groupSlideDuration = .25f;

    [Tooltip("Empty Carrier Rows: how long a row's completed carrier takes to lerp onto its row's " +
             "spline (point 1) and ride it to the last point, once its lid finishes closing.")]
    [SerializeField] private float _emptyCarrierExitDuration = 1.5f;

    [Tooltip("How long a carrier waits after being clicked before its blocks jump to the conveyor. " +
             "The carrier can't be clicked again until this elapses.")]
    [SerializeField] private float _carrierTransferClickDelay = .5f;

    [Tooltip("Carriers this scene treats as block sources. Only carriers in this list or Empty " +
             "Carriers actually function at runtime — everything else is inert. Populated by hand.")]
    [SerializeField] private List<Carrier> _startCarriers = new();

    [Tooltip("Carriers this scene treats as block sinks. Only carriers in this list or Start " +
             "Carriers actually function at runtime — everything else is inert. Populated by hand.")]
    [SerializeField] private List<Carrier> _emptyCarriers = new();

    [Tooltip("When enabled, each row below offers one carrier at a time — the first of that row " +
             "still holding space. Filling it hands the row on to the next carrier. When disabled, " +
             "every Empty Carriers entry above is available as normal and rows are ignored.")]
    [SerializeField] private bool _useEmptyCarrierRows;

    [Tooltip("Row groupings for the toggle above, front-to-back per row. Populated by the Level " +
             "Sandbox's Generate Empty Carrier Rows button.")]
    [SerializeField] private List<EmptyCarrierRow> _emptyCarrierRows = new();

    // Self-registered by each EmptyCarrierRowExit in its own Awake — not hand-populated, so this
    // stays correct across however many rows the Level Sandbox generates without any manual wiring.
    private readonly List<EmptyCarrierRowExit> _emptyCarrierRowExits = new();

    [Header("Conveyor")]
    [Tooltip("Scales a block on top of its normal size while it's jumping to and sitting on the " +
             "conveyor belt. Reset to a block's normal scale the moment it lands back in a carrier.")]
    [SerializeField] private float _conveyorBlockScaleMultiplier = 1f;

    [Tooltip("Overrides the conveyor's baked movement speed for this scene. Leave at 0 to keep the " +
             "speed generated from the spline length (LevelSandbox.ConveyorSpeed).")]
    [SerializeField] private float _conveyorSpeedOverride;

    [Tooltip("Caps how many blocks the conveyor can hold across all its slots at once. Leave at 0 " +
             "for no cap beyond what the belt's slot geometry already allows.")]
    [SerializeField] private int _conveyorMaxBlockCount;

    [Tooltip("Overrides how many blocks sit side by side in a single slot (the belt's lane width). " +
             "Leave at 0 to use CarrierConfig's SlotElementCount for the active physics type.")]
    [SerializeField] private int _conveyorSlotElementCountOverride;

    [Tooltip("Overrides the gap between blocks sitting side by side in a slot. Raising Slot Element " +
             "Count widens that row by (count - 1) * this value, so shrink this to keep it inside the " +
             "belt. Leave at 0 to use ConveyorConfig's SlotElementOffset.")]
    [SerializeField] private float _conveyorSlotElementOffsetOverride;

    [Header("Scene")]
    [Tooltip("Your camera. InteractionModule raycasts through it — without one there is no input.")]
    [SerializeField] private Camera _camera;

    [SerializeField] private Conveyor _conveyor;
    [SerializeField] private SplineComputer _splineComputer;
    [SerializeField] private SplineMesh _splineMesh;

    [Tooltip("The single global pickup trigger blocks pass through to be routed to a compatible, " +
             "unfinished Empty carrier anywhere in the level. Hand-placed in the scene, not generated.")]
    [SerializeField] private GlobalTrigger _globalTrigger;

    [Tooltip("Hand-placed pointer GameObject toggled on/off by ShortcutManager's P key.")]
    [SerializeField] private GameObject _pointer;

    [Tooltip("Played each time a block finishes its transition onto an Empty carrier (a sink) — i.e. " +
             "every time a block gets stored on the truck.")]
    [SerializeField] private AudioSource _blockStoredAudioSource;

    [Tooltip("Minimum time between Block Stored Audio Source plays. Blocks that get stored before " +
             "this elapses since the last play are silent instead of retriggering the clip.")]
    [SerializeField] private float _blockStoredAudioMinInterval = .1f;

    [Tooltip("Played each time a block finishes its jump motion onto a conveyor slot — i.e. every " +
             "time a block lands on the conveyor belt.")]
    [SerializeField] private AudioSource _blockConveyorJumpAudioSource;

    [Tooltip("Minimum time between Block Conveyor Jump Audio Source plays. Blocks that land on the " +
             "belt before this elapses since the last play are silent instead of retriggering the clip.")]
    [SerializeField] private float _blockConveyorJumpAudioMinInterval = .1f;

    [Header("Systems")]
    [Tooltip("Only these SystemBase types are constructed. Everything else in the project is ignored.")]
    [SerializeField]
    private string[] _systems =
    {
        nameof(ConveyorSpeedSystem),
        nameof(ConveyorSlotCreateSystem),
        nameof(BlockPhysicsSystem),
        nameof(BlockTransferSystem),
        nameof(BlockTriggerSystem),
        nameof(CarrierSelectSystem),
        nameof(BlockCarrierMeshSystem),
        nameof(BlockNextTransferSystem),
    };

    /// <summary>Also what the Sandbox's Apply Carrier Modes button fills a Start carrier from.</summary>
    public IReadOnlyList<ColorType> BlockColors => _blockColors;

    /// <summary>Index-paired with BlockColors — see the field's tooltip.</summary>
    public IReadOnlyList<ColorType> TruckColors => _truckColors;

    public float GroupSlideDuration => _groupSlideDuration;
    public float EmptyCarrierExitDuration => _emptyCarrierExitDuration;
    public float CarrierTransferClickDelay => _carrierTransferClickDelay;
    public float ConveyorBlockScaleMultiplier => _conveyorBlockScaleMultiplier;
    public float ConveyorSpeedOverride => _conveyorSpeedOverride;
    public int ConveyorMaxBlockCount => _conveyorMaxBlockCount;
    public int ConveyorSlotElementCountOverride => _conveyorSlotElementCountOverride;
    public float ConveyorSlotElementOffsetOverride => _conveyorSlotElementOffsetOverride;
    public IReadOnlyList<Carrier> StartCarriers => _startCarriers;
    public IReadOnlyList<Carrier> EmptyCarriers => _emptyCarriers;
    public IEnumerable<Carrier> AllCarriers => _startCarriers.Concat(_emptyCarriers);
    public bool IsRegisteredCarrier(Carrier carrier) => _startCarriers.Contains(carrier) || _emptyCarriers.Contains(carrier);

    public bool UseEmptyCarrierRows => _useEmptyCarrierRows;
    public IReadOnlyList<EmptyCarrierRow> EmptyCarrierRows => _emptyCarrierRows;

    /// <summary>Called by each EmptyCarrierRowExit's own Awake so ShortcutManager can drive every row
    /// through SceneScope without any manual per-scene wiring.</summary>
    public void RegisterEmptyCarrierRowExit(EmptyCarrierRowExit rowExit)
    {
        if (rowExit == null) return;
        if (!_emptyCarrierRowExits.Contains(rowExit)) _emptyCarrierRowExits.Add(rowExit);
    }

    /// <summary>Lerps every registered row back to its authored position. Each row no-ops unless its
    /// own Use Start Position Lerp toggle is on. See ShortcutManager's O key.</summary>
    public void LerpEmptyCarrierRows()
    {
        foreach (var rowExit in _emptyCarrierRowExits)
            if (rowExit != null) rowExit.LerpToOriginalPosition();
    }

    /// <summary>
    /// First Empty carrier that's still a sink, isn't full, can begin a transfer, and accepts this
    /// block's colour (OnlyCompatibleColor / any IBlockTransferHandler included via
    /// CanTransferBlock) — the same acceptance rules HandleCarrierTrigger already applies
    /// per-carrier.
    ///
    /// When UseEmptyCarrierRows is on, EmptyCarriers is not consulted at all — each row offers one
    /// carrier and one only, the first of that row still holding space (see GetActiveRowCarrier).
    /// Filling it is what hands the row on to the next carrier; nothing has to be disabled, moved or
    /// removed for that to happen.
    ///
    /// Between rows, the carrier sitting furthest forward wins: a row whose open carrier is still at
    /// index 0 is served before one that has already filled its way back to index 1, so rows advance
    /// roughly in step instead of one racing ahead. Two candidates at the same index go to whichever
    /// row is listed first.
    /// </summary>
    public Carrier FindCompatibleEmptyCarrier(Block block)
    {
        if (_useEmptyCarrierRows)
        {
            Carrier best = null;
            var bestIndex = int.MaxValue;

            foreach (var row in _emptyCarrierRows)
            {
                var active = GetActiveRowCarrier(row, out var index);
                if (active == null) continue;

                // Not a better position than what we already hold, so nothing this carrier could
                // answer would change the outcome. Ties land here too, which is what keeps the
                // earlier row ahead of a later one at the same index.
                if (index >= bestIndex) continue;

                // Unlike the checks in GetActiveRowCarrier, these two are about this block right
                // now, so a no here means the row stays shut rather than passing the block back to
                // a carrier behind the active one.
                if (!active.CanBeginTransfer()) continue;
                if (!active.CanTransferBlock(block)) continue;

                best = active;
                bestIndex = index;

                // Nothing further back can beat the front of a row.
                if (bestIndex == 0) break;
            }

            return best;
        }

        foreach (var carrier in _emptyCarriers)
        {
            if (carrier == null) continue;
            if (!carrier.IsSink()) continue;
            if (carrier.IsFull()) continue;
            if (!carrier.CanBeginTransfer()) continue;
            if (!carrier.CanTransferBlock(block)) continue;
            return carrier;
        }

        return null;
    }

    /// <summary>
    /// A row's one open carrier: the first one, front to back, that still has room, reported with
    /// the position it sits at so callers can compare rows. Carriers that have filled up are stepped
    /// over — that is what hands a row on to its next carrier — as are entries that could never take
    /// a block anyway (null, inactive, or not a sink), so none of them can leave a row permanently
    /// stalled. Null once every carrier in the row is full.
    /// </summary>
    private static Carrier GetActiveRowCarrier(EmptyCarrierRow row, out int index)
    {
        for (var i = 0; i < row.Carriers.Count; i++)
        {
            var carrier = row.Carriers[i];
            if (carrier == null) continue;
            if (!carrier.gameObject.activeInHierarchy) continue;
            if (!carrier.IsSink()) continue;
            if (carrier.IsFull()) continue;

            index = i;
            return carrier;
        }

        index = int.MaxValue;
        return null;
    }

    public Camera Camera => _camera;
    public Conveyor Conveyor => _conveyor;
    public GlobalTrigger GlobalTrigger => _globalTrigger;
    public SplineComputer SplineComputer => _splineComputer;

    /// <summary>
    /// Flips Global Trigger's BlockTrigger.IsActive rather than the GameObject itself — SetActive
    /// would fire BlockTrigger.OnDisable and, since BindGlobalTrigger only ever runs once per level
    /// (off LevelBuildCompleteMessage), permanently discard its routing listener on the first toggle.
    /// </summary>
    public void ToggleGlobalTrigger()
    {
        if (_globalTrigger == null) return;
        if (!_globalTrigger.TryGetComponent<BlockTrigger>(out var blockTrigger)) return;
        blockTrigger.SetActive(!blockTrigger.IsActive);
    }

    /// <summary>
    /// Flips the Pointer GameObject active/inactive. Turning it on first moves it to the mouse
    /// position — a raycast against the Default layer, the same hit test InteractionModule uses for
    /// clicks — so it starts out pointing at whatever's under the cursor. See ShortcutManager's P key.
    /// </summary>
    public void TogglePointer()
    {
        if (_pointer == null) return;

        var isActive = !_pointer.activeSelf;
        if (isActive && _camera != null)
        {
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f, LayerMask.GetMask(LayerRefs.Default)))
                _pointer.transform.position = hit.point;
        }

        _pointer.SetActive(isActive);
    }

    private float _lastBlockStoredPlayTime = float.NegativeInfinity;

    /// <summary>
    /// Plays Block Stored Audio Source, but only if Block Stored Audio Min Interval has elapsed
    /// since the last play — so a burst of blocks landing on a sink within the same instant doesn't
    /// retrigger the clip on top of itself.
    /// </summary>
    public void PlayBlockStoredSound()
    {
        if (_blockStoredAudioSource == null) return;
        if (Time.time - _lastBlockStoredPlayTime < _blockStoredAudioMinInterval) return;

        _lastBlockStoredPlayTime = Time.time;
        _blockStoredAudioSource.Play();
    }

    private float _lastBlockConveyorJumpPlayTime = float.NegativeInfinity;

    /// <summary>
    /// Plays Block Conveyor Jump Audio Source, but only if Block Conveyor Jump Audio Min Interval has
    /// elapsed since the last play — so a burst of blocks landing on the belt within the same instant
    /// doesn't retrigger the clip on top of itself.
    /// </summary>
    public void PlayBlockConveyorJumpSound()
    {
        if (_blockConveyorJumpAudioSource == null) return;
        if (Time.time - _lastBlockConveyorJumpPlayTime < _blockConveyorJumpAudioMinInterval) return;

        _lastBlockConveyorJumpPlayTime = Time.time;
        _blockConveyorJumpAudioSource.Play();
    }

    private World _world;
    private readonly HashSet<Type> _explicitTypes = new();

#if UNITY_EDITOR
    /// <summary>Wired by LevelSandboxGenerator after it builds the conveyor.</summary>
    public void SetSceneReferences(Conveyor conveyor, SplineComputer splineComputer, SplineMesh splineMesh)
    {
        _conveyor = conveyor;
        _splineComputer = splineComputer;
        _splineMesh = splineMesh;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    protected override void Awake()
    {
        base.Awake();

        ApplyRandomBlockColors();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        _world?.Dispose();
    }

    /// <summary>
    /// Repaints every carrier in the level, at run time only.
    ///
    /// Each block carries the colour it was generated with in a serialized ColorType and re-applies it
    /// in Block.OnRent — which runs from GameBehaviourBase.Awake at execution order 0. This scope is
    /// at -100, so rewriting the field here lands before any block has initialised and the override
    /// costs nothing more than the material each block was going to apply anyway. The generated scene
    /// on disk is left alone; stopping play restores it.
    ///
    /// A carrier's blocks are split into runs of one colour, in the order they sit in BlockParent.
    /// The generator lays one authored cell down as a run of consecutive same-coloured blocks and the
    /// Apply Carrier Modes button fills group by group, so a run is exactly one colour group — and two
    /// adjacent groups that already shared a colour merge into one, which is the right unit anyway.
    /// Every run draws on its own, so a carrier can come out all one colour unless
    /// Prevent Single Color Carriers says otherwise.
    /// </summary>
    private void ApplyRandomBlockColors()
    {
        var palette = BuildRandomColorPalette();
        if (palette.Count == 0) return;

        using var p2 = ListPool<string>.Get(out var log);
        foreach (var carrier in AllCarriers)
        {
            var colorTypes = ApplyRandomCarrierColors(carrier, palette);
            if (colorTypes != null) log.Add($"{carrier.name} [{string.Join(", ", colorTypes)}]");
        }

        if (_truckColors.Count != _blockColors.Count)
            Debug.LogWarning($"<b>{nameof(SceneScope)}</b>: Truck Colors ({_truckColors.Count}) doesn't " +
                             $"match Block Colors ({_blockColors.Count}). Empty carriers whose draw " +
                             "lands outside Truck Colors fall back to their own compatible color.", this);

        foreach (var carrier in EmptyCarriers)
        {
            if (carrier == null || !carrier.IsSink()) continue;
            ApplyRandomCompatibleColor(carrier, palette);
        }

        if (log.Count == 0) return;

        Debug.Log($"<b>{nameof(SceneScope)}</b>: random block colors — {string.Join("; ", log)}.", this);
    }

    /// <summary>
    /// Block Colors, filtered to entries with a real material in the Colors asset and clamped to
    /// Color Range — the set every random draw (Start carrier groups, Empty carrier compatible
    /// colors, and the consecutive-cap rerolls on EmptyCarrierRowExit) picks from. Explicit color
    /// overrides don't use this — they index Block Colors directly, full list.
    /// </summary>
    public List<ColorType> BuildRandomColorPalette()
    {
        var palette = new List<ColorType>();

        var colors = _data?.OfType<Colors>().FirstOrDefault();
        if (colors == null)
        {
            Debug.LogWarning($"<b>{nameof(SceneScope)}</b>: no Colors asset in Data. Playing the level " +
                             "in the colors it was generated with.", this);
            return palette;
        }

        var start = _colorRangeStart < 0 ? 0 : _colorRangeStart;
        var end = _colorRangeEnd < 0 ? _blockColors.Count - 1 : _colorRangeEnd;
        if (start > end || start >= _blockColors.Count || end < 0)
        {
            Debug.LogWarning($"<b>{nameof(SceneScope)}</b>: Color Range [{_colorRangeStart}, " +
                             $"{_colorRangeEnd}] doesn't select anything in Block Colors " +
                             $"({_blockColors.Count} entries). Using the full list.", this);
            start = 0;
            end = _blockColors.Count - 1;
        }

        end = Mathf.Min(end, _blockColors.Count - 1);

        for (var i = start; i <= end; i++)
        {
            var colorType = _blockColors[i];
            if (colors.Get(colorType).Material == null)
            {
                Debug.LogWarning($"<b>{nameof(SceneScope)}</b>: Block Colors entry {colorType} has no " +
                                 "entry in the Colors asset, skipping it.", this);
                continue;
            }

            palette.Add(colorType);
        }

        if (palette.Count == 0)
            Debug.LogWarning($"<b>{nameof(SceneScope)}</b>: Block Colors has no usable entry in range. " +
                             "Playing the level in the colors it was generated with.", this);

        return palette;
    }

    /// <summary>
    /// Draws `count` colors from palette, sequentially, never letting a run of the same color exceed
    /// maxConsecutive (0 or less = unlimited). Walks forward through the palette from the streak
    /// color to find a different one when the cap would be exceeded — same approach as the
    /// single-color reroll in ApplyRandomCarrierColors below.
    /// </summary>
    public static List<ColorType> DrawWithConsecutiveCap(int count, List<ColorType> palette, int maxConsecutive)
    {
        var colorTypes = new List<ColorType>(count);
        if (palette.Count == 0) return colorTypes;

        ColorType? streakColor = null;
        var streakLength = 0;

        for (var i = 0; i < count; i++)
        {
            var colorType = palette.GetRandom();

            if (maxConsecutive > 0 && streakColor != null && streakLength >= maxConsecutive && palette.Count > 1)
            {
                var start = palette.IndexOf(streakColor.Value);
                for (var j = 1; j <= palette.Count; j++)
                {
                    var candidate = palette.GetWrapped(start + j);
                    if (candidate == streakColor.Value) continue;

                    colorType = candidate;
                    break;
                }
            }

            if (streakColor != null && colorType == streakColor.Value) streakLength++;
            else
            {
                streakColor = colorType;
                streakLength = 1;
            }

            colorTypes.Add(colorType);
        }

        return colorTypes;
    }

    /// <summary>
    /// Draws one palette color as this sink's new accepted color — same draw Start carriers use.
    /// OnRent resolves the matching TruckColors entry and paints the sink with it; see
    /// Carrier.ApplyTruckColor.
    /// </summary>
    private void ApplyRandomCompatibleColor(Carrier carrier, List<ColorType> palette)
    {
        carrier.SetCompatibleColor(palette.GetRandom());
    }

    /// <summary>
    /// Draws one palette colour per colour group in the carrier. Returns the colours it landed on, or
    /// null when the carrier holds no blocks to repaint.
    /// </summary>
    private List<ColorType> ApplyRandomCarrierColors(Carrier carrier, List<ColorType> palette)
    {
        if (carrier.BlockParent == null) return null;

        using var p = ListPool<List<Block>>.Get(out var groups);
        using var pBlocks = ListPool<Block>.Get(out var authoredBlocks);
        Carrier.GetAuthoredBlocksInOrder(carrier.BlockParent, authoredBlocks);

        var previousColorType = default(ColorType?);

        foreach (var block in authoredBlocks)
        {
            if (previousColorType == null || previousColorType.Value != block.ColorType)
                groups.Add(new List<Block>());

            groups[^1].Add(block);
            previousColorType = block.ColorType;
        }

        if (groups.Count == 0) return null;

        List<ColorType> colorTypes;
        if (carrier.OverrideStartColor)
        {
            // Deliberately monochrome: skip both the random draw and the consecutive-run guards
            // below, they don't mean anything once every group is forced to the same color.
            var colorType = carrier.OverrideStartColorIndex >= 0 && carrier.OverrideStartColorIndex < _blockColors.Count
                ? _blockColors[carrier.OverrideStartColorIndex]
                : (ColorType?)null;

            if (colorType == null)
            {
                Debug.LogWarning($"<b>{nameof(SceneScope)}</b>: '{carrier.name}' Override Start Color " +
                                 $"Index {carrier.OverrideStartColorIndex} is out of range for Block " +
                                 $"Colors ({_blockColors.Count} entries). Drawing randomly instead.", carrier);
                colorType = palette.GetRandom();
            }

            colorTypes = new List<ColorType>(groups.Count);
            for (var i = 0; i < groups.Count; i++) colorTypes.Add(colorType.Value);
        }
        else if (carrier.MaxConsecutiveSameColorGroups > 0)
        {
            // The cap already keeps every group from landing on the same color whenever it's below
            // groups.Count, so Prevent Single Color Carriers' own fixup below would be redundant.
            colorTypes = DrawWithConsecutiveCap(groups.Count, palette, carrier.MaxConsecutiveSameColorGroups);
        }
        else
        {
            colorTypes = new List<ColorType>(groups.Count);
            foreach (var _ in groups)
                colorTypes.Add(palette.GetRandom());

            // Every group landing on the same colour makes the whole carrier one solid block of colour,
            // which is rarely what you want to test against. One redraw is enough to break it up.
            if (_preventSingleColorCarriers && colorTypes.Count > 1 && palette.Count > 1)
            {
                var isSingleColor = true;
                foreach (var colorType in colorTypes)
                    if (colorType != colorTypes[0])
                    {
                        isSingleColor = false;
                        break;
                    }

                // Walking on from the colour that was drawn rather than rerolling: a palette is allowed
                // to list the same colour twice, and rerolling could then never find a different one.
                if (isSingleColor)
                {
                    var start = palette.IndexOf(colorTypes[0]);
                    for (var i = 1; i <= palette.Count; i++)
                    {
                        var candidate = palette.GetWrapped(start + i);
                        if (candidate == colorTypes[0]) continue;

                        colorTypes[^1] = candidate;
                        break;
                    }
                }
            }
        }

        // Color Pattern paints this carrier's groups starting from the last one (groups.Count - 1 —
        // the front group, the first one this Start carrier actually dispenses) and works backward, a
        // suffix overlay on top of whatever Override Start Color / Max Consecutive Same Color Groups /
        // random draw above already filled every group with. Groups the pattern's blocks don't reach
        // (or every group, if the asset is missing or empty) are left exactly as that fallback drew
        // them, rather than looping the pattern back to its start.
        if (carrier.UseColorPattern)
        {
            var colorPattern = carrier.ColorPattern;
            if (colorPattern != null && colorPattern.Blocks.Count > 0)
            {
                var patternColors = colorPattern.GetColors(groups.Count);
                for (var i = 0; i < patternColors.Count; i++)
                    colorTypes[groups.Count - 1 - i] = patternColors[i];
            }
            else
            {
                Debug.LogWarning($"<b>{nameof(SceneScope)}</b>: '{carrier.name}' Use Color Pattern is " +
                                 "on but Color Pattern is missing or empty. Falling back to Override " +
                                 "Start Color / Max Consecutive Same Color Groups.", carrier);
            }
        }

        for (var i = 0; i < groups.Count; i++)
        foreach (var block in groups[i])
            block.OverrideColorType(colorTypes[i]);

        return colorTypes;
    }

    protected override void Configure(IContainerBuilder builder)
    {
        base.Configure(builder);

        _explicitTypes.Clear();

        builder.RegisterMessagePipe();
        builder.RegisterBuildCallback(container => GlobalMessagePipe.SetProvider(container.AsServiceProvider()));

        builder.Register<MaterialPropertyBlock>(Lifetime.Singleton);
        builder.Register<SceneRegistry>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();

        HandleData(builder);
        HandleModules(builder);
        HandleSceneSingletons(builder);
        HandleSceneComponents(builder);
        HandleWorld(builder);
        HandleSystems(builder);

        builder.RegisterInstance(_theme).AsSelf();
    }

    private void HandleData(IContainerBuilder builder)
    {
        if (_data == null) return;

        foreach (var data in _data)
        {
            if (data == null) continue;
            if (!_explicitTypes.Add(data.GetType())) continue;
            builder.RegisterComponent(data).AsSelf();
        }
    }

    /// <summary>
    /// An explicit list rather than reflection over ModuleBase: reflection would drag in
    /// EconomyModule (which needs SheetContainer) and TutorialModule (which needs the UI).
    /// </summary>
    private void HandleModules(IContainerBuilder builder)
    {
        Register<SceneModule>();
        Register<PrefabModule>();
        Register<RemoteConfigModule>();
        Register<AudioModule>();
        Register<HapticModule>();
        Register<InteractionModule>();
        Register<OutlineModule>();
        return;

        void Register<T>()
        {
            _explicitTypes.Add(typeof(T));
            builder.Register(typeof(T), Lifetime.Singleton).AsSelf().AsImplementedInterfaces();
        }
    }

    /// <summary>
    /// Registered by hand because a generated level contains more than one of some of these types
    /// (the conveyor collider prefab carries a second SplineMesh), and HandleSceneComponents
    /// silently drops any type it sees twice.
    /// </summary>
    private void HandleSceneSingletons(IContainerBuilder builder)
    {
        Register(_camera);
        Register(_conveyor);
        Register(_splineComputer);
        Register(_splineMesh);
        return;

        void Register<T>(T component) where T : Component
        {
            if (component == null) return;
            _explicitTypes.Add(typeof(T));
            builder.RegisterComponent(component).AsSelf();
        }
    }

    private void HandleSceneComponents(IContainerBuilder builder)
    {
        var components = FindObjectsOfType<Component>().ToList();
        components.Remove(this);
        components.RemoveAll(x => x.gameObject.scene != gameObject.scene);
        var uniqueComponents = components.GroupBy(x => x.GetType())
            .Where(g => g.Count() == 1)
            .Select(g => g.First());
        foreach (var component in uniqueComponents)
        {
            if (_explicitTypes.Contains(component.GetType())) continue;
            builder.RegisterInstance(component).AsSelf();
        }
    }

    private void HandleWorld(IContainerBuilder builder)
    {
        _world = World.Create();
        _world.UpdateByUnity = true;
        builder.RegisterComponent(_world).AsSelf();
    }

    private void HandleSystems(IContainerBuilder builder)
    {
        var systemsGroup = _world.CreateSystemsGroup();

        var baseType = typeof(SystemBase);
        using var p1 = baseType.GetDerivedClassTypes(out var derivedTypes);
        using var p2 = ListPool<SystemBase>.Get(out var systems);

        var allowed = new HashSet<string>(_systems ?? Array.Empty<string>());
        var matched = new HashSet<string>();

        foreach (var derivedType in derivedTypes)
        {
            if (!allowed.Contains(derivedType.Name)) continue;
            matched.Add(derivedType.Name);

            var instance = Activator.CreateInstance(derivedType) as SystemBase;
            systems.Add(instance);
            systemsGroup.AddSystem(instance);
        }

        foreach (var name in allowed)
        {
            if (matched.Contains(name)) continue;
            Debug.LogWarning($"<b>{nameof(SceneScope)}</b>: no SystemBase named '{name}' was found.", this);
        }

        foreach (var system in systems)
        {
            builder.RegisterComponent(system).AsSelf().AsImplementedInterfaces();
        }

        _world.AddSystemsGroup(0, systemsGroup);
    }
}

public struct Level
{
    public int Index;
    public PlayerPrefsInt LoseCount;

    private readonly LevelSheet.Level _level;
    private readonly List<ColorType> _colorTypes;
    private readonly Dictionary<ColorType, int> _idxByColorType;

    public Level(int idx, LevelSheet.Level level)
    {
        Index = idx;
        _level = level;
        LoseCount = new PlayerPrefsInt($"level_{idx}_lose_count");
        _colorTypes = level.Carriers.Ref.ColorTypes;
        _idxByColorType = new Dictionary<ColorType, int>();
        for (var i = 0; i < _colorTypes.Count; i++) _idxByColorType[_colorTypes[i]] = i;
    }

    public ColorType GetColor(string data)
    {
        var colorType = new ColorType(data);
        return GetColor(colorType);
    }

    public ColorType GetColor(ColorType color)
    {
        var idx = _idxByColorType.GetValueOrDefault(color, -1);
        return idx == -1 ? color : _colorTypes.GetWrapped(idx + LoseCount + _level.ColorMix);
    }
}

public struct LevelTransitionData
{
    public bool Enter;
    public bool Exit;
}

/// <summary>One row of SceneScope's Empty Carrier Rows, front-to-back.</summary>
[Serializable]
public sealed class EmptyCarrierRow
{
    public List<Carrier> Carriers = new();
}

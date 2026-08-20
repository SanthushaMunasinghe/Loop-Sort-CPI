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

    [Tooltip("Reroll one of a carrier's color groups when the draw gives it every block in the same " +
             "color. Needs two usable Block Colors and two groups in the carrier to do anything.")]
    [SerializeField] private bool _preventSingleColorCarriers = true;

    [Header("Carriers")]
    [Tooltip("Start mode: how long the group slide-forward animation takes after a transfer batch " +
             "fully drains a colour group.")]
    [SerializeField] private float _groupSlideDuration = .25f;

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

    [Header("Conveyor")]
    [Tooltip("Scales a block on top of its normal size while it's jumping to and sitting on the " +
             "conveyor belt. Reset to a block's normal scale the moment it lands back in a carrier.")]
    [SerializeField] private float _conveyorBlockScaleMultiplier = 1f;

    [Header("Scene")]
    [Tooltip("Your camera. InteractionModule raycasts through it — without one there is no input.")]
    [SerializeField] private Camera _camera;

    [SerializeField] private Conveyor _conveyor;
    [SerializeField] private SplineComputer _splineComputer;
    [SerializeField] private SplineMesh _splineMesh;

    [Tooltip("The single global pickup trigger blocks pass through to be routed to a compatible, " +
             "unfinished Empty carrier anywhere in the level. Hand-placed in the scene, not generated.")]
    [SerializeField] private GlobalTrigger _globalTrigger;

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

    public float GroupSlideDuration => _groupSlideDuration;
    public float ConveyorBlockScaleMultiplier => _conveyorBlockScaleMultiplier;
    public IReadOnlyList<Carrier> StartCarriers => _startCarriers;
    public IReadOnlyList<Carrier> EmptyCarriers => _emptyCarriers;
    public IEnumerable<Carrier> AllCarriers => _startCarriers.Concat(_emptyCarriers);
    public bool IsRegisteredCarrier(Carrier carrier) => _startCarriers.Contains(carrier) || _emptyCarriers.Contains(carrier);

    public bool UseEmptyCarrierRows => _useEmptyCarrierRows;
    public IReadOnlyList<EmptyCarrierRow> EmptyCarrierRows => _emptyCarrierRows;

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
        var colors = _data?.OfType<Colors>().FirstOrDefault();
        if (colors == null)
        {
            Debug.LogWarning($"<b>{nameof(SceneScope)}</b>: no Colors asset in Data. Playing the level " +
                             "in the colors it was generated with.", this);
            return;
        }

        using var p1 = ListPool<ColorType>.Get(out var palette);
        foreach (var colorType in _blockColors)
        {
            if (colors.Get(colorType).Material == null)
            {
                Debug.LogWarning($"<b>{nameof(SceneScope)}</b>: Block Colors entry {colorType} has no " +
                                 "entry in the Colors asset, skipping it.", this);
                continue;
            }

            palette.Add(colorType);
        }

        if (palette.Count == 0)
        {
            Debug.LogWarning($"<b>{nameof(SceneScope)}</b>: Block Colors has no usable entry. Playing " +
                             "the level in the colors it was generated with.", this);
            return;
        }

        using var p2 = ListPool<string>.Get(out var log);
        foreach (var carrier in AllCarriers)
        {
            var colorTypes = ApplyRandomCarrierColors(carrier, palette);
            if (colorTypes != null) log.Add($"{carrier.name} [{string.Join(", ", colorTypes)}]");
        }

        if (log.Count == 0) return;

        Debug.Log($"<b>{nameof(SceneScope)}</b>: random block colors — {string.Join("; ", log)}.", this);
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

        var colorTypes = new List<ColorType>(groups.Count);
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

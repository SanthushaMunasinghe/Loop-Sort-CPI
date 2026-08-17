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
    [Tooltip("Palette the level is repainted with when Use Random Colors is on. Needs a matching " +
             "entry in the Colors asset above; entries that have none are skipped.")]
    [SerializeField] private List<ColorType> _blockColors = new();

    [Tooltip("Ignore the colors the level was generated with. Every color group in the scene draws " +
             "one of Block Colors at random. Draws are independent, so two groups can land on the " +
             "same color and merge — with only Black and White in the list the whole level is " +
             "played in black and white.")]
    [SerializeField] private bool _useRandomColors;

    [Header("Scene")]
    [Tooltip("Your camera. InteractionModule raycasts through it — without one there is no input.")]
    [SerializeField] private Camera _camera;

    [SerializeField] private Conveyor _conveyor;
    [SerializeField] private SplineComputer _splineComputer;
    [SerializeField] private SplineMesh _splineMesh;

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

    public Camera Camera => _camera;
    public Conveyor Conveyor => _conveyor;
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
    /// Repaints the level the scene was generated with, at run time only.
    ///
    /// Every block in the scene carries the colour it was generated with in a serialized ColorType,
    /// and re-applies it in Block.OnRent — which runs from GameBehaviourBase.Awake at execution
    /// order 0. This scope is at -100, so rewriting the field here lands before any block has
    /// initialised and the override costs nothing more than the material each block was going to
    /// apply anyway. The generated scene on disk is left alone; stopping play restores it.
    ///
    /// Blocks are grouped by the colour they currently have, so a whole group is always repainted
    /// as one and the four-blocks-per-colour invariant survives. Draws are independent per group.
    /// </summary>
    private void ApplyRandomBlockColors()
    {
        if (!_useRandomColors) return;

        var colors = _data?.OfType<Colors>().FirstOrDefault();
        if (colors == null)
        {
            Debug.LogWarning($"<b>{nameof(SceneScope)}</b>: Use Random Colors is on but there is no " +
                             "Colors asset in Data. Playing the level in its generated colors.", this);
            return;
        }

        using var p = ListPool<ColorType>.Get(out var palette);
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
            Debug.LogWarning($"<b>{nameof(SceneScope)}</b>: Use Random Colors is on but Block Colors " +
                             "has no usable entry. Playing the level in its generated colors.", this);
            return;
        }

        var overrides = new Dictionary<ColorType, ColorType>();
        foreach (var block in FindObjectsOfType<Block>(includeInactive: true))
        {
            if (block.gameObject.scene != gameObject.scene) continue;

            var colorType = block.ColorType;
            if (colorType.Base == BaseColor.None) continue;

            if (!overrides.TryGetValue(colorType, out var overridden))
            {
                overridden = palette.GetRandom();
                overrides[colorType] = overridden;
            }

            block.OverrideColorType(overridden);
        }

        if (overrides.Count == 0) return;

        var mapping = string.Join(", ", overrides.Select(pair => $"{pair.Key}→{pair.Value}"));
        Debug.Log($"<b>{nameof(SceneScope)}</b>: random block colors — {mapping}.", this);
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

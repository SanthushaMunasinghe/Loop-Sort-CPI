using System;
using System.Collections.Generic;
using System.Linq;
using Scellecs.Morpeh;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using VContainer.Unity;

[DefaultExecutionOrder(-100)]
public sealed class SceneScope : LifetimeScope
{
    [Inject] private BootstrapScope _bootstrapScope;

    private World _world;
    private LevelSheet.Level _currentLevel;

    private readonly HashSet<int> _transitionLevels = new() { 0, 1 };

    protected override void Awake()
    {
        LifetimeScopeH.InjectIfNotScoped<MonitorScope>(this);
        base.Awake();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        _world?.Dispose();
    }

    protected override void Configure(IContainerBuilder builder)
    {
        base.Configure(builder);

        builder.Register<MaterialPropertyBlock>(Lifetime.Singleton);
        builder.Register<SceneRegistry>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();

        HandleSceneComponents(builder);
        HandleWorld(builder);
        HandleSystems(builder);
        HandleSheetContainer(builder);

        builder.RegisterInstance(new Level(Prefs.Level.Value, _currentLevel));
        builder.RegisterInstance(new LevelTransitionData
        {
            Enter = _transitionLevels.Contains(Prefs.Level.Value - 1),
            Exit = _transitionLevels.Contains(Prefs.Level.Value)
        });
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
        if (gameObject.scene.buildIndex != 1) return;

        var systemsGroup = _world.CreateSystemsGroup();

        var baseType = typeof(SystemBase);
        using var p1 = baseType.GetDerivedClassTypes(out var derivedTypes);
        using var p2 = ListPool<SystemBase>.Get(out var systems);
        foreach (var derivedType in derivedTypes)
        {
            var instance = Activator.CreateInstance(derivedType) as SystemBase;
            systems.Add(instance);
            systemsGroup.AddSystem(instance);
        }

        foreach (var system in systems)
        {
            builder.RegisterComponent(system).AsSelf().AsImplementedInterfaces();
        }

        _world.AddSystemsGroup(0, systemsGroup);
    }

    private void HandleSheetContainer(IContainerBuilder builder)
    {
        if (!_bootstrapScope.Container.TryResolve<SheetContainer>(out var sheetContainer)) return;

        var maxLevelOffset = sheetContainer.Constants.GetInt(GameConstant.MaxLevelOffset);
        var level = sheetContainer.Levels.GetCurrent(maxLevelOffset);
        builder.RegisterInstance(level).AsSelf();

        var themeFrequency = sheetContainer.Constants.GetInt(GameConstant.ThemeFrequency);
        var nextTheme = level.Theme != ThemeType.Default
            ? level.Theme
            : sheetContainer.Levels.GetNextTheme(themeFrequency);
        builder.RegisterInstance(nextTheme).AsSelf();

        _currentLevel = level;
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
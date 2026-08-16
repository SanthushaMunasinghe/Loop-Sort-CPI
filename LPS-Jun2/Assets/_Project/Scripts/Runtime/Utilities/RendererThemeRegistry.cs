using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class RendererThemeRegistry : MonoBehaviour
{
    [SerializeField] private GenericDictionary<ThemeType, Material[]> Registries = new();

    private Renderer _renderer;

    private void OnEnable()
    {
        SetTheme();
    }

    private void Start()
    {
        SetTheme();
    }

    private void SetTheme()
    {
        // The theme now lives on SceneScope directly; there is no Bootstrap container to go through.
        var scope = LifetimeScopeH.FindScope<SceneScope>();
        if (scope == null || scope.Container == null) return;
        ApplyTheme(scope.Container.Resolve<ThemeType>());
    }

    public void ApplyTheme(ThemeType themeType)
    {
        _renderer ??= GetComponent<Renderer>();
        var theme = Registries.ContainsKey(themeType) ? themeType : ThemeType.Default;
        if (!Registries.TryGetValue(theme, out var materials)) return;
        _renderer.sharedMaterials = materials;
    }
}
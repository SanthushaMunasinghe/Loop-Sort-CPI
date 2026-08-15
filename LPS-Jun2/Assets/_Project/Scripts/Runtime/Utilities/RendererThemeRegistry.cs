using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class RendererThemeRegistry : MonoBehaviour
{
    [SerializeField] private GenericDictionary<ThemeType, Material[]> Registries = new();

    private Renderer _renderer;

    private static SceneModule _sceneModule;

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
        if (_sceneModule == null)
        {
            var resolver = LifetimeScopeH.FindScope<BootstrapScope>().Container;
            _sceneModule = resolver.Resolve<SceneModule>();
        }

        if (_sceneModule.Scope != null)
        {
            var resolver = _sceneModule.Scope.Container;
            var themeType = resolver.Resolve<ThemeType>();
            ApplyTheme(themeType);
        }
    }

    public void ApplyTheme(ThemeType themeType)
    {
        _renderer ??= GetComponent<Renderer>();
        var theme = Registries.ContainsKey(themeType) ? themeType : ThemeType.Default;
        if (!Registries.TryGetValue(theme, out var materials)) return;
        _renderer.sharedMaterials = materials;
    }
}
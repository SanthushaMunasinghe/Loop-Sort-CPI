using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

public sealed class ImageThemeRegistry : MonoBehaviour
{
    [SerializeField] private ThemeType DefaultTheme = ThemeType.Default;
    [SerializeField] private bool ShouldDisplayDefault = true;

    [SerializeField] private GenericDictionary<ThemeType, Data> Registries = new();

    private Image _image;

    private static SceneModule _sceneModule;

    [Serializable]
    public struct Data
    {
        public Sprite Sprite;
        public Color Color;
    }

    private void OnEnable()
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
        _image ??= GetComponent<Image>();
        var containsTheme = Registries.ContainsKey(themeType);
        _image.enabled = containsTheme || ShouldDisplayDefault;
        var theme = containsTheme ? themeType : DefaultTheme;
        if (!Registries.TryGetValue(theme, out var data)) return;
        _image.overrideSprite = data.Sprite;
        if (data.Color != default) _image.color = data.Color;
    }
}
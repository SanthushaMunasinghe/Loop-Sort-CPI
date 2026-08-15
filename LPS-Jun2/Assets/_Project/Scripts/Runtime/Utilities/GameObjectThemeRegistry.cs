using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class GameObjectThemeRegistry : MonoBehaviour
{
    [SerializeField] private ThemeType DefaultTheme = ThemeType.Default;
    [SerializeField] private bool ShouldDisplayDefault = true;

    [SerializeField] private GenericDictionary<ThemeType, GameObject> Registries = new();

    private static SceneModule _sceneModule;

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
        foreach (var value in Registries.Values) value.SetActive(false);
        var containsTheme = Registries.ContainsKey(themeType);
        var active = containsTheme || ShouldDisplayDefault;
        var theme = containsTheme ? themeType : DefaultTheme;
        if (!Registries.TryGetValue(theme, out var targetGameObject)) return;
        targetGameObject.SetActive(active);
    }
}
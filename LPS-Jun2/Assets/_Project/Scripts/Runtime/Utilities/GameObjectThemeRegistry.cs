using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class GameObjectThemeRegistry : MonoBehaviour
{
    [SerializeField] private ThemeType DefaultTheme = ThemeType.Default;
    [SerializeField] private bool ShouldDisplayDefault = true;

    [SerializeField] private GenericDictionary<ThemeType, GameObject> Registries = new();

    private void OnEnable()
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
        foreach (var value in Registries.Values) value.SetActive(false);
        var containsTheme = Registries.ContainsKey(themeType);
        var active = containsTheme || ShouldDisplayDefault;
        var theme = containsTheme ? themeType : DefaultTheme;
        if (!Registries.TryGetValue(theme, out var targetGameObject)) return;
        targetGameObject.SetActive(active);
    }
}
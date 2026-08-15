using UnityEngine;
using VContainer;

public sealed class MapLoader : MonoBehaviour
{
    [Inject] private BootstrapScope _bootstrapScope;

    private void Awake()
    {
        LifetimeScopeH.InjectIfNotScoped<BootstrapScope>(this);
        CreateMap();
    }

    private void CreateMap()
    {
        if (!_bootstrapScope.Container.TryResolve<SheetContainer>(out var sheetContainer)) return;
        var maxLevelOffset = sheetContainer.Constants.GetInt(GameConstant.MaxLevelOffset);
        var level = sheetContainer.Levels.GetCurrent(maxLevelOffset);
        var path = $"Maps/{level.Map.ToString()}";
        var mapPrefab = Resources.Load<GameObject>(path);
        Instantiate(mapPrefab);
    }
}
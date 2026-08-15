using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;

public partial class GameBehaviourBase
{
    private readonly List<RendererPropertyRegistry> _rendererPropertyRegistries = new();
    private readonly List<RendererThemeRegistry> _rendererThemeRegistries = new();

    public void InjectColorType(ColorType colorType, Color colorOffset = default)
    {
        if (_rendererPropertyRegistries.Count == 0)
            GetComponentsInChildren(_rendererPropertyRegistries);

        foreach (var propertyRegistry in _rendererPropertyRegistries)
            InjectColorType(colorType, propertyRegistry, colorOffset);
    }

    public void InjectColorType(ColorType colorType, GameObject targetGameObject, Color colorOffset = default)
    {
        var propertyRegistry = targetGameObject.GetComponent<RendererPropertyRegistry>();
        InjectColorType(colorType, propertyRegistry, colorOffset);
    }

    public void InjectColorType(ColorType colorType, RendererPropertyRegistry propertyRegistry, Color colorOffset = default)
    {
        var data = Colors.Get(colorType);
        if (data.Material == null)
        {
            Debug.LogError($"Color not found: {colorType.ToString()}");
            return;
        }

        using var p = ListPool<Material>.Get(out var materials);
        var targets = propertyRegistry.GetTargets(propertyKey: nameof(Color));
        foreach (var target in targets)
        {
            foreach (var targetRenderer in target.TargetRenderers)
            {
                targetRenderer.GetSharedMaterials(materials);
                materials[target.MaterialIndex] = data.Material;
                targetRenderer.SetSharedMaterials(materials);

                if (colorOffset == default)
                {
                    targetRenderer.SetPropertyBlock(null, target.MaterialIndex);
                    continue;
                }

                var color = data.Material.GetColor(target.PropertyId);
                color += colorOffset;
                PropertyBlock.Clear();
                PropertyBlock.SetColor(target.PropertyId, color);
                targetRenderer.SetPropertyBlock(PropertyBlock, target.MaterialIndex);
            }
        }
    }

    public void InjectTheme(ThemeType themeType)
    {
        if (_rendererThemeRegistries.Count == 0)
            GetComponentsInChildren(_rendererThemeRegistries);

        foreach (var registry in _rendererThemeRegistries)
            registry.ApplyTheme(themeType);
    }

    public void InjectMaterial(Material material)
    {
        if (material == null)
            return;

        if (_rendererPropertyRegistries.Count == 0)
            GetComponentsInChildren(_rendererPropertyRegistries);

        using var p = ListPool<Material>.Get(out var materials);
        foreach (var propertyRegistry in _rendererPropertyRegistries)
        {
            var targets = propertyRegistry.GetTargets(propertyKey: nameof(Color));
            foreach (var target in targets)
            {
                foreach (var targetRenderer in target.TargetRenderers)
                {
                    targetRenderer.GetSharedMaterials(materials);
                    materials[target.MaterialIndex] = material;
                    targetRenderer.SetSharedMaterials(materials);
                    targetRenderer.SetPropertyBlock(null, target.MaterialIndex);
                }
            }
        }
    }

    public void GetCutoutRenderers(List<Renderer> cutoutRenderers)
    {
        using var p = ListPool<Renderer>.Get(out var renderers);
        GetComponentsInChildren(includeInactive: true, renderers);

        using var enumerator = renderers
            .Where(r => r.sharedMaterial.shader.name.Contains("StencilMask"))
            .Select(r => r.gameObject)
            .GetEnumerator();
        cutoutRenderers.Clear();
        while (enumerator.MoveNext())
        {
            var instance = enumerator.Current;
            if (instance == null) continue;
            var cutoutRenderer = instance.GetComponent<Renderer>();
            if (cutoutRenderer == null) continue;
            cutoutRenderers.Add(cutoutRenderer);
        }
    }
}
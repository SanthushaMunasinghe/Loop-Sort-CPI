using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages mappings between high-level property keys and shader properties on a Renderer.
/// </summary>
public sealed class RendererPropertyRegistry : MonoBehaviour
{
    [SerializeField] private List<Renderer> Renderers;
    [SerializeField] private List<PropertyDefinition> PropertyDefinitions = new();

    private readonly List<ShaderPropertyTarget> _shaderPropertyTargets = new();

    [Serializable]
    public struct PropertyDefinition
    {
        /// <summary>
        /// Logical key used by game code to identify this property.
        /// </summary>
        public string PropertyKey;

        /// <summary>
        /// Exact shader property name (e.g. "_MainTex" or "_Color").
        /// </summary>
        public string ShaderPropertyName;

        /// <summary>
        /// Index of the material on the Renderer to apply this property to (-1 for all).
        /// </summary>
        public int MaterialIndex;
    }

    public struct ShaderPropertyTarget
    {
        /// <summary>
        /// Numeric ID of the shader property (obtained via Shader.PropertyToID).
        /// </summary>
        public int PropertyId;

        /// <summary>
        /// Material index on the Renderer to which this property will be applied.
        /// </summary>
        public int MaterialIndex;

        /// <summary>
        /// Renderer instance on which the property will be set.
        /// </summary>
        public List<Renderer> TargetRenderers;
    }

    /// <summary>
    /// Retrieves all shader property targets matching a given logical property key.
    /// </summary>
    /// <param name="propertyKey">The game-level key identifying the property.</param>
    /// <returns>A list of targets indicating which shader properties to set and where.</returns>
    public List<ShaderPropertyTarget> GetTargets(string propertyKey)
    {
        _shaderPropertyTargets.Clear();

        foreach (var def in PropertyDefinitions)
        {
            if (!string.Equals(def.PropertyKey, propertyKey, StringComparison.Ordinal))
                continue;

            var propId = Shader.PropertyToID(def.ShaderPropertyName);
            _shaderPropertyTargets.Add(new ShaderPropertyTarget
            {
                PropertyId = propId,
                MaterialIndex = def.MaterialIndex,
                TargetRenderers = Renderers
            });
        }

        return _shaderPropertyTargets;
    }

    public void AddRenderer(Renderer r)
    {
        Renderers.Add(r);
    }

    public void RemoveRenderer(Renderer r)
    {
        Renderers.Remove(r);
    }

    public void RemoveAllRenderers()
    {
        Renderers.Clear();
    }
}
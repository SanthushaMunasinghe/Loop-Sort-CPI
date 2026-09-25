using System;
using UnityEngine;

/// <summary>
/// One entry of SceneScope's Grocery Models — what a Grocery Item of this type looks like. The type
/// of a Grocery Item is its index into that list.
/// </summary>
[Serializable]
public sealed class GroceryModel
{
    [Tooltip("Label only, so the list reads well in the inspector.")]
    public string Name;

    [Tooltip("Model spawned under the Grocery Item's Model Root. Its colliders are switched off.")]
    public GameObject Model;

    [Tooltip("Local scale of the Grocery Item's Model Root while it holds this model. The item's own " +
             "root scale is still driven by the carrier/conveyor on top of this.")]
    public Vector3 SpawnScale = Vector3.one;
}

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

    [Tooltip("Local position of the Grocery Item's Model Root while it holds this model — nudges the " +
             "model within its grid cell.")]
    public Vector3 SpawnPosition = Vector3.zero;

    [Tooltip("Local rotation (euler angles) of the Grocery Item's Model Root while it holds this model.")]
    public Vector3 SpawnRotation = Vector3.zero;

    [Tooltip("Local scale of the Grocery Item's Model Root while it holds this model. In a cart the " +
             "item's own root stays at scale 1, so this is the size it shows at there; the conveyor " +
             "still scales the root on the belt.")]
    public Vector3 SpawnScale = Vector3.one;
}

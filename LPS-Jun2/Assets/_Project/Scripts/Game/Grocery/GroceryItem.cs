using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// The unified grocery prefab: a Block underneath, so the conveyor, triggers and carrier sorting all
/// work on it unchanged, wearing whichever SceneScope Grocery Models entry its type points at.
///
/// Matching runs on Block.ColorType everywhere in that pipeline, so the grocery type is folded into a
/// ColorType no real colour uses (see <see cref="ToMatchKey"/>). There is no RendererPropertyRegistry on
/// the prefab, so Block's own colour painting has nothing to touch.
/// </summary>
[RequireComponent(typeof(Block))]
public sealed class GroceryItem : MonoBehaviour
{
    private const int MatchKeyOffset = 1000;

    [Tooltip("Parent the type's model is spawned under. Takes the entry's Spawn Scale.")]
    [SerializeField] private Transform _modelRoot;

    public int Type { get; private set; } = -1;

    private Block _block;
    private GameObject _model;

    private void Awake()
    {
        _block = GetComponent<Block>();

        // The mesh systems keep swapping the Block's cube mesh and flipping its renderer on, so hide it
        // in a way none of them touch.
        if (TryGetComponent<MeshRenderer>(out var meshRenderer))
            meshRenderer.forceRenderingOff = true;
    }

    public void SetType(int type, GroceryModel entry)
    {
        Type = type;
        _block.OverrideColorType(ToMatchKey(type));

        if (_model != null) Destroy(_model);
        _model = null;

        var modelRoot = _modelRoot != null ? _modelRoot : transform;
        modelRoot.localScale = entry.SpawnScale;

        if (entry.Model == null) return;

        _model = Instantiate(entry.Model, modelRoot, false);
        _model.transform.localPosition = Vector3.zero;
        _model.transform.localRotation = Quaternion.identity;

        // A model's own colliders would compound into the Block's Rigidbody and catch clicks.
        using var p = ListPool<Collider>.Get(out var colliders);
        _model.GetComponentsInChildren(true, colliders);
        foreach (var modelCollider in colliders)
            modelCollider.enabled = false;
    }

    public static ColorType ToMatchKey(int type)
    {
        return new ColorType((BaseColor)(MatchKeyOffset + type));
    }
}

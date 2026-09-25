using UnityEngine;

/// <summary>
/// Pairs a trigger volume with the shopping cart it feeds. Only Grocery Items are picked up — see
/// BlockTriggerSystem.BindGroceryTriggers. Hand-placed in the scene, same as CarrierBlockTrigger.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(BlockTrigger))]
public sealed class GroceryTrigger : MonoBehaviour
{
    [SerializeField] private Carrier _cart;

    public Carrier Cart => _cart;

#if UNITY_EDITOR
    public void SetCart(Carrier cart) => _cart = cart;
#endif
}

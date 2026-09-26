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

    /// <summary>Editor wiring, and ShoppingCartExit handing the trigger to the cart it respawns in a
    /// completed one's seat. BlockTriggerSystem reads Cart on every pickup, so this takes effect at once.</summary>
    public void SetCart(Carrier cart) => _cart = cart;
}

using UnityEngine;

/// <summary>
/// Marks a Carrier as a shopping cart: filled with Grocery Items at run time by
/// SceneScope.FillShoppingCarts, fed by GroceryTriggers, and left out of the cube mesh / group block
/// handling in BlockCarrierMeshSystem.
/// </summary>
[RequireComponent(typeof(Carrier))]
public sealed class ShoppingCart : MonoBehaviour
{
}

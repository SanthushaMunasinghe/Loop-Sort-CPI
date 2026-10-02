using UnityEngine;

/// <summary>
/// Marks a Carrier as a shopping cart: filled with Grocery Items at run time by
/// SceneScope.FillShoppingCarts, fed by GroceryTriggers, and left out of the cube mesh / group block
/// handling in BlockCarrierMeshSystem.
/// </summary>
[RequireComponent(typeof(Carrier))]
public sealed class ShoppingCart : MonoBehaviour
{
    /// <summary>
    /// The one grocery type (a SceneScope Grocery Models index) this cart takes in and completes on,
    /// or -1 when it sorts like any Default carrier. Dealt out by SceneScope when its Use Shopping Cart
    /// Required Types is on; Carrier.CanTransferBlock and CanComplete are what enforce it.
    /// </summary>
    public int RequiredType { get; private set; } = -1;

    public bool HasRequiredType => RequiredType >= 0;

    public void SetRequiredType(int type) => RequiredType = type;
}

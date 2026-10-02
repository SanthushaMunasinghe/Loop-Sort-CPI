using UnityEngine;

/// <summary>
/// The thought cloud over a shopper's head showing the grocery type their cart is after. Off until
/// SceneScope deals the cart a required type (Use Shopping Cart Required Types) and calls Show with
/// that type's Icon; ShoppingCartExit hides it again as the completed cart sets off.
///
/// Sits on the always-active root so it can be found and shown; Visual is the part that is toggled.
/// </summary>
public sealed class ShopperThoughtCloud : MonoBehaviour
{
    [Tooltip("Everything that is shown and hidden — the cloud sprite and the icon inside it.")]
    [SerializeField] private GameObject _visual;

    [Tooltip("Renderer inside the cloud that takes the required type's Icon.")]
    [SerializeField] private SpriteRenderer _iconRenderer;

    private Camera _camera;

    private void Awake()
    {
        _visual.SetActive(false);
    }

    public void Show(Sprite icon)
    {
        _iconRenderer.sprite = icon;
        _visual.SetActive(true);
    }

    public void Hide()
    {
        _visual.SetActive(false);
    }

    // Faces the camera flat-on, wherever the cart has turned to.
    private void LateUpdate()
    {
        if (!_visual.activeSelf) return;

        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        transform.rotation = _camera.transform.rotation;
    }
}

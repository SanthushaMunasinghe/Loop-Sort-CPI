using UnityEngine;

public sealed class CursorFollowElement : MonoBehaviour
{
    private static readonly int ClickTrigger = Animator.StringToHash("Click");

    [SerializeField] private Animator _animator;
    [SerializeField] private Camera _canvasCamera;

    private RectTransform _rectTransform;
    private RectTransform _parentRectTransform;

    private void Awake()
    {
        _rectTransform = transform as RectTransform;
        _parentRectTransform = _rectTransform != null ? _rectTransform.parent as RectTransform : null;
    }

    private void Update()
    {
        FollowCursor();

        if (Input.GetMouseButtonDown(0) && _animator != null)
            _animator.SetTrigger(ClickTrigger);
    }

    private void FollowCursor()
    {
        if (_rectTransform == null || _parentRectTransform == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentRectTransform, Input.mousePosition, _canvasCamera, out var localPoint))
            return;

        _rectTransform.anchoredPosition = localPoint;
    }
}

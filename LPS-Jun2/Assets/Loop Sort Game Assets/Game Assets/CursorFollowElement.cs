using UnityEngine;

public sealed class CursorFollowElement : MonoBehaviour
{
    private static readonly int ClickTrigger = Animator.StringToHash("Click");

    [SerializeField] private Animator _animator;
    [SerializeField] private Camera _canvasCamera;

    [Header("Follow Smoothing")]
    [Tooltip("Time (seconds) to catch up to the cursor. Higher = smoother/laggier, lower = snappier.")]
    [SerializeField] private float _smoothTime = 0.08f;
    [Tooltip("Maximum move speed in local units/sec, so fast mouse flicks don't cause a huge jump.")]
    [SerializeField] private float _maxSpeed = 4000f;
    [Tooltip("Ignore cursor deltas smaller than this many local units to prevent jitter from tiny mouse movements.")]
    [SerializeField] private float _deadZone = 0.5f;

    private RectTransform _rectTransform;
    private RectTransform _parentRectTransform;
    private Vector2 _followVelocity;
    private Vector2? _targetPoint;

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

        if (!_targetPoint.HasValue)
        {
            _targetPoint = localPoint;
            _rectTransform.anchoredPosition = localPoint;
            return;
        }

        if ((localPoint - _targetPoint.Value).sqrMagnitude >= _deadZone * _deadZone)
            _targetPoint = localPoint;

        _rectTransform.anchoredPosition = Vector2.SmoothDamp(
            _rectTransform.anchoredPosition, _targetPoint.Value, ref _followVelocity, _smoothTime, _maxSpeed, Time.unscaledDeltaTime);
    }
}

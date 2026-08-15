using UnityEngine;

public class ArrowEffect : EffectBase
{
    [SerializeField] private float Frequency;
    [SerializeField] private float Amplitude;
    [SerializeField] private bool Right;

    private RectTransform _rectTransform;
    private Vector3 _originalPosition;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _originalPosition = _rectTransform.anchoredPosition;
    }

    private void Update()
    {
        var t = GetEffectTime();
        var direction = Right ? _rectTransform.right : _rectTransform.up;
        var offset = direction * (Mathf.Cos(t * Frequency) * Amplitude);
        _rectTransform.anchoredPosition = _originalPosition + offset;
    }
}
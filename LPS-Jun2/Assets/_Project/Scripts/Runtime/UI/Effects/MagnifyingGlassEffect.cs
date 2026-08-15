using UnityEngine;

public class MagnifyingGlassEffect : EffectBase
{
    [SerializeField] private float Frequency;
    [SerializeField] private float Amplitude;

    private RectTransform _rectTransform;
    private Vector2 _originalPosition;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _originalPosition = _rectTransform.anchoredPosition;
    }

    private void Update()
    {
        var t = GetEffectTime();
        var right = Vector2.right * (Mathf.Cos(t * Frequency) * Amplitude);
        var up = Vector2.up * (Mathf.Sin(t * Frequency) * Amplitude);
        _rectTransform.anchoredPosition = _originalPosition + right + up;
    }
}
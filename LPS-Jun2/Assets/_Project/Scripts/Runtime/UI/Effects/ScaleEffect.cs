using UnityEngine;

public sealed class ScaleEffect : EffectBase
{
    [SerializeField] private float Frequency;
    [SerializeField] private float Amplitude;

    private RectTransform _rectTransform;
    private Vector3 _originalScale;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _originalScale = _rectTransform.localScale;
    }

    private void Update()
    {
        var t = GetEffectTime();
        var scale = Vector3.one * (Mathf.Cos(t * Frequency) * Amplitude);
        _rectTransform.localScale = _originalScale + scale;
    }

    public void UpdateOriginalScale(Vector3 scale)
    {
        _originalScale = scale;
    }
}
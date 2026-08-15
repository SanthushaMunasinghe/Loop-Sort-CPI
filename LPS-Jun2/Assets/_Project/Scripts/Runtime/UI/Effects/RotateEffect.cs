using UnityEngine;

public sealed class RotateEffect : EffectBase
{
    [SerializeField] private float Speed;

    private RectTransform _rectTransform;

    private void Awake()
    {
        _rectTransform = transform as RectTransform;
    }

    private void Update()
    {
        var deltaTime = GetEffectDeltaTime();
        _rectTransform.Rotate(Vector3.forward * (deltaTime * Speed), Space.Self);
    }
}
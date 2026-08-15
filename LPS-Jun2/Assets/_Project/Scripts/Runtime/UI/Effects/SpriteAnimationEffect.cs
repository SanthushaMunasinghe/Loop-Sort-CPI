using UnityEngine;
using UnityEngine.UI;

public sealed class SpriteAnimationEffect : EffectBase
{
    [SerializeField] private Sprite[] Sprites;
    [SerializeField] private float Speed;

    private Image _image;
    private int _currentIdx;
    private float _nextChangeTimestamp;

    private void Awake()
    {
        _image = GetComponent<Image>();
    }

    private void Update()
    {
        var t = GetEffectTime();
        if (_nextChangeTimestamp > t) return;
        _nextChangeTimestamp = t + Speed;
        _image.sprite = Sprites[_currentIdx++];
        _currentIdx %= Sprites.Length;
    }
}
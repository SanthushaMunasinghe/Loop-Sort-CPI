using System;
using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ToggleElement : MonoBehaviour
{
    [SerializeField] private Button Button;
    [SerializeField] private RectTransform MotionTarget;
    [SerializeField] private Image Image;
    [SerializeField] private Image Background;
    [SerializeField] private TMP_Text StatusText;
    [SerializeField] private GameObject DisabledItem;
    [Space]
    [SerializeField] private Sprite ActiveSprite;
    [SerializeField] private Sprite PassiveSprite;
    [SerializeField] private Sprite ActiveBackgroundSprite;
    [SerializeField] private Sprite PassiveBackgroundSprite;
    [SerializeField] private Vector2 ActivePosition;
    [SerializeField] private Vector2 PassivePosition;
    [SerializeField] private Ease Ease = Ease.InOutBack;
    [SerializeField] private float Duration = .4f;

    private bool _isActive;
    private MotionHandle _motion;

    private event Action<bool> OnChange;

    private void Awake()
    {
        Button.onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        Set(!_isActive);
    }

    public void Initialize(bool isActive)
    {
        _isActive = isActive;
        UpdateView(motion: false);
    }

    public void Set(bool isActive)
    {
        _isActive = isActive;
        UpdateView();
        OnChange?.Invoke(isActive);
    }

    private void UpdateView(bool motion = true)
    {
        if (Image != null) Image.sprite = _isActive ? ActiveSprite : PassiveSprite;
        if (StatusText != null) StatusText.SetText(_isActive ? "ON" : "OFF");
        if (Background != null) Background.sprite = _isActive ? ActiveBackgroundSprite : PassiveBackgroundSprite;
        if (DisabledItem != null) DisabledItem.SetActive(!_isActive);

        if (MotionTarget == null) return;
        var targetPosition = _isActive ? ActivePosition : PassivePosition;
        if (.01f > Vector2.Distance(MotionTarget.anchoredPosition, targetPosition)) return;
        if (motion)
        {
            _motion.TryCancel();
            _motion = LMotion.Create(MotionTarget.anchoredPosition, targetPosition, Duration)
                .WithEase(Ease)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToAnchoredPosition(MotionTarget);
        }
        else
        {
            MotionTarget.anchoredPosition = targetPosition;
        }
    }

    public void AddSubscribe(Action<bool> onChange)
    {
        OnChange += onChange;
    }
}
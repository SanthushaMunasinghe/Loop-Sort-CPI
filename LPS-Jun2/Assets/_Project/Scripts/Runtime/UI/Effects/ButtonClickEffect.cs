using System.Collections.Generic;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

public sealed class ButtonClickEffect : EffectBase, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IDeselectHandler
{
    [SerializeField] private bool DisableScaleEffect;
    [SerializeField] private float ChildMoveOffset;

    private Button _button;
    private MotionHandle _motion;
    private Vector3 _mainScale;
    private bool _isButtonDown;

    private static HapticModule _hapticModule;
    private static AudioModule _audioModule;
    private static PointerEventData _emptyPointerEventData;

    private readonly List<RectTransform> _rectTransforms = new();

    private void Awake()
    {
        _mainScale = transform.localScale;
    }

    private void Start()
    {
        _emptyPointerEventData ??= new PointerEventData(EventSystem.current);
        _button = GetComponent<Button>();
        GetComponentsInChildren(_rectTransforms);
        _rectTransforms.RemoveAt(0);
    }

    private void OnDestroy()
    {
        _motion.TryCancel();
    }

    private void OnClicked()
    {
        if (!enabled) return;
        if (!_button.interactable) return;

        if (_hapticModule == null || _audioModule == null)
        {
            var container = LifetimeScopeH.FindScope<BootstrapScope>().Container;
            _hapticModule = container.Resolve<HapticModule>();
            _audioModule = container.Resolve<AudioModule>();
        }

        _hapticModule.PlaySoft();
        _audioModule.GetPlayer().Play(_audioModule.Sounds.Click);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnButtonDown();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        OnButtonUp();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClicked();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        // if i disable the input system, button stays pressed and we need to release it manually
        _button.OnPointerUp(_emptyPointerEventData);
        OnButtonUp();
    }

    private void OnButtonDown()
    {
        if (DisableScaleEffect) return;
        if (_isButtonDown) return;
        _isButtonDown = true;

        _motion.TryCancel();
        _motion = LMotion.Create(transform.localScale, _mainScale * .9f, .075f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.InOutBack)
            .BindToLocalScale(transform);

        if (ChildMoveOffset <= 0) return;
        if (_rectTransforms.Count == 0) return;

        foreach (var rt in _rectTransforms)
        {
            rt.anchoredPosition += Vector2.down * ChildMoveOffset;
        }
    }

    private void OnButtonUp()
    {
        if (DisableScaleEffect) return;
        if (!_isButtonDown) return;
        _isButtonDown = false;

        _motion.TryCancel();
        _motion = LMotion.Create(transform.localScale, _mainScale, .075f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithEase(Ease.InOutBack)
            .BindToLocalScale(transform);

        if (ChildMoveOffset <= 0) return;
        if (_rectTransforms.Count == 0) return;

        foreach (var rt in _rectTransforms)
        {
            rt.anchoredPosition += Vector2.up * ChildMoveOffset;
        }
    }
}
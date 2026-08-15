using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CanvasAlphaTransitionElement : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private CanvasGroup CanvasGroup;

    private MotionHandle _motion;

    private void OnEnable()
    {
        CanvasGroup.alpha = 1f;
    }

    private void OnDisable()
    {
        _motion.TryCancel();
    }

    private void OnDestroy()
    {
        _motion.TryCancel();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _motion.TryCancel();
        var from = CanvasGroup.alpha;
        _motion = LMotion.Create(from, 0f, .2f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAlpha(CanvasGroup);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _motion.TryCancel();
        var from = CanvasGroup.alpha;
        _motion = LMotion.Create(from, 1f, .2f)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAlpha(CanvasGroup);
    }
}
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class RectTransformResizerElement : MonoBehaviour, ILayoutSelfController
{
    [SerializeField] private RectTransform TargetRT;
    [SerializeField] private Vector2 Padding;

    private RectTransform _selfRT;

    private void OnEnable()
    {
        if (TargetRT == null) return;
        if (_selfRT == null) _selfRT = GetComponent<RectTransform>();
        _selfRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
        _selfRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
    }

    private void Update()
    {
        UpdateRectTransform();
    }

    public void SetLayoutHorizontal()
    {
        UpdateRectTransform();
    }

    public void SetLayoutVertical()
    {
        UpdateRectTransform();
    }

    private void UpdateRectTransform()
    {
        if (TargetRT == null) return;
        if (_selfRT == null) _selfRT = GetComponent<RectTransform>();

        var sizeDelta = TargetRT.sizeDelta;
        var horizontalSize = sizeDelta.x + Padding.x;
        var verticalSize = sizeDelta.y + Padding.y;

        _selfRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, horizontalSize);
        _selfRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, verticalSize);
    }
}
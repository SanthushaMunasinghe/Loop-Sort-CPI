using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class ImageAspectSizeFitterElement : MonoBehaviour, ILayoutSelfController
{
    [SerializeField] private Image TargetImage;

    private RectTransform _selfRT;

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
        if (TargetImage == null) return;
        if (_selfRT == null) _selfRT = GetComponent<RectTransform>();

        var sprite = TargetImage.sprite;
        if (sprite == null) return;
        var spriteRatio = sprite.rect.width / sprite.rect.height;

        var size = TargetImage.rectTransform.rect.size;
        if (size.x / size.y > spriteRatio)
        {
            var newWidth = size.y * spriteRatio;
            _selfRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newWidth);
        }
        else
        {
            var newHeight = size.x / spriteRatio;
            _selfRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newHeight);
        }
    }
}
using UnityEngine;
using UnityEngine.UI;

public sealed class RaycastBlockerImage : Image
{
    private RectTransform _clickableArea;

    public override bool Raycast(Vector2 sp, Camera eventCamera)
    {
        if (_clickableArea == null) return true;
        var isInsideClickableArea = RectTransformUtility.RectangleContainsScreenPoint(_clickableArea, sp, eventCamera);
        return !isInsideClickableArea;
    }

    public void SetClickableArea(RectTransform area)
    {
        _clickableArea = area;
    }

    public void ResetClickableArea()
    {
        _clickableArea = null;
    }
}

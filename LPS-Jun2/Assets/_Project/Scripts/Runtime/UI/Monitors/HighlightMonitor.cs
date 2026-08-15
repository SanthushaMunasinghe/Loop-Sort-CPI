using LitMotion;
using LitMotion.Extensions;
using StatefulUI.Runtime.Core;
using UnityEngine;
using UnityEngine.UI;

public sealed class HighlightMonitor : MonitorBase
{
    private Image _mask;
    private ContainerView _uiMaskContainer;
    private ContainerView _worldMaskContainer;

    private void Awake()
    {
        _mask = GetImage(ImageRole.Mask);
        _uiMaskContainer = GetContainer(ContainerRole.UIMask);
        _worldMaskContainer = GetContainer(ContainerRole.WorldMask);
        _mask.gameObject.SetActive(false);
    }

    public void CreateUIMask(Vector3 worldPoint, Vector2 size, Vector2 offset = default)
    {
        ShowMask();
        var instance = _uiMaskContainer.AddInstance<RectTransform>();
        WorldToCanvasSpace(instance, worldPoint);
        instance.anchoredPosition += offset;
        instance.sizeDelta = size;
    }

    public void CreateWorldMask(Vector3 worldPoint, Vector2 size)
    {
        ShowMask();
        var instance = _worldMaskContainer.AddInstance<SpriteRenderer>();
        var instanceT = instance.transform;
        instanceT.localRotation = Quaternion.identity;
        instanceT.position = worldPoint;
        instanceT.localScale = Vector3.one * (1f / transform.localScale.x);
        instance.size = size;
    }

    public void ShowMask(float alpha = .8f)
    {
        if (!_mask.gameObject.activeSelf) LMotion.Create(0f, alpha, .5f).BindToColorA(_mask);
        _mask.gameObject.SetActive(true);
    }

    public void ClearMask()
    {
        _uiMaskContainer.Clear();
        _worldMaskContainer.Clear();
        _mask.gameObject.SetActive(false);
    }

    public void HideMask()
    {
        _mask.gameObject.SetActive(false);
    }
}
using Cysharp.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public sealed class RectTransformSafeAreaElement : MonoBehaviour
{
    private RectTransform _rectTransform;
    private Rect _lastSafeArea;
    private float _bannerAnchorMinOffset;
    private float _bannerAnchorMaxOffset;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        Refresh().Forget();
    }

#if UNITY_EDITOR
    private void LateUpdate()
    {
        Refresh().Forget();
    }
#endif

    public async UniTaskVoid Refresh()
    {
        var safeArea = Screen.safeArea;
        if (_lastSafeArea == safeArea) return;
        _lastSafeArea = safeArea;

        await UniTask.NextFrame();

        var anchorMin = _lastSafeArea.position;
        var anchorMax = _lastSafeArea.position + _lastSafeArea.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;
        _rectTransform.anchorMin = anchorMin + Vector2.up * _bannerAnchorMinOffset;
        _rectTransform.anchorMax = anchorMax - Vector2.up * _bannerAnchorMaxOffset;
        _rectTransform.sizeDelta = Vector2.zero;
        _rectTransform.ForceUpdateRectTransforms();
    }
}
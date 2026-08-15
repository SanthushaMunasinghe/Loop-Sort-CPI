using Freya;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class CameraScalerEffect : EffectBase
{
    [SerializeField] private float MainOrthographicSize;

    private static Camera _camera;

    private void Start()
    {
        if (_camera == null)
        {
            var scope = LifetimeScopeH.FindScope<BootstrapScope>();
            _camera = scope.Container.Resolve<Camera>();
        }
    }

    private void LateUpdate()
    {
        if (_camera == null) return;

        var t = transform;
        var remap = _camera.orthographicSize.Remap(0f, MainOrthographicSize, 0f, 1f);
        t.localScale = Vector3.one * remap;
    }
}
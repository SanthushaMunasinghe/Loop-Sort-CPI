using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class LookAtCameraEffect : EffectBase
{
    [SerializeField] private Vector3 Offset;

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
        var cameraT = _camera.transform;
        var direction = cameraT.forward;
        t.rotation = Quaternion.LookRotation(direction);
        t.rotation *= Quaternion.Euler(Offset);
    }
}
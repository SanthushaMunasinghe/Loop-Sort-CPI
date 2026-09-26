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
            // A level scene played on its own (TestRunner) has no BootstrapScope — fall back to the
            // scene's own camera rather than throwing and never facing it.
            var scope = LifetimeScopeH.FindScope<BootstrapScope>();
            _camera = scope != null ? scope.Container.Resolve<Camera>() : Camera.main;
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
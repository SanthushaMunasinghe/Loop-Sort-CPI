using UnityEngine;

public static class CameraH
{
    /// <summary>
    /// Gets the active camera based on the highest depth and enabled state.
    /// </summary>
    /// <returns>The active camera if available, otherwise null.</returns>
    public static Camera GetActiveCamera()
    {
        var allCameras = Camera.allCameras;
        if (allCameras == null || allCameras.Length == 0) return null;

        Camera activeCamera = null;
        foreach (var camera in allCameras)
        {
            if (!camera.enabled) continue;
            if (activeCamera != null && activeCamera.depth > camera.depth) continue;
            activeCamera = camera;
        }

        return activeCamera;
    }
}
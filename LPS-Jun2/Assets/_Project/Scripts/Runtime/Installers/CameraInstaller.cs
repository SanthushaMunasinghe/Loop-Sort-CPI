using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class CameraInstaller : InstallerBase
{
    private Camera _cameraInstance;

    public override async UniTask Initialize(Dictionary<string, string> args)
    {
        var resourceRequest = Resources.LoadAsync<Camera>("Core/Camera");
        var cameraPrefab = (Camera)await resourceRequest;
        _cameraInstance = Object.Instantiate(cameraPrefab);
        Object.DontDestroyOnLoad(_cameraInstance.gameObject);
    }

    public override void Install(IContainerBuilder builder)
    {
        builder.RegisterComponent(_cameraInstance).AsSelf();
        builder.RegisterComponent(_cameraInstance.GetComponent<AudioListener>()).AsSelf();
        builder.RegisterComponent(_cameraInstance.GetComponent<CinemachineBrain>()).AsSelf();
    }
}
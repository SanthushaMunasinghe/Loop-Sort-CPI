using System;
using UnityEngine;

#if UNITY_IOS
using Unity.Advertisement.IosSupport;
using UnityEngine.iOS;
#endif

public sealed class iOSATTHandler : MonoBehaviour
{
    private void Start()
    {
#if UNITY_IOS && !UNITY_EDITOR
        RequestTrackingAuthorization();
#else
        SceneManagerH.LoadNextScene();
#endif
    }

#if UNITY_IOS
    private void RequestTrackingAuthorization()
    {
        var status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
        var currentVersion = new Version(Device.systemVersion);
        var ios14 = new Version("14.5");

        if (status == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED && currentVersion >= ios14)
        {
            ATTrackingStatusBinding.RequestAuthorizationTracking(OnRequestAuthorizationTrackingComplete);
        }
        else
        {
            SceneManagerH.LoadNextScene();
        }
    }

    private void OnRequestAuthorizationTrackingComplete(int status)
    {
        SceneManagerH.LoadNextScene();
    }
#endif
}
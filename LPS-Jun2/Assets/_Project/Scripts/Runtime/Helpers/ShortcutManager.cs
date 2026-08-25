#if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEngine;

/// <summary>
/// Dev/QA keyboard shortcuts. Not a singleton — drop this on a GameObject in whichever scene you
/// want it active in, same as GlobalTrigger or SceneScope. Only invokes actions; the actions
/// themselves live wherever they're already owned (e.g. SceneScope.ToggleGlobalTrigger).
///
/// T - toggle the scene's Global Trigger on/off
/// Q - start/stop Unity Recorder, using whatever settings are already set up in the Recorder window
/// R - reload the active scene
/// O - lerp every Empty Carrier Row with Use Start Position Lerp on back to its authored position
/// P - toggle the scene's Pointer GameObject on/off
/// C - lerp Scene Scope's Movable Camera back to its authored position (if Use Camera Start Position Lerp is on)
/// </summary>
public sealed class ShortcutManager : MonoBehaviour
{
    [SerializeField] private SceneScope _sceneScope;

    [Tooltip("Recording auto-stops after this many seconds. Time left is logged every 10s, bold at " +
             "the 10s and 5s marks.")]
    [SerializeField] private float _recordingTimeoutSeconds = 60f;

    private Coroutine _recordingTimeoutRoutine;

    private void Update()
    {
        if (InputH.GetKeyDown(KeyCode.T)) ToggleGlobalTrigger();
        if (InputH.GetKeyDown(KeyCode.Q)) ToggleRecording();
        if (InputH.GetKeyDown(KeyCode.R)) ReloadScene();
        if (InputH.GetKeyDown(KeyCode.O)) LerpEmptyCarrierRows();
        if (InputH.GetKeyDown(KeyCode.P)) TogglePointer();
        if (InputH.GetKeyDown(KeyCode.C)) LerpCamera();
    }

    private void ToggleGlobalTrigger()
    {
        if (_sceneScope == null)
        {
            Debug.LogWarning($"<b>{nameof(ShortcutManager)}</b>: no Scene Scope assigned, can't toggle Global Trigger.", this);
            return;
        }

        _sceneScope.ToggleGlobalTrigger();
        Debug.Log($"<b>{nameof(ShortcutManager)}</b>: toggled Global Trigger.", this);
    }

    private void ToggleRecording()
    {
        var recorderWindow = (RecorderWindow)EditorWindow.GetWindow(typeof(RecorderWindow), false, null, false);

        if (!recorderWindow.IsRecording())
        {
            recorderWindow.StartRecording();
            Debug.Log($"<b>{nameof(ShortcutManager)}</b>: started recording.");
            _recordingTimeoutRoutine = StartCoroutine(RecordingTimeoutRoutine(recorderWindow));
        }
        else
        {
            StopRecording(recorderWindow);
        }
    }

    private void StopRecording(RecorderWindow recorderWindow)
    {
        recorderWindow.StopRecording();
        Debug.Log($"<b>{nameof(ShortcutManager)}</b>: stopped recording.");

        if (_recordingTimeoutRoutine == null) return;
        StopCoroutine(_recordingTimeoutRoutine);
        _recordingTimeoutRoutine = null;
    }

    private IEnumerator RecordingTimeoutRoutine(RecorderWindow recorderWindow)
    {
        var timeoutSeconds = Mathf.RoundToInt(_recordingTimeoutSeconds);
        var elapsedSeconds = 0;

        while (elapsedSeconds < timeoutSeconds)
        {
            yield return new WaitForSeconds(1f);
            elapsedSeconds++;

            var remainingSeconds = timeoutSeconds - elapsedSeconds;

            if (remainingSeconds == 10 || remainingSeconds == 5)
                Debug.Log($"<b>{nameof(ShortcutManager)}</b>: <b>recording — {remainingSeconds}s left.</b>");
            else if (remainingSeconds > 0 && remainingSeconds % 10 == 0)
                Debug.Log($"<b>{nameof(ShortcutManager)}</b>: recording — {remainingSeconds}s left.");
        }

        _recordingTimeoutRoutine = null;
        if (!recorderWindow.IsRecording()) yield break;

        Debug.Log($"<b>{nameof(ShortcutManager)}</b>: recording timeout reached, stopping automatically.");
        StopRecording(recorderWindow);
    }

    private static void ReloadScene()
    {
        Debug.Log($"<b>{nameof(ShortcutManager)}</b>: reloading scene.");
        SceneManagerH.ReloadScene();
    }

    private void LerpEmptyCarrierRows()
    {
        if (_sceneScope == null)
        {
            Debug.LogWarning($"<b>{nameof(ShortcutManager)}</b>: no Scene Scope assigned, can't lerp Empty Carrier Rows.", this);
            return;
        }

        _sceneScope.LerpEmptyCarrierRows();
        Debug.Log($"<b>{nameof(ShortcutManager)}</b>: triggered Empty Carrier Row position lerp.", this);
    }

    private void TogglePointer()
    {
        if (_sceneScope == null)
        {
            Debug.LogWarning($"<b>{nameof(ShortcutManager)}</b>: no Scene Scope assigned, can't toggle Pointer.", this);
            return;
        }

        _sceneScope.TogglePointer();
        Debug.Log($"<b>{nameof(ShortcutManager)}</b>: toggled Pointer.", this);
    }

    private void LerpCamera()
    {
        if (_sceneScope == null)
        {
            Debug.LogWarning($"<b>{nameof(ShortcutManager)}</b>: no Scene Scope assigned, can't lerp the camera.", this);
            return;
        }

        _sceneScope.LerpCameraToOriginalPosition();
        Debug.Log($"<b>{nameof(ShortcutManager)}</b>: triggered camera pan.", this);
    }
}
#endif

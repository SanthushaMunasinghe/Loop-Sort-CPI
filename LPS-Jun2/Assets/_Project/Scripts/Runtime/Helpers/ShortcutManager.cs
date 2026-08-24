#if UNITY_EDITOR
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
/// </summary>
public sealed class ShortcutManager : MonoBehaviour
{
    [SerializeField] private SceneScope _sceneScope;

    private void Update()
    {
        if (InputH.GetKeyDown(KeyCode.T)) ToggleGlobalTrigger();
        if (InputH.GetKeyDown(KeyCode.Q)) ToggleRecording();
        if (InputH.GetKeyDown(KeyCode.R)) ReloadScene();
        if (InputH.GetKeyDown(KeyCode.O)) LerpEmptyCarrierRows();
        if (InputH.GetKeyDown(KeyCode.P)) TogglePointer();
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

    private static void ToggleRecording()
    {
        var recorderWindow = (RecorderWindow)EditorWindow.GetWindow(typeof(RecorderWindow), false, null, false);

        if (!recorderWindow.IsRecording())
        {
            recorderWindow.StartRecording();
            Debug.Log($"<b>{nameof(ShortcutManager)}</b>: started recording.");
        }
        else
        {
            recorderWindow.StopRecording();
            Debug.Log($"<b>{nameof(ShortcutManager)}</b>: stopped recording.");
        }
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
}
#endif

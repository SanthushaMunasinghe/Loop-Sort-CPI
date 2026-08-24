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
/// </summary>
public sealed class ShortcutManager : MonoBehaviour
{
    [SerializeField] private SceneScope _sceneScope;

    private void Update()
    {
        if (InputH.GetKeyDown(KeyCode.T)) ToggleGlobalTrigger();
        if (InputH.GetKeyDown(KeyCode.Q)) ToggleRecording();
        if (InputH.GetKeyDown(KeyCode.R)) ReloadScene();
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
}
#endif

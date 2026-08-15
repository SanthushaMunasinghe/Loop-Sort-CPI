using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class HideDDOL
{
    private static GameObject _ddolObject;

    static HideDDOL()
    {
        var hide = Prefs.HideDDOL.Value;
        if (!hide) return;
        SceneManager.activeSceneChanged += (_, _) => HideAllDDOL();
    }

    private static void HideAllDDOL()
    {
        if (!EditorApplication.isPlaying) return;
        _ddolObject ??= new GameObject(nameof(HideDDOL));
        _ddolObject.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(_ddolObject);
        SceneVisibilityManager.instance.Hide(_ddolObject.scene);
    }
}
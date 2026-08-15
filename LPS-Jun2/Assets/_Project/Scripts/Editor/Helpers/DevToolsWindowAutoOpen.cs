using UnityEditor;

[InitializeOnLoad]
internal static class DevToolsWindowAutoOpen
{
    private const string SessionKey = "DevToolsWindow.OpenedThisSession";

    static DevToolsWindowAutoOpen()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);

        EditorApplication.delayCall += DevToolsWindow.ShowWindow;
    }
}
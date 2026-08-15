using System;
using UnityEditor;
using UnityEngine;
using VContainer;

public sealed partial class DevToolsWindow : EditorWindow
{
    [Inject] private IObjectResolver _resolver;

    private bool _injected;
    private Vector2 _scrollPosition;
    private string _searchFilter;

    [MenuItem("Tools/Dev Tools", priority = -100)]
    public static void ShowWindow()
    {
        var window = GetWindow<DevToolsWindow>("Dev Tools");
        window.Show();
    }

    private void TryInject()
    {
        if (_resolver == null) _injected = false;
        if (_injected) return;
        _injected = LifetimeScopeH.InjectIfNotScoped<MonitorScope>(this);
    }

    private void OnGUI()
    {
        TryInject();
        DrawToolbar();
        DrawSections();
    }

    private void DrawSections()
    {
        var windowStyle = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 10, 10),
            margin = new RectOffset(5, 5, 5, 5)
        };
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        {
            GUILayout.Label("Editor", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.BeginVertical(windowStyle);
            DrawSection("Data", DrawData);
            DrawSection("Data Container Override", DrawDataContainerOverride);
            // DrawSection("Build", DrawBuild);
            DrawSection("Scene", DrawScene);
            DrawSection("Player Prefs", DrawPlayerPrefs);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.Space(25);
        {
            GUILayout.Label("Runtime", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.BeginVertical(windowStyle);
            if (EditorApplication.isPlaying)
            {
                DrawSection("Game Machine", DrawGameMachine);
                DrawSection("UI", DrawUI);
                DrawSection("Time", DrawTime);
                DrawSection("Game", DrawGame);
                DrawSection("Quality", DrawQuality);
            }
            else
            {
                EditorGUILayout.HelpBox("Waiting for play mode...", MessageType.Info);
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField);
        if (!string.IsNullOrEmpty(_searchFilter)) _searchFilter = _searchFilter.Trim(' ');
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Separator();
    }

    private void DrawSection(string content, Action draw)
    {
        var key = content + "_Foldout";
        var enabled = EditorPrefs.GetBool(key, false);
        enabled = EditorGUILayout.BeginFoldoutHeaderGroup(enabled, content, EditorStyles.foldoutHeader);
        EditorPrefs.SetBool(key, enabled);
        if (enabled) draw();
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(15);
    }

    private bool DrawButton(string text, GUIStyle style = null)
    {
        if (!string.IsNullOrEmpty(_searchFilter))
            if (!text.Contains(_searchFilter, StringComparison.InvariantCultureIgnoreCase))
                return false;

        return style == null ? GUILayout.Button(text) : GUILayout.Button(text, style);
    }
}
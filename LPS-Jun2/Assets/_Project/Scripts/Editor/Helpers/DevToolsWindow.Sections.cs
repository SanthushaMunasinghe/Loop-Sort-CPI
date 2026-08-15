using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using VContainer;

public sealed partial class DevToolsWindow
{
    [Inject] private GameMachine _gameMachine;
    [Inject] private SceneModule _sceneModule;
    [Inject] private Monitors _monitors;

    private DataContainer[] _dataContainers;
    private const string DefaultContainerName = "Data Container Base";
    private float _timeScale = 1f;
    private int _targetFrameRate = 90;
    private bool _highQuality;
    private EditorPrefsBool _startSceneBootstrap = Prefs.StartSceneBootstrap;
    private EditorPrefsBool _hideDDOL = Prefs.HideDDOL;
    private EditorPrefsEnum<Prefs.EditorSdkType> _editorSdk = Prefs.EditorSdk;

    private void DrawGameMachine()
    {
        if (_gameMachine == null) return;

        GUILayout.Label($"Active State: {_gameMachine.ActiveStateName}");
        if (DrawButton("Win")) _gameMachine.RequestStateChange(nameof(GameWinState));
        if (DrawButton("Lose")) _gameMachine.RequestStateChange(nameof(GameReviveState));
    }

    private void DrawUI()
    {
        if (_monitors == null) return;

        if (DrawButton("Toggle Alpha Visibility"))
        {
            _monitors.ToggleAlphaVisibility();
        }
    }

    private void DrawData()
    {
        if (DrawButton("Create New Data"))
        {
            var window = GetWindow<DataCreatorWindow>("Data Creator");
            window.Show();
        }
    }

    private void DrawDataContainerOverride()
    {
        _dataContainers = Resources.LoadAll<DataContainer>("");

        var dataContainerPaths = Prefs.DataContainerPaths;
        foreach (var dataContainer in _dataContainers)
        {
            var containerName = dataContainer.name;
            if (containerName == DefaultContainerName) continue;

            var selected = dataContainerPaths.Value.Contains(containerName);
            var newSelected = EditorGUILayout.Toggle(containerName, selected);
            if (selected == newSelected) continue;

            var dataPath = AssetDatabase.GetAssetPath(dataContainer);
            dataPath = dataPath["Assets/_Project/Resources/".Length..];
            dataPath = dataPath[..^".asset".Length];

            if (newSelected)
            {
                var paths = dataContainerPaths.Value;
                if (!string.IsNullOrEmpty(paths)) paths += '\n';
                paths += dataPath;
                dataContainerPaths.Set(paths);
                Debug.Log($"Data Container override selected: {dataContainer.name} - {dataPath}");
            }

            if (!newSelected)
            {
                var paths = dataContainerPaths.Value.Replace(dataPath, string.Empty);
                if (string.IsNullOrWhiteSpace(paths)) paths = string.Empty;
                dataContainerPaths.Set(paths);
                Debug.Log($"Data Container override deselected: {dataContainer.name}");
            }
        }

        if (_dataContainers.Length == 1)
        {
            GUILayout.Label("No data container found.");
        }

        if (!string.IsNullOrEmpty(dataContainerPaths) && DrawButton("Clear Override"))
        {
            dataContainerPaths.DeleteValue();
            Debug.Log("Data Container override cleared");
        }
    }

    private void DrawBuild()
    {
        const string releaseBuildKey = "ReleaseBuild";
        var value = EditorPrefs.GetBool(releaseBuildKey);
        var newValue = EditorGUILayout.Toggle("Release Build", value);
        EditorPrefs.SetBool(releaseBuildKey, newValue);

        if (value != newValue)
        {
            if (newValue)
            {
                SetReleaseBuild(NamedBuildTarget.Android);
                SetReleaseBuild(NamedBuildTarget.iOS);
            }
            else
            {
                ClearReleaseBuild(NamedBuildTarget.Android);
                ClearReleaseBuild(NamedBuildTarget.iOS);
            }
        }

        return;
        void SetReleaseBuild(NamedBuildTarget buildTarget)
        {
            var symbols = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
            symbols += ";" + "RELEASE_BUILD";
            PlayerSettings.SetScriptingDefineSymbols(buildTarget, symbols);
        }
        void ClearReleaseBuild(NamedBuildTarget buildTarget)
        {
            var symbols = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
            symbols = symbols.Replace("RELEASE_BUILD", string.Empty);
            PlayerSettings.SetScriptingDefineSymbols(buildTarget, symbols);
        }
    }

    private void DrawScene()
    {
        {
            var editorSdk = _editorSdk.Value;
            var newEditorSdk = EditorGUILayout.EnumPopup("Editor Sdk", editorSdk);
            _editorSdk.Set((Prefs.EditorSdkType)newEditorSdk);
        }
        {
            var value = _startSceneBootstrap.Value;
            value = EditorGUILayout.Toggle("Start Scene (Bootstrap)", value);
            _startSceneBootstrap.Set(value);
            if (value)
            {
                var playModeStartScene = EditorSceneManager.playModeStartScene;
                var targetScenePath = _editorSdk.Value == Prefs.EditorSdkType.Editor
                    ? SceneNameRefs.Bootstrap
                    : EditorBuildSettings.scenes[0].path;
                if (playModeStartScene == null || !targetScenePath.Contains(playModeStartScene.name))
                {
                    var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(targetScenePath);

                    if (sceneAsset == null)
                    {
                        Debug.LogError($"Scene not found at {targetScenePath}");
                        return;
                    }

                    EditorSceneManager.playModeStartScene = sceneAsset;
                    Debug.Log($"Play mode start scene set to: {targetScenePath}");
                }
            }
            else
            {
                if (EditorSceneManager.playModeStartScene != null)
                {
                    EditorSceneManager.playModeStartScene = null;
                    Debug.Log("Play mode start scene cleared");
                }
            }
        }
        {
            var hide = EditorGUILayout.Toggle("Hide DDOL in Scene View", _hideDDOL.Value);
            _hideDDOL.Set(hide);
        }
    }

    private void DrawPlayerPrefs()
    {
        if (DrawButton("Clear Data"))
        {
            PlayerPrefs.DeleteAll();
            Debug.Log("PlayerPrefs cleared");
        }
    }

    private void DrawTime()
    {
        EditorGUILayout.BeginHorizontal();
        {
            var content = new GUIContent($"Time Scale({Time.timeScale})");
            _timeScale = EditorGUILayout.Slider(content, _timeScale, 0f, 10f);
            if (DrawButton("Update")) Time.timeScale = _timeScale;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        {
            var content = new GUIContent($"Target Frame Rate({Application.targetFrameRate})");
            _targetFrameRate = EditorGUILayout.IntSlider(content, _targetFrameRate, 0, 90);
            if (DrawButton("Update")) Application.targetFrameRate = _targetFrameRate;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawGame()
    {
        if (_sceneModule == null || _sceneModule.Scope == null || _sceneModule.Scope.Container == null) return;
        var scope = _sceneModule.Scope.Container;
    }

    private void DrawQuality()
    {
        _highQuality = EditorGUILayout.Toggle("High Quality", _highQuality);
        if (_highQuality)
        {
            QualitySettings.antiAliasing = 8;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        }
    }
}

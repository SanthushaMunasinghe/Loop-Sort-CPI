using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class DataCreatorWindow : EditorWindow
{
    private Vector2 _scrollPosition;
    private Type[] _dataTypes;

    private void OnGUI()
    {
        _dataTypes ??= TypeCache.GetTypesDerivedFrom<Data>().Where(type => !type.IsAbstract).ToArray();

        EditorGUILayout.LabelField("Select Data Type to Create", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        foreach (var dataType in _dataTypes)
        {
            if (!GUILayout.Button(dataType.Name)) continue;
            CreateAsset(dataType);
        }

        EditorGUILayout.EndScrollView();
    }

    private void CreateAsset(Type dataType)
    {
        var savePath = Selection.activeObject == null ? "Assets" : AssetDatabase.GetAssetPath(Selection.activeObject);
        var path = EditorUtility.SaveFilePanelInProject(
            "Create " + dataType.Name,
            "New" + dataType.Name + ".asset",
            "asset",
            "Select where to save the asset",
            savePath
        );
        Close();

        if (string.IsNullOrEmpty(path)) return;

        var asset = CreateInstance(dataType);
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = asset;
        EditorUtility.FocusProjectWindow();
    }
}
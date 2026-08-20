using Dreamteck.Splines;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for <see cref="ConveyorMeshUpdater"/>.
///
/// Hand-written rather than Odin-driven, to match <see cref="ConveyorShapeBuilderEditor"/> and
/// <see cref="LevelSandboxEditor"/> — same status-line-then-button layout, since this is the same
/// kind of one-shot authoring tool.
/// </summary>
[CustomEditor(typeof(ConveyorMeshUpdater))]
public sealed class ConveyorMeshUpdaterEditor : Editor
{
    private ConveyorMeshUpdater Updater => (ConveyorMeshUpdater)target;

    public override void OnInspectorGUI()
    {
        DrawFields();

        EditorGUILayout.Space(10);
        DrawStatus();
        EditorGUILayout.Space(10);
        DrawUpdateButton();
    }

    // ----------------------------------------------------------------- fields

    private void DrawFields()
    {
        serializedObject.Update();

        var property = serializedObject.GetIterator();
        for (var enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
        {
            if (property.propertyPath == "m_Script") continue;
            EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    // ----------------------------------------------------------------- status

    private void DrawStatus()
    {
        EditorGUILayout.LabelField("Spline", EditorStyles.boldLabel);

        var spline = Updater.GetComponent<SplineComputer>();
        if (spline == null)
        {
            EditorGUILayout.HelpBox(
                "No SplineComputer on this GameObject. Attach this component to the Conveyor prefab " +
                "root — the one carrying SplineComputer and SplineMesh.",
                MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField("Points", spline.pointCount.ToString());
        EditorGUILayout.LabelField("Length", spline.CalculateLength().ToString("F2"));
        EditorGUILayout.LabelField("Closed", spline.isClosed ? "yes" : "no");

        var belt = Updater.GetComponent<SplineMesh>();
        if (belt == null)
        {
            EditorGUILayout.HelpBox("No SplineMesh (belt) on this GameObject.", MessageType.Error);
            return;
        }

        // Tile count is the smoothness control, so it is the one number worth reading back: a belt
        // sitting at its authored count against a much longer spline is exactly the "faceted until I
        // press Play" case, and a collider that disagrees with the belt is the "blocks ride a
        // different shape than they see" case.
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Tiles", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Belt", DescribeCounts(belt));

        var collider = FindColliderMesh(belt);
        EditorGUILayout.LabelField("Collider", collider != null ? DescribeCounts(collider) : "—");

        if (collider != null && belt.GetChannelCount() > 0 && collider.GetChannelCount() > 0 &&
            belt.GetChannel(0).count != collider.GetChannel(0).count)
        {
            EditorGUILayout.HelpBox(
                "Belt and collider tile counts disagree, so the surface blocks collide with is not " +
                "the shape you see. Press Update Mesh to put both on the same density.",
                MessageType.Warning);
        }
    }

    private static string DescribeCounts(SplineMesh mesh)
    {
        var channelCount = mesh.GetChannelCount();
        if (channelCount == 0) return "no channels";

        var counts = new string[channelCount];
        for (var i = 0; i < channelCount; i++) counts[i] = mesh.GetChannel(i).count.ToString();

        return string.Join(", ", counts);
    }

    // Same rule the component itself uses: any child SplineMesh that is not the belt's own.
    private static SplineMesh FindColliderMesh(SplineMesh belt)
    {
        foreach (var mesh in belt.GetComponentsInChildren<SplineMesh>(true))
        {
            if (mesh == belt) continue;
            if (mesh.transform == belt.transform) continue;
            return mesh;
        }

        return null;
    }

    // ---------------------------------------------------------------- update

    private void DrawUpdateButton()
    {
        EditorGUILayout.LabelField("Update", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Update Mesh", GUILayout.Height(32)))
                Updater.UpdateMesh();

            if (GUILayout.Button("Resolve References"))
            {
                Undo.RecordObject(Updater, "Resolve References");
                Updater.ResolveReferences();
                EditorUtility.SetDirty(Updater);
            }
        }

        EditorGUILayout.HelpBox(
            "Update Mesh rebuilds the spline, refits the belt mesh (and the collider, if enabled) to " +
            "the current Width Modifier, and re-spaces the existing arrows and slots — with them, the " +
            "blocks riding each slot. It never adds, removes, or reshapes points; it only makes the " +
            "conveyor match whatever shape is already on the spline.",
            MessageType.None);

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            EditorGUILayout.HelpBox("Not available in play mode.", MessageType.Warning);
    }
}

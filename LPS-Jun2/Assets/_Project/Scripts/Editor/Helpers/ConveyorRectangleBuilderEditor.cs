using Dreamteck.Splines;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for <see cref="ConveyorRectangleBuilder"/>.
///
/// Hand-written rather than Odin-driven, to match <see cref="ConveyorMeshUpdaterEditor"/> — same
/// status-line-then-button layout, since this is the same kind of one-shot authoring tool.
/// </summary>
[CustomEditor(typeof(ConveyorRectangleBuilder))]
public sealed class ConveyorRectangleBuilderEditor : Editor
{
    private ConveyorRectangleBuilder Builder => (ConveyorRectangleBuilder)target;

    public override void OnInspectorGUI()
    {
        DrawFields();

        EditorGUILayout.Space(10);
        DrawStatus();
        EditorGUILayout.Space(10);
        DrawBuildButton();
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

        var spline = Builder.GetComponent<SplineComputer>();
        if (spline == null)
        {
            EditorGUILayout.HelpBox(
                "No SplineComputer on this GameObject. Attach this component to the Conveyor prefab " +
                "root.",
                MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField("Points", spline.pointCount.ToString());
        EditorGUILayout.LabelField("Length", spline.CalculateLength().ToString("F2"));
        EditorGUILayout.LabelField("Closed", spline.isClosed ? "yes" : "no");
        EditorGUILayout.LabelField("Type", spline.type.ToString());

        var splineTypeProperty = serializedObject.FindProperty("_splineType");
        if (splineTypeProperty.enumValueIndex != (int)ConveyorSplineType.Linear)
        {
            EditorGUILayout.HelpBox(
                "Corners will stay visibly rounded, however high Points Per Side goes — this type " +
                "treats/blends points rather than passing exactly through each one. That's expected, " +
                "not a bug; switch Spline Type to Linear for an exact rectangle instead.",
                MessageType.Info);
        }

        if (Builder.GetComponent<ConveyorMeshUpdater>() == null)
        {
            EditorGUILayout.HelpBox(
                "No ConveyorMeshUpdater on this GameObject. It refits the belt mesh, collider, " +
                "slots and arrows once the rectangle is built.",
                MessageType.Error);
        }
    }

    // ----------------------------------------------------------------- build

    private void DrawBuildButton()
    {
        EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Build Rectangle", GUILayout.Height(32)))
                Builder.BuildRectangle();

            if (GUILayout.Button("Resolve References"))
            {
                Undo.RecordObject(Builder, "Resolve References");
                Builder.ResolveReferences();
                EditorUtility.SetDirty(Builder);
            }
        }

        EditorGUILayout.HelpBox(
            "Build Rectangle replaces the spline's points with a closed Width x Height rectangle at " +
            "the chosen Spline Type and Points Per Side, then refits the belt mesh, collider, and " +
            "re-spaces the existing slots and arrows via ConveyorMeshUpdater.",
            MessageType.None);

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            EditorGUILayout.HelpBox("Not available in play mode.", MessageType.Warning);
    }
}

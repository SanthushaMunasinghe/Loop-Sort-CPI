using Dreamteck.Splines;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for <see cref="ConveyorPathBuilder"/>.
///
/// Hand-written rather than Odin-driven, to match <see cref="ConveyorRectangleBuilderEditor"/> and
/// <see cref="ConveyorMeshUpdaterEditor"/> — same status-line-then-button layout, since this is the
/// same kind of one-shot authoring tool.
/// </summary>
[CustomEditor(typeof(ConveyorPathBuilder))]
public sealed class ConveyorPathBuilderEditor : Editor
{
    private ConveyorPathBuilder Builder => (ConveyorPathBuilder)target;

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
        EditorGUILayout.LabelField("Path", EditorStyles.boldLabel);

        var pathParentProperty = serializedObject.FindProperty("_pathParent");
        var pathParent = pathParentProperty.objectReferenceValue as Transform;

        if (pathParent == null)
        {
            EditorGUILayout.HelpBox(
                "No Path Parent assigned. Assign a transform whose direct children (in sibling " +
                "order) mark the path's waypoints.",
                MessageType.Error);
        }
        else if (pathParent.childCount < 3)
        {
            EditorGUILayout.HelpBox(
                $"Path Parent '{pathParent.name}' has {pathParent.childCount} child(ren); at " +
                "least 3 are needed to close a loop.",
                MessageType.Error);
        }
        else
        {
            EditorGUILayout.LabelField("Waypoints", pathParent.childCount.ToString());
        }

        EditorGUILayout.Space(6);
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
                "Corners will stay visibly rounded, however high Subdivisions Per Segment goes — " +
                "this type treats/blends points rather than passing exactly through each one. " +
                "That's expected, not a bug; switch Spline Type to Linear for an exact fit instead.",
                MessageType.Info);
        }

        if (Builder.GetComponent<ConveyorMeshUpdater>() == null)
        {
            EditorGUILayout.HelpBox(
                "No ConveyorMeshUpdater on this GameObject. It refits the belt mesh, collider, " +
                "slots and arrows once the path is built.",
                MessageType.Error);
        }
    }

    // ----------------------------------------------------------------- build

    private void DrawBuildButton()
    {
        EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Build Path", GUILayout.Height(32)))
                Builder.BuildPath();

            if (GUILayout.Button("Resolve References"))
            {
                Undo.RecordObject(Builder, "Resolve References");
                Builder.ResolveReferences();
                EditorUtility.SetDirty(Builder);
            }
        }

        EditorGUILayout.HelpBox(
            "Build Path replaces the spline's points with a closed loop through Path Parent's " +
            "children (in sibling order, at their exact world positions) at the chosen Spline Type " +
            "and Subdivisions Per Segment, then refits the belt mesh, collider, and re-spaces the " +
            "existing slots and arrows via ConveyorMeshUpdater.",
            MessageType.None);

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            EditorGUILayout.HelpBox("Not available in play mode.", MessageType.Warning);
    }
}

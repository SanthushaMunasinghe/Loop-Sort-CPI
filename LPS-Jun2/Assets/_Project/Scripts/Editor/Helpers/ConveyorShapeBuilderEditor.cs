using Dreamteck.Splines;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for <see cref="ConveyorShapeBuilder"/>.
///
/// Hand-written rather than Odin-driven, to match <see cref="LevelSandboxEditor"/> — the two are
/// used side by side and the same status-line-then-buttons layout keeps the workflow readable. The
/// readout above the buttons is the point of it: the three shape buttons only move the spline, so
/// without a live point count and length there is no way to see what a bevel did until you press
/// Rebuild Conveyor.
/// </summary>
[CustomEditor(typeof(ConveyorShapeBuilder))]
public sealed class ConveyorShapeBuilderEditor : Editor
{
    private ConveyorShapeBuilder Builder => (ConveyorShapeBuilder)target;

    public override void OnInspectorGUI()
    {
        DrawFields();

        EditorGUILayout.Space(10);
        DrawStatus();
        EditorGUILayout.Space(10);
        DrawShapeButtons();
        EditorGUILayout.Space(10);
        DrawRebuildButton();
    }

    // ----------------------------------------------------------------- fields

    /// <summary>
    /// Stands in for DrawDefaultInspector so the size fields can be shown one way at a time: Radius
    /// and Width/Height are alternatives, and showing both invites setting the one that is ignored.
    ///
    /// Walks the serialized properties rather than listing them, so the component's [Header]s and
    /// tooltips keep working and a field added later shows up without touching this file.
    /// </summary>
    private void DrawFields()
    {
        serializedObject.Update();

        var byWidthHeight = serializedObject.FindProperty("_sizeMode").enumValueIndex ==
                            (int)ConveyorShapeBuilder.SizeMode.WidthAndHeight;
        var isEllipse = serializedObject.FindProperty("_shapeKind").enumValueIndex ==
                        (int)ConveyorShapeBuilder.ShapeKind.Ellipse;

        var property = serializedObject.GetIterator();
        for (var enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
        {
            if (property.propertyPath == "m_Script") continue;
            if (IsHidden(property.propertyPath)) continue;

            EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
        return;

        bool IsHidden(string path) => path switch
        {
            "_radius" => byWidthHeight,
            "_width" or "_height" => !byWidthHeight,
            "_sideCount" => isEllipse,
            "_segmentCount" => !isEllipse,
            _ => false
        };
    }

    // ----------------------------------------------------------------- status

    private void DrawStatus()
    {
        EditorGUILayout.LabelField("Spline", EditorStyles.boldLabel);

        var spline = Builder.GetComponent<SplineComputer>();
        if (spline == null)
        {
            EditorGUILayout.HelpBox(
                "No SplineComputer on this GameObject. Attach this component to the generated " +
                "Conveyor object — the prefab instance that carries SplineComputer and SplineMesh.",
                MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField("Points", spline.pointCount.ToString());
        EditorGUILayout.LabelField("Length", spline.CalculateLength().ToString("F2"));
        EditorGUILayout.LabelField("Footprint", DescribeFootprint(spline));
        EditorGUILayout.LabelField("Closed", spline.isClosed ? "yes" : "no");
        EditorGUILayout.LabelField("Type", spline.type.ToString());

        if (spline.type == Spline.Type.BSpline)
        {
            // The footprint above is measured on the control points, which a BSpline stays inside —
            // one more reason the reading will not match what Width and Height asked for.
            EditorGUILayout.HelpBox(
                "BSpline treats points as control points and does not pass through them, so a shape " +
                "comes out smaller and rounder than authored and the bevel has little left to do. " +
                "Set Spline Type to Linear or CatmullRom above if you want the shape you draw.",
                MessageType.Warning);
        }
    }

    /// <summary>
    /// Ground size of the spline's control points, measured back in the builder's own local space —
    /// the frame Width and Height are authored in — so the reading can be compared with what was
    /// typed rather than with world units.
    /// </summary>
    private string DescribeFootprint(SplineComputer spline)
    {
        if (spline.pointCount == 0) return "—";

        var toLocal = Builder.transform.worldToLocalMatrix;
        var points = spline.GetPoints();

        var first = toLocal.MultiplyPoint3x4(points[0].position);
        var min = new Vector2(first.x, first.z);
        var max = min;

        foreach (var point in points)
        {
            var local = toLocal.MultiplyPoint3x4(point.position);
            min.x = Mathf.Min(min.x, local.x);
            min.y = Mathf.Min(min.y, local.z);
            max.x = Mathf.Max(max.x, local.x);
            max.y = Mathf.Max(max.y, local.z);
        }

        return $"{max.x - min.x:F2} x {max.y - min.y:F2}";
    }

    // ------------------------------------------------------------------ shape

    private void DrawShapeButtons()
    {
        EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Create Shape", GUILayout.Height(28)))
                Builder.CreateShape();

            var noCorners = !Builder.ShapeGenerated || Builder.ShapeBeveled || Builder.ShapeIsSmooth;
            using (new EditorGUI.DisabledScope(noCorners))
            {
                if (GUILayout.Button("Bevel Shape"))
                    Builder.BevelShape();
            }

            if (GUILayout.Button("Bevel Existing Points"))
                Builder.BevelExistingPoints();
        }

        if (!Builder.ShapeGenerated)
        {
            EditorGUILayout.HelpBox(
                "Bevel Shape is for a shape created here. Bevel Existing Points always runs and " +
                "rounds whatever is on the spline, including a path generated from Splines.json.",
                MessageType.Info);
        }
        else if (Builder.ShapeIsSmooth)
        {
            EditorGUILayout.HelpBox(
                "The generated shape is a ring, which has no corners to round. Raise Segment Count " +
                "or use CatmullRom for a smoother outline.",
                MessageType.Info);
        }
        else if (Builder.ShapeBeveled)
        {
            EditorGUILayout.HelpBox(
                "This shape is already beveled. Press Create Shape to start over, or Bevel Existing " +
                "Points to round it further.",
                MessageType.Info);
        }
    }

    // ---------------------------------------------------------------- rebuild

    private void DrawRebuildButton()
    {
        EditorGUILayout.LabelField("Rebuild", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Rebuild Conveyor", GUILayout.Height(32)))
                Builder.RebuildConveyor();

            if (GUILayout.Button("Resolve References"))
            {
                Undo.RecordObject(Builder, "Resolve References");
                Builder.ResolveReferences();
                EditorUtility.SetDirty(Builder);
            }
        }

        EditorGUILayout.HelpBox(
            "Rebuild Conveyor re-fits the belt mesh, slots, arrows and collider to the current " +
            "spline and re-bakes the conveyor speed and slot collision distance onto LevelSandbox. " +
            "Carriers, blocks, triggers and end meshes are left alone, and the slot count is held at " +
            "what the level was generated with so the blocks stay valid.",
            MessageType.None);

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            EditorGUILayout.HelpBox("Not available in play mode.", MessageType.Warning);
    }
}

using System.Collections.Generic;
using Dreamteck.Splines;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Spline interpolation to apply when the rectangle is built. Mirrors <see cref="Spline.Type"/> under
/// a name that reads naturally in this component's own inspector.
/// </summary>
public enum ConveyorSplineType
{
    Linear = 0,
    CatmullRom = 1,
    BSpline = 2,
}

/// <summary>
/// Editor-only authoring tool that lays a rectangle onto the conveyor's spline and drags the rest of
/// the conveyor onto it in one press.
///
/// <para><b>What it does.</b> <c>Build Rectangle</c> replaces the spline's points with a closed
/// rectangle sized by <see cref="_width"/> (local X) and <see cref="_height"/> (local Z), sets the
/// spline's interpolation to <see cref="_splineType"/>, then hands off to
/// <see cref="ConveyorMeshUpdater"/> to refit the belt mesh, the collider mesh, and re-space whatever
/// slots and arrows already exist. This component never touches the mesh, collider, slots or arrows
/// itself — that is entirely <see cref="ConveyorMeshUpdater"/>'s job, done the same way pressing its
/// own Update Mesh button would.</para>
///
/// <para><b>Spline type and point density.</b> Linear gives an exact rectangle from just its 4
/// corners — real 90° corners, nothing else needed. CatmullRom and BSpline are smoother but do not
/// pass through a corner the way Linear does: CatmullRom's tangents bow the edges near a corner, and
/// BSpline treats every point as a control point rather than a point the curve passes through, so 4
/// points alone come out as an oval rather than a rectangle. <see cref="_pointsPerSide"/> subdivides
/// each edge with extra evenly-spaced, collinear points, which straightens CatmullRom's bowed edges
/// and pulls BSpline's curve tighter toward the rectangle the higher it goes — but for BSpline this is
/// a tightening, not an exact fit: there is no point count that reproduces a mathematically sharp
/// corner, because Dreamteck's BSpline here is a uniform cubic curve over a sliding 4-point window,
/// not a knot-vector NURBS with corner multiplicity. Raise Points Per Side until the corners look
/// tight enough, or switch to Linear for an exact fit.</para>
///
/// <para><b>Deliberately minimal otherwise.</b> Elevation is always local Y=0 (move the GameObject
/// transform for a vertical offset) and the winding is always clockwise seen from above.</para>
///
/// <para><b>Editor only.</b> Every method lives inside <c>#if UNITY_EDITOR</c> and there is no
/// Awake/Start/Update, so nothing here can execute in a player build. The serialized fields
/// deliberately sit outside the guard so the component's serialized layout is identical in the editor
/// and in a build.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SplineComputer))]
[RequireComponent(typeof(ConveyorMeshUpdater))]
public sealed class ConveyorRectangleBuilder : MonoBehaviour
{
    // See the class summary for why these sit outside the UNITY_EDITOR guard.
#pragma warning disable CS0169, CS0414, CS0649

    [Header("References (auto-filled; drag only if this is not on the conveyor)")]
    [Tooltip("The conveyor's path. Auto-filled from this GameObject.")]
    [SerializeField] private SplineComputer _splineComputer;

    [Tooltip("Refits the belt mesh, collider, slots and arrows onto the rectangle once it is built. " +
             "Auto-filled from this GameObject; added automatically if missing.")]
    [SerializeField] private ConveyorMeshUpdater _meshUpdater;

    [Header("Rectangle")]
    [Tooltip("Size along the conveyor's local X, in local units. The finished rectangle measures " +
             "exactly this far across.")]
    [Min(0.0001f)]
    [SerializeField] private float _width = 1f;

    [Tooltip("Size along the conveyor's local Z, in local units — the other ground axis, not " +
             "elevation.")]
    [Min(0.0001f)]
    [SerializeField] private float _height = 1f;

    [Tooltip("Interpolation to apply when the rectangle is written. Linear gives an exact rectangle " +
             "from just its corners. CatmullRom and BSpline are smoother but need Points Per Side " +
             "raised to track the shape — and BSpline's corners stay visibly rounded no matter how " +
             "high that goes, since it treats points as control points rather than passing through " +
             "them.")]
    [SerializeField] private ConveyorSplineType _splineType = ConveyorSplineType.BSpline;

    [Tooltip("How many evenly-spaced spline points make up each of the 4 edges, counting that edge's " +
             "own leading corner — so the total point count is 4x this. 1 reproduces the minimal " +
             "4-corner rectangle. Raise this to straighten CatmullRom's bowed edges or tighten " +
             "BSpline's curve toward the rectangle; for Linear it only adds points; it does not " +
             "change the shape, which is already exact.")]
    [Min(1)]
    [SerializeField] private int _pointsPerSide = 16;

#pragma warning restore CS0169, CS0414, CS0649

#if UNITY_EDITOR

    private const string BuildUndoName = "Build Conveyor Rectangle";

    // Fills the references in when the component is first added, so dropping it on the generated
    // conveyor is all the setup there is.
    private void Reset()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Fills in every reference that is still null. Safe to call repeatedly; never overwrites
    /// something you assigned by hand.
    /// </summary>
    public void ResolveReferences()
    {
        if (_splineComputer == null) _splineComputer = GetComponent<SplineComputer>();
        if (_meshUpdater == null) _meshUpdater = GetComponent<ConveyorMeshUpdater>();
    }

    /// <summary>
    /// Replaces the spline's points with a closed rectangle sized by Width and Height, then hands the
    /// rest of the conveyor over to <see cref="ConveyorMeshUpdater"/>.
    /// </summary>
    public void BuildRectangle()
    {
        if (!CanEdit()) return;
        if (!ValidateDimensions()) return;

        ResolveReferences();
        if (_splineComputer == null)
        {
            LogError("no Spline Computer. Attach this to the Conveyor prefab root.");
            return;
        }

        if (_meshUpdater == null)
        {
            LogError("no Conveyor Mesh Updater. Attach this to the Conveyor prefab root.");
            return;
        }

        var points = BuildRectanglePoints();

        Undo.RecordObjects(new Object[] { _splineComputer, this }, BuildUndoName);

        // Set before SetPoints/RebuildImmediate so the rebuild evaluates using the chosen type from
        // the start.
        _splineComputer.type = ResolveSplineType();
        _splineComputer.SetPoints(points);
        _splineComputer.Close();
        _splineComputer.RebuildImmediate();
        MarkDirty(_splineComputer);

        // UpdateMesh reads the spline's resulting length/shape, so it runs only after the spline above
        // has fully settled. It records its own Undo targets (belt mesh, collider mesh), so nothing
        // more needs recording here.
        _meshUpdater.ResolveReferences();
        _meshUpdater.UpdateMesh();

        Log($"built a {_width:0.###} x {_height:0.###} {_splineType} rectangle from {points.Length} " +
            "points.");
    }

    // ── Point generation ────────────────────────────────────────────────────────────────────

    private Spline.Type ResolveSplineType() => _splineType switch
    {
        ConveyorSplineType.Linear => Spline.Type.Linear,
        ConveyorSplineType.CatmullRom => Spline.Type.CatmullRom,
        ConveyorSplineType.BSpline => Spline.Type.BSpline,
        _ => Spline.Type.Linear,
    };

    // Four corners in this transform's local XZ plane, centred on the origin, wound clockwise seen
    // from above (matches the convention used elsewhere on the conveyor: local +Z is "forward"), with
    // Points Per Side extra evenly-spaced points inserted along each edge. Subdividing is what lets
    // CatmullRom/BSpline track the straight edges instead of bowing or ovalling — see the class
    // summary for why it can never make BSpline's corners mathematically sharp.
    private SplinePoint[] BuildRectanglePoints()
    {
        var w = _width * 0.5f;
        var h = _height * 0.5f;

        var corners = new[]
        {
            new Vector3(w, 0f, h),
            new Vector3(w, 0f, -h),
            new Vector3(-w, 0f, -h),
            new Vector3(-w, 0f, h),
        };

        var perSide = Mathf.Max(1, _pointsPerSide);
        var local = new List<Vector3>(corners.Length * perSide);

        for (var i = 0; i < corners.Length; i++)
        {
            var start = corners[i];
            var end = corners[(i + 1) % corners.Length];

            // Each corner is added once, by the edge that leads into it, so it is never duplicated
            // between neighbouring edges.
            local.Add(start);
            for (var j = 1; j < perSide; j++)
                local.Add(Vector3.Lerp(start, end, (float)j / perSide));
        }

        var points = new SplinePoint[local.Count];
        for (var i = 0; i < local.Count; i++)
        {
            // Built in local space and handed over in world space: SetPoints pulls it back into
            // whichever space the computer is set to, so the computer's own Space setting is read but
            // never overwritten.
            points[i] = new SplinePoint(transform.TransformPoint(local[i]));
        }

        return points;
    }

    // ── Validation and plumbing ─────────────────────────────────────────────────────────────

    private bool CanEdit()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;

        LogError("cannot build the rectangle in play mode. Exit play mode and try again.");
        return false;
    }

    private bool ValidateDimensions()
    {
        if (_width > 0f && _height > 0f) return true;

        LogError($"Width {_width} x Height {_height}; both must be greater than 0.");
        return false;
    }

    private static void MarkDirty(Object target)
    {
        if (target == null) return;

        EditorUtility.SetDirty(target);

        if (target is Component component && component.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
    }

    private void Log(string message) => Debug.Log($"<b>Conveyor Rectangle</b>: {message}", this);
    private void LogError(string message) => Debug.LogError($"<b>Conveyor Rectangle</b>: {message}", this);

#endif
}

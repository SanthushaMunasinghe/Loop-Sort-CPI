using System.Collections.Generic;
using Dreamteck.Splines;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Editor-only authoring tool that lays an arbitrary closed path onto the conveyor's spline from a
/// hand-placed set of waypoint transforms, and drags the rest of the conveyor onto it in one press.
///
/// <para><b>What it does.</b> <c>Build Path</c> replaces the spline's points with a closed loop
/// passing through <see cref="_pathParent"/>'s direct children, in sibling order, using each child's
/// exact world position, sets the spline's interpolation to <see cref="_splineType"/>, then hands off
/// to <see cref="ConveyorMeshUpdater"/> to refit the belt mesh, the collider mesh, and re-space
/// whatever slots and arrows already exist. This component never touches the mesh, collider, slots
/// or arrows itself — that is entirely <see cref="ConveyorMeshUpdater"/>'s job, done the same way
/// pressing its own Update Mesh button would.</para>
///
/// <para><b>Path Parent is manual, not auto-filled.</b> Unlike the Spline Computer/Mesh Updater
/// references, <see cref="_pathParent"/> is not this GameObject and cannot be guessed, so it is never
/// touched by <see cref="ResolveReferences"/> — assign it once by hand to whatever object holds the
/// ordered waypoint children.</para>
///
/// <para><b>Spline type and subdivisions.</b> Linear passes exactly through every waypoint with sharp
/// corners. CatmullRom and BSpline are smoother but do not pass through a waypoint the way Linear
/// does: CatmullRom's tangents bow near each waypoint, and BSpline treats every point as a control
/// point rather than a point the curve passes through. <see cref="_subdivisionsPerSegment"/> inserts
/// extra evenly-spaced points between each pair of consecutive waypoints (and between the last
/// waypoint and the first, since the path is closed), which straightens CatmullRom's bowed edges and
/// pulls BSpline's curve tighter toward the waypoints the higher it goes — but for BSpline this is a
/// tightening, not an exact fit. 0 inserts none, so each waypoint becomes exactly one spline point.</para>
///
/// <para><b>Up / banking.</b> Each spline point's normal (which decides which way the belt's
/// cross-section faces — see <see cref="Dreamteck.Splines.SplineSample.right"/>) is <i>not</i> a flat
/// world-up like <see cref="ConveyorRectangleBuilder"/> can get away with. A path that climbs, banks,
/// or loops (roller-coaster style) will have stretches where the direction of travel is itself
/// pointing straight up or down, and a fixed world-up normal there is parallel to the direction of
/// travel — the exact degenerate case that makes the belt's width flip and twist unpredictably.
/// Instead, <see cref="BuildPathPoints"/> propagates a rotation-minimizing frame around the whole
/// closed loop, seeded from the <i>first</i> waypoint's own local up, so the belt's up only ever
/// turns gradually from one waypoint to the next — including flipping upside-down relative to world
/// space through the top of a loop, which is correct there, just never abrupt. Each waypoint's own
/// <c>transform.up</c> is then blended in as a banking hint by <see cref="_bankBlend"/>, so rotating a
/// waypoint still tilts the belt at that point without reintroducing sudden snaps.</para>
///
/// <para><b>Editor only.</b> Every method lives inside <c>#if UNITY_EDITOR</c> and there is no
/// Awake/Start/Update, so nothing here can execute in a player build. The serialized fields
/// deliberately sit outside the guard so the component's serialized layout is identical in the editor
/// and in a build.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SplineComputer))]
[RequireComponent(typeof(ConveyorMeshUpdater))]
public sealed class ConveyorPathBuilder : MonoBehaviour
{
    // See the class summary for why these sit outside the UNITY_EDITOR guard.
#pragma warning disable CS0169, CS0414, CS0649

    [Header("References (auto-filled; drag only if this is not on the conveyor)")]
    [Tooltip("The conveyor's path. Auto-filled from this GameObject.")]
    [SerializeField] private SplineComputer _splineComputer;

    [Tooltip("Refits the belt mesh, collider, slots and arrows onto the path once it is built. " +
             "Auto-filled from this GameObject; added automatically if missing.")]
    [SerializeField] private ConveyorMeshUpdater _meshUpdater;

    [Header("Path")]
    [Tooltip("Parent whose direct children, in sibling order, become the closed path's points, " +
             "using each child's exact world position. Assign manually - this is a separate " +
             "object, not auto-filled.")]
    [SerializeField] private Transform _pathParent;

    [Tooltip("Interpolation to apply when the path is written. Linear passes exactly through every " +
             "waypoint with sharp corners. CatmullRom and BSpline are smoother but need " +
             "Subdivisions Per Segment raised to track the shape - and BSpline's corners stay " +
             "visibly rounded no matter how high that goes, since it treats points as control " +
             "points rather than passing through them.")]
    [SerializeField] private ConveyorSplineType _splineType = ConveyorSplineType.BSpline;

    [Tooltip("How many evenly-spaced extra points to insert between each pair of consecutive " +
             "waypoints (and between the last waypoint and the first, since the path is closed). " +
             "0 inserts none - each waypoint becomes exactly one spline point. Raise this to " +
             "straighten CatmullRom's bowed edges or tighten BSpline's curve toward the waypoints; " +
             "for Linear it only adds points, it does not change the shape, which is already exact.")]
    [Min(0)]
    [SerializeField] private int _subdivisionsPerSegment = 0;

    [Header("Up / Banking")]
    [Tooltip("How strongly each waypoint's own local Y (its transform's up arrow) pulls the belt's " +
             "cross-section toward that orientation there, versus a smoothly propagated up carried " +
             "over from the previous waypoint. 0 ignores waypoint rotation entirely and only " +
             "propagates from the first waypoint's up - safest, but rotating waypoints does " +
             "nothing. 1 snaps fully to each waypoint's own up - full control, but a waypoint " +
             "rotated very differently from its neighbours can twist the belt abruptly. 0.5 blends " +
             "the two, which is a good default for hand-placed waypoints.")]
    [Range(0f, 1f)]
    [SerializeField] private float _bankBlend = 0.5f;

#pragma warning restore CS0169, CS0414, CS0649

#if UNITY_EDITOR

    private const string BuildUndoName = "Build Conveyor Path";
    private const int MinWaypoints = 3;

    // Fills the spline/mesh-updater references in when the component is first added. Path Parent is
    // deliberately left untouched - see the class summary.
    private void Reset()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Fills in the Spline Computer / Mesh Updater references if still null. Safe to call
    /// repeatedly; never overwrites something you assigned by hand. Never touches Path Parent,
    /// which is always assigned manually.
    /// </summary>
    public void ResolveReferences()
    {
        if (_splineComputer == null) _splineComputer = GetComponent<SplineComputer>();
        if (_meshUpdater == null) _meshUpdater = GetComponent<ConveyorMeshUpdater>();
    }

    /// <summary>
    /// Replaces the spline's points with a closed loop through Path Parent's children, then hands
    /// the rest of the conveyor over to <see cref="ConveyorMeshUpdater"/>.
    /// </summary>
    public void BuildPath()
    {
        if (!CanEdit()) return;
        if (!ValidatePathParent()) return;

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

        var points = BuildPathPoints();

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

        Log($"built a {_pathParent.childCount}-waypoint path ({points.Length} spline points, " +
            $"{_splineType}) from '{_pathParent.name}'.");
    }

    // ── Point generation ────────────────────────────────────────────────────────────────────

    private Spline.Type ResolveSplineType() => _splineType switch
    {
        ConveyorSplineType.Linear => Spline.Type.Linear,
        ConveyorSplineType.CatmullRom => Spline.Type.CatmullRom,
        ConveyorSplineType.BSpline => Spline.Type.BSpline,
        _ => Spline.Type.Linear,
    };

    // Path Parent's direct children, in sibling order, at their exact world positions, with
    // Subdivisions Per Segment extra evenly-spaced points inserted along each segment (wrapping from
    // the last child back to the first, since the path is closed). Subdividing is what lets
    // CatmullRom/BSpline track the waypoints instead of bowing or rounding them off — see the class
    // summary for why it can never make BSpline's corners mathematically sharp.
    private SplinePoint[] BuildPathPoints()
    {
        var childCount = _pathParent.childCount;
        var world = new Vector3[childCount];
        var hintUps = new Vector3[childCount];
        for (var i = 0; i < childCount; i++)
        {
            var child = _pathParent.GetChild(i);
            world[i] = child.position;
            hintUps[i] = child.up;
        }

        var tangents = ComputeTangents(world);
        var waypointUps = ComputeWaypointUps(world, tangents, hintUps);

        var result = new List<Vector3>(childCount * (_subdivisionsPerSegment + 1));
        var resultUps = new List<Vector3>(childCount * (_subdivisionsPerSegment + 1));
        for (var i = 0; i < childCount; i++)
        {
            var start = world[i];
            var end = world[(i + 1) % childCount];
            var startUp = waypointUps[i];
            var endUp = waypointUps[(i + 1) % childCount];

            // Each waypoint is added once, by the segment that leads into it, so it is never
            // duplicated between neighbouring segments. Its up is carried along the same way
            // Dreamteck itself blends point normals (SplinePoint.Lerp), so a subdivided segment looks
            // identical to the un-subdivided one, just with more points on it.
            result.Add(start);
            resultUps.Add(startUp);
            for (var j = 1; j <= _subdivisionsPerSegment; j++)
            {
                var t = (float)j / (_subdivisionsPerSegment + 1);
                result.Add(Vector3.Lerp(start, end, t));
                resultUps.Add(Vector3.Slerp(startUp, endUp, t));
            }
        }

        var points = new SplinePoint[result.Count];
        for (var i = 0; i < result.Count; i++)
        {
            // Already world space, matching SetPoints' default space - the computer's own Space
            // setting is read but never overwritten.
            points[i] = new SplinePoint(result[i]) { normal = resultUps[i] };
        }

        return points;
    }

    // ── Up / banking ─────────────────────────────────────────────────────────────────────────

    // Central-difference direction of travel at each waypoint, wrapping around the closed loop.
    // Only used to keep the "up" propagation below perpendicular to the path - never fed back into
    // the spline's own tangent handles, which SplinePoint(Vector3) already leaves at zero length
    // (fine: Linear/CatmullRom/BSpline all shape themselves from point positions, not handles).
    private static Vector3[] ComputeTangents(Vector3[] positions)
    {
        var n = positions.Length;
        var tangents = new Vector3[n];
        for (var i = 0; i < n; i++)
        {
            var prev = positions[(i - 1 + n) % n];
            var next = positions[(i + 1) % n];
            var t = next - prev;
            tangents[i] = t.sqrMagnitude > 1e-10f ? t.normalized : Vector3.forward;
        }

        return tangents;
    }

    private static Vector3 ProjectPerpendicular(Vector3 v, Vector3 tangent) =>
        v - Vector3.Dot(v, tangent) * tangent;

    // A waypoint's own up, flattened onto the plane perpendicular to its tangent. Falls back to
    // whichever world axis is least parallel to the tangent when the waypoint's up happens to point
    // along the direction of travel there (the exact case that used to degenerate the belt's width).
    private static Vector3 ResolveUpHint(Vector3 hintUp, Vector3 tangent)
    {
        var projected = ProjectPerpendicular(hintUp, tangent);
        if (projected.sqrMagnitude > 0.0001f) return projected.normalized;

        var fallback = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) < 0.99f ? Vector3.up : Vector3.forward;
        return ProjectPerpendicular(fallback, tangent).normalized;
    }

    // Double reflection method (Wang, Jüttler, Zheng, Liu 2008): transports an "up" vector known to
    // be perpendicular to t0 at p0 into one perpendicular to t1 at p1, rotating by exactly as much as
    // the frame itself turned - no more. Unlike recomputing "up" independently at each point (e.g.
    // Cross(worldUp, tangent)), this never degenerates when the tangent points straight up/down, and
    // never flips 180° between neighbours, which is what a roller-coaster-style vertical loop needs.
    private static Vector3 TransportUp(Vector3 p0, Vector3 t0, Vector3 r0, Vector3 p1, Vector3 t1)
    {
        var v1 = p1 - p0;
        var c1 = Vector3.Dot(v1, v1);
        if (c1 < 1e-10f) return r0;

        var rL = r0 - (2f / c1) * Vector3.Dot(v1, r0) * v1;
        var tL = t0 - (2f / c1) * Vector3.Dot(v1, t0) * v1;

        var v2 = t1 - tL;
        var c2 = Vector3.Dot(v2, v2);
        if (c2 < 1e-10f) return rL.normalized;

        var r1 = rL - (2f / c2) * Vector3.Dot(v2, rL) * v2;
        return r1.normalized;
    }

    // One stable "up" per waypoint: propagated all the way around the closed loop from the first
    // waypoint's own up (the seed - "initial point up as initial up"), corrected so the loop closes
    // without a seam, then blended toward each waypoint's own local up by Bank Blend. See the class
    // summary's Up / banking section for why this is needed instead of a fixed world-up normal.
    private Vector3[] ComputeWaypointUps(Vector3[] positions, Vector3[] tangents, Vector3[] hintUps)
    {
        var n = positions.Length;
        var seed = ResolveUpHint(hintUps[0], tangents[0]);

        var propagated = new Vector3[n];
        propagated[0] = seed;
        for (var i = 1; i < n; i++)
            propagated[i] = TransportUp(positions[i - 1], tangents[i - 1], propagated[i - 1], positions[i], tangents[i]);

        // Propagating one more step (last waypoint back to the first) should land back on the seed;
        // it generally won't exactly, so the mismatch is spread evenly across every waypoint instead
        // of dumped entirely into one seam at the point the loop closes.
        var wrapped = TransportUp(positions[n - 1], tangents[n - 1], propagated[n - 1], positions[0], tangents[0]);
        var mismatch = Vector3.SignedAngle(seed, wrapped, tangents[0]);
        for (var i = 0; i < n; i++)
        {
            var correctionAngle = -mismatch * i / n;
            propagated[i] = Quaternion.AngleAxis(correctionAngle, tangents[i]) * propagated[i];
        }

        var result = new Vector3[n];
        for (var i = 0; i < n; i++)
        {
            var hint = ResolveUpHint(hintUps[i], tangents[i]);
            var blended = Vector3.Slerp(propagated[i], hint, _bankBlend);
            result[i] = ProjectPerpendicular(blended, tangents[i]).normalized;
        }

        return result;
    }

    // ── Validation and plumbing ─────────────────────────────────────────────────────────────

    private bool CanEdit()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;

        LogError("cannot build the path in play mode. Exit play mode and try again.");
        return false;
    }

    private bool ValidatePathParent()
    {
        if (_pathParent == null)
        {
            LogError("no Path Parent assigned. Assign a transform with at least " +
                     $"{MinWaypoints} child waypoints.");
            return false;
        }

        if (_pathParent.childCount < MinWaypoints)
        {
            LogError($"Path Parent '{_pathParent.name}' has {_pathParent.childCount} child(ren); " +
                     $"at least {MinWaypoints} are needed to close a loop.");
            return false;
        }

        return true;
    }

    private static void MarkDirty(Object target)
    {
        if (target == null) return;

        EditorUtility.SetDirty(target);

        if (target is Component component && component.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
    }

    private void Log(string message) => Debug.Log($"<b>Conveyor Path</b>: {message}", this);
    private void LogError(string message) => Debug.LogError($"<b>Conveyor Path</b>: {message}", this);

#endif
}

using System.Collections.Generic;
using Dreamteck.Splines;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Editor-only authoring tool that re-shapes the conveyor of an already generated sandbox level.
///
/// <para><b>What it is for.</b> <c>LevelSandboxGenerator</c> builds the conveyor from a
/// Splines.json row, which means the belt's path is whatever the sheet says. Attach this to the
/// generated <c>Conveyor</c> GameObject (the prefab instance parent, the one carrying
/// <see cref="SplineComputer"/> and <see cref="SplineMesh"/>) and the path becomes something you can
/// author from numbers instead: a regular polygon with rounded corners. Pressing <c>Rebuild
/// Conveyor</c> then drags the rest of the conveyor onto the new path — belt mesh, slots, arrows and
/// the collider mesh — and re-bakes the length-derived values on <see cref="LevelSandbox"/> so the
/// level still plays.</para>
///
/// <para><b>Four buttons.</b>
/// <list type="number">
/// <item><c>Create Shape</c> — replaces the spline's points with a fresh loop laid flat in the
/// conveyor's local XZ plane, and closes the spline. See <b>Shapes and sizing</b> below.</item>
/// <item><c>Bevel Shape</c> — rounds the corners of the shape <i>this component generated</i>, and
/// refuses if that shape has already been beveled.</item>
/// <item><c>Bevel Existing Points</c> — ignores the shape tracking and rounds whatever points are
/// currently on the spline, so it also works on a path generated from a Splines.json row. Always
/// runs.</item>
/// <item><c>Rebuild Conveyor</c> — the one that matters: re-fits mesh, slots, arrows and collider to
/// the current spline and writes the new speed / collision distance back to the sandbox.</item>
/// </list>
/// The first three only touch the spline; nothing downstream moves until you press the fourth. That
/// separation is deliberate — shaping is cheap and iterative, rebuilding is not.</para>
///
/// <para><b>Shapes and sizing.</b> Two independent choices. <see cref="ShapeKind"/> picks a regular
/// polygon (real corners, which <c>Bevel Shape</c> can round) or an ellipse (a smooth ring).
/// <see cref="SizeMode"/> picks how it is measured: <c>Radius</c> sizes it uniformly, so a polygon
/// gets that circumradius and an ellipse becomes a circle of that radius; <c>Width And Height</c>
/// sizes the two ground axes separately, which is what turns the circle into an oval. The two size
/// modes are alternatives and the inspector only shows the one in use.
///
/// Width and Height are the <i>real footprint</i>: the finished shape measures exactly that far
/// across the ground, whatever its kind and however it is rotated, because the fit is applied after
/// the rotation. A four-sided polygon at 45° therefore comes out as a true Width by Height
/// rectangle, which is where racetrack shapes come from. Note that Height here is a ground axis —
/// <see cref="_elevation"/> is what lifts the loop.</para>
///
/// <para><b>Why the level stays compatible.</b> Slot count is <c>GroupSlotCount * SlotCount</c>,
/// both of which come from the <see cref="CarrierBlockArgs"/> the generator baked onto
/// <see cref="LevelSandbox"/>. This tool deliberately does <b>not</b> recompute those args from the
/// new spline length, so the block grid, the carriers and their blocks are left exactly as generated
/// and the slot count does not move. Only the length-derived values change — conveyor speed, slot
/// collision distance, the slot percents and the arrow count — and those are exactly what
/// <see cref="ConveyorSlotCreateSystem"/> reads back off the sandbox at play time.</para>
///
/// <para><b>What it does not touch.</b> Carriers, blocks, the Triggers root and the End Meshes root.
/// A closed loop has no ends, so end meshes left over from an open generated path are reported
/// rather than deleted — they carry <c>BlockTrigger</c>, and triggers are yours to place.</para>
///
/// <para><b>How the bevel is sized.</b> The rounding is bounded by an absolute
/// <see cref="_bevelMaxDistance"/> measured along the path in world units, <i>not</i> by a fraction
/// of the edge. A corner is set back by that distance along each of its two edges and the sharp
/// vertex is replaced by exactly <c>2 * <see cref="_bevelCount"/></c> points sampled along a
/// quadratic Bézier through it. An edge longer than twice the max distance therefore keeps a straight
/// middle section — that is what makes this corner rounding rather than rounding the whole shape into
/// a circle. A corner whose edges are too short clamps to half the shorter one, so neighbouring
/// bevels can meet but never cross.</para>
///
/// <para><b>Spline type matters.</b> Dreamteck's BSpline (what the Conveyor prefab ships with) treats
/// points as control points and does <i>not</i> pass through them, so a polygon comes out noticeably
/// smaller and rounder than <see cref="_radius"/> suggests and the bevel has little left to do. Use
/// <see cref="_splineType"/> to switch to Linear (the shape exactly as authored) or CatmullRom (a
/// smooth curve through every point). <c>Keep Current</c> leaves the prefab's choice alone and warns
/// instead.</para>
///
/// <para><b>Editor only.</b> Every method lives inside <c>#if UNITY_EDITOR</c> and there is no
/// Awake/Start/Update, so nothing here can execute in a player build. The serialized fields
/// deliberately sit outside the guard so the component's serialized layout is identical in the editor
/// and in a build — leaving the component on the prefab costs a build nothing but the fields.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class ConveyorShapeBuilder : MonoBehaviour
{
    /// <summary>
    /// Spline interpolation to apply when a shape is written. Mirrors <see cref="Spline.Type"/> with
    /// an extra "leave it as authored" entry, so this tool never silently changes the belt's look.
    /// </summary>
    public enum SplineTypeOverride
    {
        Keep = 0,
        Linear = 1,
        CatmullRom = 2,
        BSpline = 3,
    }

    /// <summary>What kind of loop <see cref="CreateShape"/> writes. Ellipse covers both a circle and
    /// an oval — which one you get is decided by the size, not by a separate kind.</summary>
    public enum ShapeKind
    {
        Polygon = 0,
        Ellipse = 1,
    }

    /// <summary>
    /// How the shape is sized. The two are alternatives, not a combination — the inspector shows
    /// whichever one is selected and hides the other.
    /// </summary>
    public enum SizeMode
    {
        Radius = 0,
        WidthAndHeight = 1,
    }

    // Read only from the editor-only methods below and written only by the inspector, so the
    // compiler sees them as unused (CS0169), write-only (CS0414) or never assigned (CS0649) — the
    // last one in the editor too, since Unity's serializer is what assigns them. That is all
    // expected: keeping the fields out of the guard is what makes the serialized layout identical in
    // the editor and in a build.
#pragma warning disable CS0169, CS0414, CS0649

    [Header("References (auto-filled; drag only if this is not on the conveyor)")]
    [Tooltip("The conveyor's path. Auto-filled from this GameObject.")]
    [SerializeField] private SplineComputer _splineComputer;

    [Tooltip("The belt mesh that rides the spline. Auto-filled from this GameObject.")]
    [SerializeField] private SplineMesh _splineMesh;

    [Tooltip("The Conveyor component. Auto-filled from this GameObject; only used to re-bake the " +
             "sandbox reference.")]
    [SerializeField] private Conveyor _conveyor;

    [Tooltip("The sandbox that owns this level. Auto-found in the scene. Supplies the baked " +
             "CarrierBlockArgs the slot layout is derived from, and receives the new speed and " +
             "collision distance.")]
    [SerializeField] private LevelSandbox _levelSandbox;

    [Tooltip("Auto-resolved from the SceneScope Data list. Slot / arrow / collider prefabs and the " +
             "speed and collision distance the layout is computed from.")]
    [SerializeField] private ConveyorConfig _conveyorConfig;

    [Tooltip("Auto-resolved from the SceneScope Data list. Needed to turn the baked block grid back " +
             "into a slot group size.")]
    [SerializeField] private CarrierConfig _carrierConfig;

    [Header("Shape")]
    [Tooltip("Polygon writes a regular many-sided loop with real corners to bevel. Ellipse writes a " +
             "smooth ring — a circle when it is sized by Radius or by an equal Width and Height, an " +
             "oval otherwise.")]
    [SerializeField] private ShapeKind _shapeKind = ShapeKind.Polygon;

    [Tooltip("Radius sizes the shape uniformly. Width And Height sizes the two ground axes " +
             "separately, which is what turns a circle into an oval. They are alternatives — only " +
             "the selected one is used, and the inspector hides the other.")]
    [SerializeField] private SizeMode _sizeMode = SizeMode.Radius;

    [Tooltip("Number of sides of the generated regular polygon.")]
    [Min(3)]
    [SerializeField] private int _sideCount = 6;

    [Tooltip("How many points the ring is built from. These are path points, not corners: raise it " +
             "for a smoother outline under a Linear spline, though CatmullRom needs far fewer for " +
             "the same result.")]
    [Min(3)]
    [SerializeField] private int _segmentCount = 24;

    [Tooltip("Distance from the centre to each point, in the conveyor's local units — the " +
             "circumradius of a polygon, or simply the radius of a circle. With a BSpline the belt " +
             "itself will sit well inside it, see Spline Type.")]
    [Min(0.0001f)]
    [SerializeField] private float _radius = 10f;

    [Tooltip("Size along the conveyor's local X, in local units. This is the real footprint: the " +
             "finished shape measures exactly this far across, whatever its kind or rotation.")]
    [Min(0.0001f)]
    [SerializeField] private float _width = 20f;

    [Tooltip("Size along the conveyor's local Z, in local units — the ground axis, not elevation. " +
             "Like Width this is the real footprint, so Width 20 / Height 12 is a shape that " +
             "measures exactly 20 by 12 on the ground.")]
    [Min(0.0001f)]
    [SerializeField] private float _height = 12f;

    [Tooltip("Rotates the shape about its centre, in degrees. At 0 the first point sits along the " +
             "conveyor's local +Z. Width and Height are measured after this, so a 4-sided polygon " +
             "at 45 comes out as a true Width by Height rectangle.")]
    [SerializeField] private float _rotationOffset;

    [Tooltip("Lifts the whole loop above the conveyor's own origin, in local units. Leave at 0 to " +
             "match what the generator produces from a flat Splines.json row.")]
    [SerializeField] private float _elevation;

    [Tooltip("Winding of the generated loop, seen from above. This is the direction blocks will " +
             "travel; flip it if the belt runs the wrong way past the carriers.")]
    [SerializeField] private bool _clockwise = true;

    [Tooltip("Interpolation to apply when a shape is written. Linear gives the polygon exactly as " +
             "authored, CatmullRom a smooth curve through every point, BSpline a curve that only " +
             "approaches them. Keep leaves whatever the prefab was authored with.")]
    [SerializeField] private SplineTypeOverride _splineType = SplineTypeOverride.Keep;

    [Header("Bevel")]
    [Tooltip("How far the rounding may reach from a corner, measured along the path in world units. " +
             "An edge longer than twice this keeps a straight middle section. A corner whose edges " +
             "are shorter clamps to half the shorter one so neighbouring bevels cannot overlap.")]
    [Min(0.0001f)]
    [SerializeField] private float _bevelMaxDistance = 2f;

    [Tooltip("Points per SIDE of each corner. A beveled corner always becomes 2x this many points: " +
             "1 = 2 points (a flat chamfer), 4 = 8 points. The outermost pair sits exactly at Bevel " +
             "Max Distance.")]
    [Min(1)]
    [SerializeField] private int _bevelCount = 3;

    [Tooltip("Corners turning by less than this many degrees are left untouched. Keeps the " +
             "near-straight points of a subdivided sheet path from being needlessly cut up. 0 bevels " +
             "every point.")]
    [Range(0f, 180f)]
    [SerializeField] private float _minCornerAngle = 5f;

    [Tooltip("Maximum spline points any button may write. Each beveled corner becomes 2x Bevel " +
             "Count points and that compounds across repeated bevels, so a button that would exceed " +
             "this refuses and reports the number instead of building it. Every point costs Sample " +
             "Rate samples at run time, so this is a real budget, not a formality.")]
    [Min(3)]
    [SerializeField] private int _maxPoints = 256;

    [Header("Rebuild")]
    [Tooltip("Re-fit the belt mesh to the new path.")]
    [SerializeField] private bool _rebuildMesh = true;

    [Tooltip("Re-space the conveyor slots and re-bake the speed and collision distance onto the " +
             "sandbox. Turning this off leaves the level playing at the old spline's speed.")]
    [SerializeField] private bool _rebuildSlots = true;

    [Tooltip("Re-space the arrows. Their count is derived from length, so they are destroyed and " +
             "recreated rather than moved.")]
    [SerializeField] private bool _rebuildArrows = true;

    [Tooltip("Re-fit the conveyor collider mesh. Without this, blocks fall through the new path.")]
    [SerializeField] private bool _rebuildCollider = true;

    [Tooltip("Belt mesh repetitions per world unit of spline. Leave at 0 to keep the count the " +
             "prefab was authored with, which stretches the same number of tiles over the new " +
             "length. Set it to hold tile size constant instead — the log reports the density the " +
             "current count works out to, so you can copy that number in.")]
    [Min(0f)]
    [SerializeField] private float _meshTilePerDistance;

    // ── Shape tracking (persisted; written by the buttons — do not hand-edit) ───────────────
    [SerializeField, HideInInspector] private bool _shapeGenerated;
    [SerializeField, HideInInspector] private bool _shapeBeveled;
    [SerializeField, HideInInspector] private bool _shapeIsSmooth;

#pragma warning restore CS0169, CS0414, CS0649

#if UNITY_EDITOR

    private const string ShapeUndoName = "Shape Conveyor";
    private const string RebuildUndoName = "Rebuild Conveyor";
    private const string SlotsRootName = "Slots";
    private const string ArrowsRootName = "Arrows";
    private const string ColliderName = "Collider";
    private const string EndMeshRootName = "End Meshes";

    public bool ShapeGenerated => _shapeGenerated;
    public bool ShapeBeveled => _shapeBeveled;
    public bool ShapeIsSmooth => _shapeIsSmooth;

    // Fills the references in when the component is first added, so dropping it on the generated
    // conveyor is all the setup there is.
    private void Reset()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Fills in every reference that is still null. Safe to call repeatedly; never overwrites
    /// something you assigned by hand. Public so the inspector can re-run it after the level is
    /// regenerated.
    /// </summary>
    public void ResolveReferences()
    {
        if (_splineComputer == null) _splineComputer = GetComponent<SplineComputer>();
        if (_splineMesh == null) _splineMesh = GetComponent<SplineMesh>();
        if (_conveyor == null) _conveyor = GetComponent<Conveyor>();
        if (_levelSandbox == null) _levelSandbox = FindInScene<LevelSandbox>();

        if (_conveyorConfig != null && _carrierConfig != null) return;

        // The SceneScope Data list is the same source of truth LevelSandboxEditor uses, and it is
        // private serialized state, so it is read the same way: through SerializedObject.
        var scope = FindInScene<SceneScope>();
        if (scope == null) return;

        var so = new SerializedObject(scope);
        var dataProperty = so.FindProperty("_data");
        if (dataProperty == null) return;

        for (var i = 0; i < dataProperty.arraySize; i++)
        {
            switch (dataProperty.GetArrayElementAtIndex(i).objectReferenceValue)
            {
                case ConveyorConfig x when _conveyorConfig == null: _conveyorConfig = x; break;
                case CarrierConfig x when _carrierConfig == null: _carrierConfig = x; break;
            }
        }
    }

    private T FindInScene<T>() where T : Component
    {
        var scene = gameObject.scene;
        if (!scene.IsValid()) return null;

        foreach (var root in scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }

        return null;
    }

    // ── Buttons: shape ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces the spline's points with a freshly generated loop — a regular polygon or a smooth
    /// ring, sized by radius or by width and height — and marks it as a fresh, un-beveled shape so
    /// <see cref="BevelShape"/> will act on it. Only the spline moves; press Rebuild Conveyor to
    /// bring the mesh, slots, arrows and collider along.
    /// </summary>
    public void CreateShape()
    {
        ResolveReferences();

        if (!CanEdit()) return;
        if (_splineComputer == null)
        {
            LogError("no Spline Computer. Attach this to the generated Conveyor GameObject.");
            return;
        }

        if (!ValidateShapeSettings(out var outputCount)) return;

        // Three stages, and the order is what makes Width and Height mean the footprint. Rotation is
        // baked into the unit outline BEFORE anything is measured, so the fit always squares up with
        // the ground axes — which is exactly what turns a 4-gon at 45° into a true Width x Height
        // rectangle rather than a diamond inside one.
        var isEllipse = _shapeKind == ShapeKind.Ellipse;
        var outline = BuildUnitOutline(isEllipse ? EllipseSampleCount : _sideCount);
        FitToSize(outline);

        // A polygon must keep its uniform ANGLE spacing — that is what makes it regular — so only
        // the ring is re-spaced by arc length. See ResampleClosedLoop for why that matters.
        var local = isEllipse ? ResampleClosedLoop(outline, _segmentCount) : outline;

        var points = new SplinePoint[local.Length];
        for (var i = 0; i < local.Length; i++)
        {
            // Built in this transform's local space and handed over in world space: SetPoints pulls
            // it back into whichever space the computer is set to, exactly as LevelGeometry does, so
            // the computer's own Space setting is read but never overwritten.
            points[i] = new SplinePoint(transform.TransformPoint(local[i]));
        }

        Undo.RecordObjects(new Object[] { _splineComputer, this }, ShapeUndoName);

        ApplySplineType();
        _splineComputer.SetPoints(points);
        _splineComputer.Close();
        _splineComputer.RebuildImmediate();

        _shapeGenerated = true;
        _shapeBeveled = false;
        _shapeIsSmooth = isEllipse;

        // Both, not just the computer: the tracking flags are serialized state on this component, so
        // dirtying only the spline would let them be lost on save and leave Bevel Shape refusing to
        // act on a shape it did in fact create.
        MarkDirty(_splineComputer);
        MarkDirty(this);

        var size = MeasureFootprint(local);
        var what = isEllipse
            ? $"an oval from {outputCount} segments"
            : $"a {outputCount}-sided loop";

        Log($"created {what}, {size.x:F1} x {size.y:F1} on the ground, length " +
            $"{_splineComputer.CalculateLength():F1}. Press Rebuild Conveyor to move the rest of the " +
            "conveyor onto it.");

        WarnIfTypeHidesTheShape();
        WarnIfRingIsCoarse();
    }

    // ── Shape generation ────────────────────────────────────────────────────────────────────

    // How finely a ring is traced before it is fitted and re-spaced. High enough that the arc-length
    // resample below has a smooth curve to walk along, and it never reaches the spline: this is an
    // intermediate, so it costs nothing at run time and is not checked against Max Points.
    private const int EllipseSampleCount = 512;

    private bool ValidateShapeSettings(out int outputCount)
    {
        var isEllipse = _shapeKind == ShapeKind.Ellipse;
        outputCount = isEllipse ? _segmentCount : _sideCount;
        var label = isEllipse ? "Segment Count" : "Side Count";

        if (outputCount < 3)
        {
            LogError($"{label} is {outputCount}; a closed loop needs at least 3.");
            return false;
        }

        if (outputCount > _maxPoints)
        {
            LogError($"{label} {outputCount} exceeds the Max Points budget of {_maxPoints}. Raise " +
                     $"Max Points or lower {label}.");
            return false;
        }

        if (_sizeMode == SizeMode.Radius && _radius <= 0f)
        {
            LogError($"Radius is {_radius}; it must be greater than 0.");
            return false;
        }

        if (_sizeMode == SizeMode.WidthAndHeight && (_width <= 0f || _height <= 0f))
        {
            LogError($"Width {_width} x Height {_height}; both must be greater than 0. Height here " +
                     "is the ground axis — use Elevation to lift the loop.");
            return false;
        }

        return true;
    }

    // Traces the shape on a UNIT circle in this transform's local XZ plane, with Rotation Offset and
    // winding already applied. Size is deliberately not applied yet — see CreateShape.
    private Vector3[] BuildUnitOutline(int count)
    {
        var step = 360f / count;
        var outline = new Vector3[count];

        for (var i = 0; i < count; i++)
        {
            // Measured from local +Z toward local +X, so increasing i winds clockwise seen from
            // above and Rotation Offset reads as a plain rotation of the shape.
            var index = _clockwise ? i : -i;
            var angle = (_rotationOffset + index * step) * Mathf.Deg2Rad;
            outline[i] = new Vector3(Mathf.Sin(angle), _elevation, Mathf.Cos(angle));
        }

        return outline;
    }

    // Scales the unit outline in place to the requested size.
    //
    // Radius mode scales uniformly, so the points end up exactly Radius from the centre — the
    // circumradius of a polygon, the radius of a circle. Width And Height instead measures what the
    // outline actually spans and scales each ground axis to match, so the finished shape measures
    // exactly Width by Height however many sides it has and however it is rotated. Elevation is
    // carried through untouched: it is a position, not a size.
    private void FitToSize(Vector3[] outline)
    {
        float scaleX, scaleZ;

        if (_sizeMode == SizeMode.Radius)
        {
            scaleX = scaleZ = _radius;
        }
        else
        {
            var span = MeasureFootprint(outline);

            // A unit outline of 3+ distinct points always spans both axes, so this only guards
            // against a degenerate count reaching here — in which case leave the axis alone rather
            // than dividing by zero and writing NaNs into the spline.
            scaleX = span.x > Mathf.Epsilon ? _width / span.x : 1f;
            scaleZ = span.y > Mathf.Epsilon ? _height / span.y : 1f;
        }

        for (var i = 0; i < outline.Length; i++)
        {
            outline[i].x *= scaleX;
            outline[i].z *= scaleZ;
        }
    }

    /// <summary>Axis-aligned ground size of a loop: X span in x, Z span in y.</summary>
    private static Vector2 MeasureFootprint(Vector3[] points)
    {
        if (points.Length == 0) return Vector2.zero;

        var min = new Vector2(points[0].x, points[0].z);
        var max = min;

        foreach (var point in points)
        {
            min.x = Mathf.Min(min.x, point.x);
            min.y = Mathf.Min(min.y, point.z);
            max.x = Mathf.Max(max.x, point.x);
            max.y = Mathf.Max(max.y, point.z);
        }

        return max - min;
    }

    // Walks a closed polyline and drops `count` points along it at equal arc length.
    //
    // This is what keeps an oval's points evenly spaced. Sampling an ellipse at uniform ANGLE bunches
    // points along the long flat sides and starves the tight ends, which is where curvature is
    // highest and where a shortage shows: the ends of a long racetrack belt come out visibly
    // faceted under a Linear spline while the sides are needlessly dense. Re-spacing by distance puts
    // the points where the curve actually turns.
    //
    // Run AFTER the size fit, not before, because it is the fitted shape whose arc length matters —
    // spacing computed on the unit circle would be stretched right back out of true by a non-uniform
    // Width / Height.
    private static Vector3[] ResampleClosedLoop(Vector3[] source, int count)
    {
        var n = source.Length;

        // Cumulative distance to the START of each segment, plus the total as the final entry, so a
        // walk target can be located with a single forward scan.
        var distances = new float[n + 1];
        for (var i = 0; i < n; i++)
            distances[i + 1] = distances[i] + Vector3.Distance(source[i], source[(i + 1) % n]);

        var total = distances[n];
        if (total <= Mathf.Epsilon) return source;

        var result = new Vector3[count];
        var segment = 0;

        for (var i = 0; i < count; i++)
        {
            var target = total * i / count;

            while (segment < n - 1 && distances[segment + 1] < target) segment++;

            var segmentLength = distances[segment + 1] - distances[segment];
            var t = segmentLength > Mathf.Epsilon ? (target - distances[segment]) / segmentLength : 0f;
            result[i] = Vector3.Lerp(source[segment], source[(segment + 1) % n], t);
        }

        return result;
    }

    /// <summary>
    /// Rounds the corners of the shape this component generated. Refuses on a shape it did not
    /// create, on a shape with no corners, and on the same shape twice.
    /// </summary>
    public void BevelShape()
    {
        ResolveReferences();

        if (!CanEdit()) return;
        if (_splineComputer == null)
        {
            LogError("no Spline Computer; nothing to bevel.");
            return;
        }

        if (!_shapeGenerated)
        {
            LogError("no shape has been generated yet. Press Create Shape first, or use Bevel " +
                     "Existing Points to round the path that is already on the spline.");
            return;
        }

        // Every point of a ring is a shallow, evenly spaced turn, so beveling one is not a mistake
        // the angle filter would catch — it would happily subdivide the whole thing into a slightly
        // smaller ring. Saying so beats quietly tripling the point count for no visible change.
        if (_shapeIsSmooth)
        {
            LogWarning("the generated shape is a ring, which has no corners to round — beveling it " +
                       "would only spend points reproducing the same curve. Raise Segment Count or " +
                       "use CatmullRom for a smoother outline, or press Bevel Existing Points if you " +
                       "really do want every point subdivided.");
            return;
        }

        if (_shapeBeveled)
        {
            LogWarning("this shape is already beveled; nothing to do. Press Create Shape to start " +
                       "over, or Bevel Existing Points to round it further.");
            return;
        }

        if (!TryBevelCurrentPath("Bevel Shape")) return;

        _shapeBeveled = true;
        MarkDirty(this);
    }

    /// <summary>
    /// Bevels whatever is currently on the spline. Deliberately ignores the shape tracking — it is
    /// the manual override, so it never refuses on account of the flags and never changes them, and
    /// it works just as well on a path the generator built from a Splines.json row. Safe to press
    /// repeatedly, subject to the point budget.
    /// </summary>
    public void BevelExistingPoints()
    {
        ResolveReferences();

        if (!CanEdit()) return;
        if (_splineComputer == null)
        {
            LogError("no Spline Computer; nothing to bevel.");
            return;
        }

        TryBevelCurrentPath("Bevel Existing Points");
    }

    // ── Button: rebuild ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Drags the rest of the conveyor onto the current spline: belt mesh, slots, arrows, collider,
    /// and the length-derived values on the sandbox. Everything else the generator produced —
    /// carriers, blocks, triggers, end meshes — is left alone.
    /// </summary>
    public void RebuildConveyor()
    {
        ResolveReferences();

        if (!CanEdit()) return;
        if (!ValidateForRebuild()) return;

        // Applied here as well as in Create Shape, so changing the dropdown and pressing this one
        // button re-fits everything to the new interpolation in a single press.
        ApplySplineType();

        _splineComputer.RebuildImmediate();
        var length = _splineComputer.CalculateLength();
        if (length <= 0f)
        {
            LogError("the spline has no length. Press Create Shape first.");
            return;
        }

        var summary = new List<string>();

        if (_rebuildMesh) summary.Add(RebuildMesh(length));
        if (_rebuildSlots) summary.Add(RebuildSlots());
        if (_rebuildArrows) summary.Add(RebuildArrows(length));
        if (_rebuildCollider) summary.Add(RebuildCollider());

        summary.RemoveAll(string.IsNullOrEmpty);

        MarkDirty(_splineComputer);
        Log($"rebuilt on a spline of length {length:F1} — {string.Join(", ", summary)}.");

        WarnAboutEndMeshes();
    }

    // ── Rebuild steps ───────────────────────────────────────────────────────────────────────

    // Re-fits the belt mesh. Auto Update already rebuilds it when the spline changes, so the only
    // real work here is the optional retiling — but the explicit Rebuild is what makes the button
    // honest when Auto Update has been turned off on the prefab.
    private string RebuildMesh(float length)
    {
        if (_splineMesh == null) return "no belt mesh";

        Undo.RecordObject(_splineMesh, RebuildUndoName);

        var channelCount = _splineMesh.GetChannelCount();
        var authoredCount = channelCount > 0 ? _splineMesh.GetChannel(0).count : 0;

        if (_meshTilePerDistance > 0f && channelCount > 0)
        {
            var count = Mathf.Max(1, Mathf.RoundToInt(length * _meshTilePerDistance));
            for (var i = 0; i < channelCount; i++)
                _splineMesh.GetChannel(i).count = count;

            _splineMesh.Rebuild();
            MarkDirty(_splineMesh);
            return $"mesh retiled to {count} across {channelCount} channel(s)";
        }

        _splineMesh.Rebuild();
        MarkDirty(_splineMesh);

        var density = authoredCount / length;
        return $"mesh refitted (kept {authoredCount} tiles, i.e. {density:F3} per unit — put that in " +
               "Mesh Tile Per Distance to hold tile size instead)";
    }

    // Re-spaces the slots and re-bakes what the sandbox hands to ConveyorSlotCreateSystem at play
    // time. The slot COUNT is deliberately derived from the baked CarrierBlockArgs rather than from
    // the new spline length, which is what keeps the carriers and their blocks valid — see the class
    // summary. It is still reconciled rather than assumed, because the carrier config can change
    // underneath a generated level.
    private string RebuildSlots()
    {
        var physicsConfig = new BlockPhysicsConfig();
        var args = _levelSandbox.CarrierBlockArgs;

        // CarrierConfig.Sizes is keyed by physics type, and a level generated against a different
        // type — or a config edited since — leaves no entry to divide by. Fail with the reason rather
        // than throwing halfway through the rebuild.
        CarrierConfig.SizeArgs carrierSize;
        try
        {
            carrierSize = _carrierConfig.Sizes[physicsConfig.Type];
        }
        catch (KeyNotFoundException)
        {
            LogError($"CarrierConfig has no Sizes entry for physics type {physicsConfig.Type}, so the " +
                     "slot group size cannot be derived. Add one, or re-generate the level.");
            return "slots skipped";
        }

        if (carrierSize.SlotElementCount <= 0)
        {
            LogError($"CarrierConfig's {physicsConfig.Type} entry has a Slot Element Count of " +
                     $"{carrierSize.SlotElementCount}; it must be at least 1.");
            return "slots skipped";
        }

        var groupSlotCount = LevelGeometry.GetGroupSlotCount(_carrierConfig, physicsConfig.Type, args);
        if (groupSlotCount <= 0 || args.SlotCount <= 0)
        {
            LogError($"the baked block grid {args.Size} x {args.SlotCount} slots gives a group slot " +
                     "count of " + groupSlotCount + ". Generate the level from the Level Sandbox " +
                     "inspector before re-shaping it.");
            return "slots skipped";
        }

        var layout = LevelGeometry.ComputeSlotLayout(
            _splineComputer, _conveyorConfig, new PaceConfig(), new ConveyorSystemConfig(),
            groupSlotCount, args.SlotCount);

        var slotsRoot = ResolveRoot(SlotsRootName, _levelSandbox.SlotsRoot);

        var slots = new List<Transform>(slotsRoot.childCount);
        for (var i = 0; i < slotsRoot.childCount; i++)
            slots.Add(slotsRoot.GetChild(i));

        var removed = 0;
        for (var i = slots.Count - 1; i >= layout.TotalSlotCount; i--)
        {
            Undo.DestroyObjectImmediate(slots[i].gameObject);
            slots.RemoveAt(i);
            removed++;
        }

        var added = 0;
        while (slots.Count < layout.TotalSlotCount)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(_conveyorConfig.SlotPrefab, slotsRoot);
            Undo.RegisterCreatedObjectUndo(instance, RebuildUndoName);
            slots.Add(instance.transform);
            added++;
        }

        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            Undo.RecordObject(slot.gameObject, RebuildUndoName);
            slot.gameObject.name = $"Slot {i}";

            if (!slot.TryGetComponent<SplineFollower>(out var follower)) continue;

            Undo.RecordObject(follower, RebuildUndoName);
            follower.spline = _splineComputer;
            follower.SetPercent(layout.Percents[i]);
            MarkDirty(follower);
        }

        // Speed and collision distance are length-derived and live on the sandbox, not the conveyor,
        // so re-spacing the slots without this would leave the level running at the old path's pace.
        Undo.RecordObject(_levelSandbox, RebuildUndoName);
        _levelSandbox.SetBakedValues(
            _levelSandbox.Level, _conveyor, _levelSandbox.CarriersRoot, slotsRoot,
            _levelSandbox.TriggersRoot, args, layout);
        MarkDirty(_levelSandbox);

        var churn = added > 0 || removed > 0 ? $" (+{added}/-{removed})" : "";
        return $"{slots.Count} slots re-spaced{churn}, speed {layout.ConveyorSpeed:F2}, " +
               $"collision distance {layout.SlotCollisionDistance:F2}";
    }

    // Arrow count is floor(length / ArrowPerDistance), so it changes with the path and the arrows are
    // destroyed and recreated rather than moved. Ported from LevelSandboxGenerator, which in turn
    // came from ConveyorArrowSystem.
    private string RebuildArrows(float length)
    {
        if (_conveyorConfig.ArrowPrefab == null || _conveyorConfig.ArrowPerDistance <= 0f)
            return "no arrows configured";

        var arrowsRoot = ResolveRoot(ArrowsRootName, null);
        DestroyChildren(arrowsRoot);

        var arrowCount = Mathf.FloorToInt(length / _conveyorConfig.ArrowPerDistance);
        if (arrowCount <= 0)
            return $"0 arrows (a length of {length:F1} does not fit one every " +
                   $"{_conveyorConfig.ArrowPerDistance})";

        var arrowSpacing = length / arrowCount;
        for (var i = 0; i < arrowCount; i++)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(_conveyorConfig.ArrowPrefab, arrowsRoot);
            Undo.RegisterCreatedObjectUndo(instance, RebuildUndoName);
            instance.name = $"Arrow {i}";
            instance.transform.localScale = Vector3.one;

            if (!instance.TryGetComponent<SplineFollower>(out var follower)) continue;
            follower.spline = _splineComputer;
            follower.SetPercent(_splineComputer.Travel(0d, arrowSpacing * i));
        }

        return $"{arrowCount} arrows recreated";
    }

    // Re-fits the collider mesh, creating it if the generated level never had one. Channel counts and
    // the per-physics-type ceiling height are ported from LevelSandboxGenerator.ConfigureColliderMesh.
    private string RebuildCollider()
    {
        if (_conveyorConfig.ColliderPrefab == null) return "no collider prefab configured";

        var colliderMesh = FindColliderMesh();
        if (colliderMesh == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(_conveyorConfig.ColliderPrefab, transform);
            Undo.RegisterCreatedObjectUndo(instance, RebuildUndoName);
            instance.name = ColliderName;

            if (!instance.TryGetComponent(out colliderMesh))
                return "collider prefab has no Spline Mesh";
        }

        Undo.RecordObject(colliderMesh, RebuildUndoName);
        colliderMesh.spline = _splineComputer;

        if (_splineMesh != null && _splineMesh.GetChannelCount() > 0)
        {
            var sourceCount = _splineMesh.GetChannel(0).count;
            var channelCount = colliderMesh.GetChannelCount();
            for (var i = 0; i < channelCount; i++)
                colliderMesh.GetChannel(i).count = sourceCount;
        }

        // Remote config is a stub that always returns defaults, so this is what the game runs with —
        // same assumption LevelSandboxGenerator makes.
        var ceilingY = new BlockPhysicsConfig().Type switch
        {
            BlockPhysicsConfig.PhysicsType.SandLoop => .388f,
            BlockPhysicsConfig.PhysicsType.SandLoopLite => .49f,
            BlockPhysicsConfig.PhysicsType.FlatCubes => .55f,
            _ => float.NaN
        };

        if (!float.IsNaN(ceilingY) && colliderMesh.GetChannelCount() > 3)
        {
            var ceilChannel = colliderMesh.GetChannel(3);
            ceilChannel.minOffset = ceilChannel.minOffset.WithY(ceilingY);
        }

        colliderMesh.Rebuild();
        MarkDirty(colliderMesh);

        return "collider refitted";
    }

    // The collider prefab carries a second SplineMesh, which is exactly why SceneScope registers the
    // belt's one by hand. Match on "not the belt mesh" rather than on the name, so a renamed collider
    // object is still found.
    private SplineMesh FindColliderMesh()
    {
        var meshes = GetComponentsInChildren<SplineMesh>(true);
        foreach (var mesh in meshes)
        {
            if (mesh == _splineMesh) continue;
            if (mesh.transform == transform) continue;
            return mesh;
        }

        return null;
    }

    // ── Bevel ───────────────────────────────────────────────────────────────────────────────

    // Reads the spline's current points, bevels them and writes them back, reporting the first
    // specific reason it cannot. Shared by both bevel buttons; the caller owns the flag handling.
    private bool TryBevelCurrentPath(string action)
    {
        var count = _splineComputer.pointCount;
        var closed = _splineComputer.isClosed;

        if (count < 3)
        {
            LogError($"{action} needs at least 3 points; the spline has {count}.");
            return false;
        }

        if (_bevelCount < 1)
        {
            LogError($"Bevel Count is {_bevelCount}; it must be at least 1.");
            return false;
        }

        // Points come back in world space so Bevel Max Distance is a real distance on screen rather
        // than one scaled by the conveyor's transform.
        var source = _splineComputer.GetPoints();

        // Classify first so the budget is checked against the EXACT output size rather than a
        // worst-case guess — with the angle filter in play most points of a subdivided sheet path
        // pass through unchanged, and the real total is far below count * 2 * bevelCount.
        var isCorner = ClassifyCorners(
            source, closed, _bevelMaxDistance, _minCornerAngle, out var cornerCount, out var clampedCount);

        // Silence here would look like a broken button, so say why nothing will change.
        if (cornerCount == 0)
        {
            LogWarning($"{action} found no corner turning by at least {_minCornerAngle}°, so the path " +
                       "is unchanged. Lower Min Corner Angle to round shallower corners.");
            return false;
        }

        var projected = (long)cornerCount * 2 * _bevelCount + (count - cornerCount);
        if (projected > _maxPoints)
        {
            LogError($"{action} would produce {projected} points from {count}, over the Max Points " +
                     $"budget of {_maxPoints}. Lower Bevel Count, raise Min Corner Angle, or raise " +
                     "Max Points.");
            return false;
        }

        var beveled = BevelPath(source, isCorner, _bevelCount, _bevelMaxDistance);

        Undo.RecordObjects(new Object[] { _splineComputer, this }, ShapeUndoName);

        _splineComputer.SetPoints(beveled);
        if (closed) _splineComputer.Close();
        _splineComputer.RebuildImmediate();
        MarkDirty(_splineComputer);

        if (clampedCount > 0)
        {
            LogWarning($"{clampedCount} of {cornerCount} corners were too tight for a Bevel Max " +
                       $"Distance of {_bevelMaxDistance} and clamped to half their shortest edge, so " +
                       "those corners are rounded less than the rest. Lower Bevel Max Distance for a " +
                       "uniform result.");
        }

        Log($"{action} took {count} points to {beveled.Length} ({cornerCount} corners rounded, " +
            $"{count - cornerCount} left untouched below {_minCornerAngle}°). Press Rebuild Conveyor " +
            "to move the rest of the conveyor onto the new path.");

        return true;
    }

    // Decides which points are corners worth rounding, and reports how many would be limited by a
    // too-short edge rather than by maxDistance. Split out from the emit pass purely so the caller
    // can size the result before allocating it.
    //
    // On an open path the two endpoints are never corners: they have only one edge, so there is
    // nothing to set back along on the far side.
    private static bool[] ClassifyCorners(
        SplinePoint[] points, bool closed, float maxDistance, float minAngleDegrees,
        out int cornerCount, out int clampedCount)
    {
        var n = points.Length;
        var isCorner = new bool[n];
        cornerCount = 0;
        clampedCount = 0;

        for (var i = 0; i < n; i++)
        {
            if (!closed && (i == 0 || i == n - 1)) continue;

            var current = points[i].position;
            var toPrevious = points[(i - 1 + n) % n].position - current;
            var toNext = points[(i + 1) % n].position - current;
            var lengthPrevious = toPrevious.magnitude;
            var lengthNext = toNext.magnitude;

            // A duplicated point gives a zero-length edge, which has no direction to offset along and
            // would normalise into a NaN that poisons the whole path.
            if (lengthPrevious <= Mathf.Epsilon || lengthNext <= Mathf.Epsilon) continue;

            // Vector3.Angle between the two outgoing edge directions is the interior angle, so a
            // straight-through point reads 180° and turns by 0.
            if (180f - Vector3.Angle(toPrevious, toNext) < minAngleDegrees) continue;

            isCorner[i] = true;
            cornerCount++;

            if (maxDistance > .5f * Mathf.Min(lengthPrevious, lengthNext)) clampedCount++;
        }

        return isCorner;
    }

    // Rounds the flagged corners. The points are already in world space, so no conversion is
    // involved and maxDistance is a world distance.
    //
    // Each corner is set back along both adjacent edges by maxDistance — clamped to half the shorter
    // edge — and the sharp point is REPLACED by 2*count points sampled along a quadratic Bézier whose
    // control point is that corner. Sampling t over [0,1] in 2*count steps puts the outermost pair
    // exactly on the setback points and, because 2*count is even, never lands a sample on the
    // midline: the corner always becomes count points either side of where it was. The Bézier is
    // tangent to both edges where it meets them, so the rounding blends into the straight sections,
    // and it stays inside the triangle formed with the corner, so no point can exceed maxDistance.
    // Unlike a true circular arc it also handles collinear and reflex corners with no special case.
    //
    // Clamping to half the shorter edge is what makes this safe on irregular input: two setbacks
    // sharing an edge each stay within half of it, so neighbouring bevels meet at worst, never cross.
    //
    // No open/closed flag is needed here: ClassifyCorners never flags an endpoint on an open path,
    // so the wrapping neighbour lookups below only ever wrap on a genuinely closed one.
    private static SplinePoint[] BevelPath(
        SplinePoint[] points, bool[] isCorner, int count, float maxDistance)
    {
        var n = points.Length;
        var perCorner = 2 * count;
        var result = new List<SplinePoint>(n * perCorner);

        for (var i = 0; i < n; i++)
        {
            var source = points[i];
            if (!isCorner[i])
            {
                result.Add(source);
                continue;
            }

            var current = source.position;
            var toPrevious = points[(i - 1 + n) % n].position - current;
            var toNext = points[(i + 1) % n].position - current;
            var lengthPrevious = toPrevious.magnitude;
            var lengthNext = toNext.magnitude;

            var setback = Mathf.Min(maxDistance, .5f * lengthPrevious, .5f * lengthNext);
            var start = current + toPrevious * (setback / lengthPrevious);
            var end = current + toNext * (setback / lengthNext);

            for (var j = 0; j < perCorner; j++)
            {
                var t = (float)j / (perCorner - 1);
                var u = 1f - t;
                result.Add(CopyWithPosition(source, u * u * start + 2f * u * t * current + t * t * end));
            }
        }

        return result.ToArray();
    }

    // Carries the corner's size, colour and normal onto the points that replace it, so a path with
    // authored per-point size or colour does not lose it at every rounded corner. Tangents are
    // rebuilt from the position, which is what the generator produces for every point anyway.
    private static SplinePoint CopyWithPosition(SplinePoint source, Vector3 position)
    {
        var point = new SplinePoint(position)
        {
            color = source.color,
            normal = source.normal,
            size = source.size
        };

        return point;
    }

    // ── Validation and plumbing ─────────────────────────────────────────────────────────────

    private bool CanEdit()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;

        LogError("cannot re-shape the conveyor in play mode. Exit play mode and try again.");
        return false;
    }

    private bool ValidateForRebuild()
    {
        var missing = new List<string>();

        if (_splineComputer == null) missing.Add("Spline Computer");
        if (_conveyorConfig == null) missing.Add("Conveyor Config");

        if (_rebuildSlots)
        {
            if (_carrierConfig == null) missing.Add("Carrier Config");
            if (_levelSandbox == null) missing.Add("Level Sandbox");
            if (_conveyor == null) missing.Add("Conveyor");
            if (_conveyorConfig != null && _conveyorConfig.SlotPrefab == null)
                missing.Add("ConveyorConfig.SlotPrefab");
        }

        if (missing.Count == 0) return true;

        LogError($"cannot rebuild — missing {string.Join(", ", missing)}. Attach this to the " +
                 "generated Conveyor GameObject and make sure the scene has a SceneScope with its " +
                 "Data list filled (Level Sandbox > Set Up Scene).");
        return false;
    }

    // Prefers the root the sandbox already points at, so a renamed or re-parented Slots object keeps
    // working, and only falls back to the name the generator used.
    private Transform ResolveRoot(string name, Transform baked)
    {
        if (baked != null && baked.IsChildOf(transform)) return baked;

        var existing = transform.Find(name);
        if (existing != null) return existing;

        var created = new GameObject(name);
        created.transform.SetParent(transform, false);
        Undo.RegisterCreatedObjectUndo(created, RebuildUndoName);
        return created.transform;
    }

    private static void DestroyChildren(Transform root)
    {
        for (var i = root.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
    }

    private void ApplySplineType()
    {
        var type = _splineType switch
        {
            SplineTypeOverride.Linear => Spline.Type.Linear,
            SplineTypeOverride.CatmullRom => Spline.Type.CatmullRom,
            SplineTypeOverride.BSpline => Spline.Type.BSpline,
            _ => _splineComputer.type
        };

        if (_splineComputer.type != type) _splineComputer.type = type;
    }

    // BSpline points are control points, not path points, so a shape authored at a given size comes
    // out visibly smaller and already rounded. Saying so once beats the user measuring the belt and
    // concluding the size fields are broken.
    private void WarnIfTypeHidesTheShape()
    {
        if (_splineComputer.type != Spline.Type.BSpline) return;

        LogWarning("the spline is a BSpline, which treats these points as control points and does " +
                   "not pass through them — the belt will sit inside the size you asked for and its " +
                   "corners are already rounded. Set Spline Type to Linear for the shape exactly as " +
                   "authored, or CatmullRom for a smooth curve through every point.");
    }

    // A ring drawn with straight segments is only as round as its point count. CatmullRom hides that
    // at a handful of points; Linear does not, and the shortfall lands on the tight ends of an oval
    // where it is most obvious.
    private void WarnIfRingIsCoarse()
    {
        const int smoothEnoughForLinear = 16;

        if (_shapeKind != ShapeKind.Ellipse) return;
        if (_splineComputer.type != Spline.Type.Linear) return;
        if (_segmentCount >= smoothEnoughForLinear) return;

        LogWarning($"a {_segmentCount}-segment ring on a Linear spline is a visible polygon, not a " +
                   $"curve. Raise Segment Count to {smoothEnoughForLinear} or more, or switch Spline " +
                   "Type to CatmullRom, which curves through the points you already have.");
    }

    // End meshes cap an open path; a loop has no ends. They carry BlockTrigger, and triggers are the
    // author's to place, so this reports rather than deletes.
    private void WarnAboutEndMeshes()
    {
        if (!_splineComputer.isClosed) return;

        var endRoot = transform.Find(EndMeshRootName);
        if (endRoot == null || endRoot.childCount == 0) return;

        LogWarning($"the path is now a closed loop but '{EndMeshRootName}' still has " +
                   $"{endRoot.childCount} object(s) sitting at the old open ends. They carry " +
                   "BlockTrigger, so they are left for you to delete or reposition.");
    }

    private static void MarkDirty(Object target)
    {
        if (target == null) return;

        EditorUtility.SetDirty(target);

        if (target is Component component && component.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
    }

    private void Log(string message) => Debug.Log($"<b>Conveyor Shape</b>: {message}", this);
    private void LogWarning(string message) => Debug.LogWarning($"<b>Conveyor Shape</b>: {message}", this);
    private void LogError(string message) => Debug.LogError($"<b>Conveyor Shape</b>: {message}", this);

#endif
}

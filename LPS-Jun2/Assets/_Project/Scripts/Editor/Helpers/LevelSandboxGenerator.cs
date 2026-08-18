using System.Collections.Generic;
using Cathei.BakingSheet;
using Cathei.BakingSheet.Unity;
using Cysharp.Threading.Tasks;
using Dreamteck.Splines;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Generates a whole level into the open scene as ordinary, editable GameObjects.
///
/// This is the edit-time counterpart to what LevelBuildSystem, ConveyorSlotCreateSystem,
/// CarrierBlockCreateSystem, ConveyorArrowSystem, ConveyorEndMeshSystem, ConveyorColliderSystem and
/// BlockTriggerSystem used to do at run time. All the maths lives in <see cref="LevelGeometry"/> so
/// there is only one copy of it.
///
/// Nothing here runs at play time — the generated scene is the level.
/// </summary>
public static class LevelSandboxGenerator
{
    private const string ConveyorPrefabPath = "Assets/_Project/Prefabs/Conveyor/Conveyor.prefab";

    public sealed class Result
    {
        public GameObject Root;
        public string Error;
        public bool Ok => Error == null;
    }

    public static async UniTask<SheetContainer> BakeSheets()
    {
        var fileSystem = new CustomFileSystem(new Dictionary<string, string>());
        var converters = new ISheetImporter[] { new JsonSheetConverter("Sheets/Local", fileSystem) };
        var container = new SheetContainer(converters, new UnityLogger());

        var success = await container.Bake(converters);
        return success ? container : null;
    }

    /// <summary>
    /// Positional lookup. Sheet&lt;TKey, TValue&gt; hides Collection's positional indexer with a
    /// by-Id one, and Prefs.Level / LevelSheet.GetCurrent are positional (Items[level]).
    /// </summary>
    public static LevelSheet.Level GetLevelAt(SheetContainer container, int index)
    {
        return ((IList<LevelSheet.Level>)container.Levels)[index];
    }

    public static Result Generate(SheetContainer sheets, int levelNumber, SceneScope scope, LevelSandbox sandbox,
        Colors colors, ConveyorConfig conveyorConfig, CarrierConfig carrierConfig, Carriers carriers, Blocks blocks)
    {
        var levelIndex = levelNumber - 1;
        if (levelIndex < 0 || levelIndex >= sheets.Levels.Count)
            return Fail($"level {levelNumber} does not exist. Valid range is 1..{sheets.Levels.Count}.");

        if (conveyorConfig == null || carrierConfig == null || carriers == null || blocks == null || colors == null)
            return Fail("assign Colors, ConveyorConfig, CarrierConfig, Carriers and Blocks before generating.");

        var level = GetLevelAt(sheets, levelIndex);
        var splineLayout = level.Spline.Ref;
        var carriersRow = level.Carriers.Ref;

        // Remote config is a stub that always returns defaults, so this is what the game runs with.
        var physicsConfig = new BlockPhysicsConfig();
        var paceConfig = new PaceConfig();
        var conveyorSystemConfig = new ConveyorSystemConfig();
        var loseConfig = new LoseConfig();

        var scene = sandbox.gameObject.scene;
        var levelRoot = NewChild("Level", null, scene);

        // ---- conveyor + spline ------------------------------------------------
        var conveyorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConveyorPrefabPath);
        if (conveyorPrefab == null)
            return Fail($"conveyor prefab not found at {ConveyorPrefabPath}.");

        var conveyorGo = (GameObject)PrefabUtility.InstantiatePrefab(conveyorPrefab, levelRoot.transform);
        conveyorGo.name = "Conveyor";
        var conveyor = conveyorGo.GetComponent<Conveyor>();
        var splineComputer = conveyorGo.GetComponent<SplineComputer>();
        var splineMesh = conveyorGo.GetComponent<SplineMesh>();
        if (conveyor == null || splineComputer == null)
            return Fail("the conveyor prefab is missing Conveyor or SplineComputer.");

        LevelGeometry.BuildSpline(splineComputer, splineLayout);
        var splineLength = splineComputer.CalculateLength();

        // ---- derived sizing ---------------------------------------------------
        var carrierBlockArgs = LevelGeometry.ComputeCarrierBlockArgs(
            carrierConfig, physicsConfig.Type, splineLength, level.SlotCount, loseConfig.FailOnFull);

        var blockSize = carrierBlockArgs.Size;
        var groupBlockCount = LevelGeometry.GetGroupBlockCount(carrierConfig, physicsConfig.Type, carrierBlockArgs);
        var groupSlotCount = LevelGeometry.GetGroupSlotCount(carrierConfig, physicsConfig.Type, carrierBlockArgs);
        var configSize = carrierConfig.Sizes[physicsConfig.Type];

        // ---- carriers + blocks ------------------------------------------------
        var carriersRoot = NewChild("Carriers", levelRoot.transform, scene);
        var levelData = new Level(levelIndex, level);
        var placements = LevelGeometry.GetCarrierPlacements(splineLayout, conveyorConfig.ConveyorHeight);

        var spawnedCarriers = new List<Carrier>();
        foreach (var placement in placements)
        {
            var carrierData = carriers.Get(FeatureType.None);
            if (carrierData.Prefab == null)
                return Fail("Carriers data has no prefab for FeatureType.None.");

            var carrierGo = (GameObject)PrefabUtility.InstantiatePrefab(
                carrierData.Prefab.gameObject, carriersRoot.transform);
            var carrier = carrierGo.GetComponent<Carrier>();
            carrier.name = $"Carrier {placement.Type}";
            carrier.EditorSetType(placement.Type);
            LevelGeometry.ApplyPivot(carrier.transform, carrier.Pivot, placement);

            spawnedCarriers.Add(carrier);

            if (!carriersRow.TryGetCarrier(placement.Type, out var carrierColors)) continue;

            var index = 0;
            foreach (var args in carrierColors)
            {
                var blockData = blocks.Get(args.Feature);
                if (blockData.Prefab == null) continue;

                var colorType = levelData.GetColor(args.Color);
                for (var i = 0; i < groupBlockCount; i++, index++)
                    CreateCarrierBlock(carrier, blockData.Prefab, colors, colorType, args.Feature,
                        index, blockSize, configSize);
            }
        }

        // ---- conveyor slots ---------------------------------------------------
        var slotLayout = LevelGeometry.ComputeSlotLayout(
            splineComputer, conveyorConfig, paceConfig, conveyorSystemConfig, groupSlotCount, carrierBlockArgs.SlotCount);

        var slotsRoot = NewChild("Slots", conveyorGo.transform, scene);
        if (conveyorConfig.SlotPrefab == null)
            return Fail("ConveyorConfig.SlotPrefab is not assigned.");

        for (var i = 0; i < slotLayout.TotalSlotCount; i++)
        {
            var slotGo = (GameObject)PrefabUtility.InstantiatePrefab(conveyorConfig.SlotPrefab, slotsRoot.transform);
            slotGo.name = $"Slot {i}";

            var follower = slotGo.GetComponent<SplineFollower>();
            if (follower == null) continue;
            follower.spline = splineComputer;
            follower.SetPercent(slotLayout.Percents[i]);
        }

        // ---- arrows (cosmetic) ------------------------------------------------
        if (conveyorConfig.ArrowPrefab != null && conveyorConfig.ArrowPerDistance > 0f)
        {
            var arrowsRoot = NewChild("Arrows", conveyorGo.transform, scene);
            var arrowCount = Mathf.FloorToInt(splineLength / conveyorConfig.ArrowPerDistance);
            for (var i = 0; i < arrowCount; i++)
            {
                var arrow = (GameObject)PrefabUtility.InstantiatePrefab(conveyorConfig.ArrowPrefab, arrowsRoot.transform);
                arrow.name = $"Arrow {i}";
                arrow.transform.localScale = Vector3.one;

                var follower = arrow.GetComponent<SplineFollower>();
                if (follower == null) continue;
                follower.spline = splineComputer;
                follower.SetPercent(splineComputer.Travel(0d, splineLength / arrowCount * i));
            }
        }

        // ---- conveyor collider mesh ------------------------------------------
        if (conveyorConfig.ColliderPrefab != null)
        {
            var colliderGo = (GameObject)PrefabUtility.InstantiatePrefab(conveyorConfig.ColliderPrefab, conveyorGo.transform);
            colliderGo.name = "Collider";
            ConfigureColliderMesh(colliderGo, splineComputer, splineMesh, physicsConfig.Type);
        }

        // ---- end meshes -------------------------------------------------------
        if (!splineComputer.isClosed && conveyorConfig.EndPrefab != null)
        {
            var endRoot = NewChild("End Meshes", conveyorGo.transform, scene);
            CreateEndMesh(endRoot.transform, conveyorConfig.EndPrefab, splineComputer, 0d, false, "Enter Mesh");
            CreateEndMesh(endRoot.transform, conveyorConfig.EndPrefab, splineComputer, 1d, true, "Exit Mesh");
        }

        // ---- carrier triggers -------------------------------------------------
        var triggersRoot = NewChild("Triggers", levelRoot.transform, scene);
        if (physicsConfig.Type != BlockPhysicsConfig.PhysicsType.None)
        {
            foreach (var carrier in spawnedCarriers)
                CreateCarrierTrigger(triggersRoot.transform, carrier, splineComputer, conveyorConfig, scene);
        }

        // ---- wire it all up ---------------------------------------------------
        sandbox.SetBakedValues(levelNumber, conveyor, carriersRoot.transform, slotsRoot.transform,
            triggersRoot.transform, carrierBlockArgs, slotLayout);
        scope.SetSceneReferences(conveyor, splineComputer, splineMesh);

        EditorUtility.SetDirty(sandbox);
        EditorUtility.SetDirty(scope);
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"<b>Level Sandbox</b>: generated level {levelNumber} — " +
                  $"{spawnedCarriers.Count} carriers, {slotLayout.TotalSlotCount} slots, " +
                  $"block grid {blockSize.x}x{blockSize.y}x{blockSize.z}, " +
                  $"spline length {splineLength:F1}.", levelRoot);

        return new Result { Root = levelRoot };
    }

    // ------------------------------------------------------------- carrier blocks

    /// <summary>
    /// Places one block in a carrier: the prefab, the colour and feature it keeps across a scene save,
    /// and the slot in the carrier's grid that <paramref name="index"/> maps to. The block's position
    /// and rotation are always computed in carrier.BlockParent's frame, regardless of which transform
    /// it is actually parented under — see <paramref name="parent"/>.
    /// </summary>
    /// <param name="parent">
    /// Where the block is parented. Defaults to carrier.BlockParent; FillCarrier passes a per-group
    /// container instead so the block still lands on its usual grid position and rotation.
    /// </param>
    /// <param name="localPositionOverride">
    /// Skips the index-to-coordinate lookup and places the block here instead (still in
    /// carrier.BlockParent's frame). FillCarrier uses this for groups beyond the level's own
    /// sizing, where <paramref name="index"/> would otherwise fall outside blockSize's grid.
    /// </param>
    public static Block CreateCarrierBlock(Carrier carrier, Block prefab, Colors colors, ColorType colorType,
        FeatureType feature, int index, Vector3Int blockSize, CarrierConfig.SizeArgs configSize,
        Transform parent = null, Vector3? localPositionOverride = null)
    {
        parent ??= carrier.BlockParent;

        var blockGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
        var block = blockGo.GetComponent<Block>();
        block.name = $"Block {index}";

        block.EditorSetColor(colors, colorType, feature);

        Vector3 localPosition;
        if (localPositionOverride.HasValue)
        {
            localPosition = localPositionOverride.Value;
        }
        else
        {
            var coordinate = LevelGeometry.GetBlockCoordinate(index, blockSize);
            localPosition = LevelGeometry.CoordinateToLocalPosition(coordinate, blockSize, configSize);
        }

        var blockT = block.transform;
        blockT.SetPositionAndRotation(carrier.BlockParent.TransformPoint(localPosition), carrier.BlockParent.rotation);
        blockT.localScale = Vector3.one * configSize.ScaleMultiplier;

        return block;
    }

    /// <summary>
    /// Removes every block a carrier is holding, including ones grouped under a
    /// CarrierBlockGroupParent container by FillCarrier's Start fill. Returns how many blocks went.
    /// </summary>
    public static int ClearCarrierBlocks(Carrier carrier)
    {
        if (carrier.BlockParent == null) return 0;

        var count = 0;
        for (var i = carrier.BlockParent.childCount - 1; i >= 0; i--)
        {
            var child = carrier.BlockParent.GetChild(i);

            if (child.TryGetComponent<Block>(out _))
            {
                Undo.DestroyObjectImmediate(child.gameObject);
                count++;
                continue;
            }

            if (child.TryGetComponent<CarrierBlockGroupParent>(out _))
            {
                count += CountBlocks(child);
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        return count;
    }

    private static int CountBlocks(Transform parent)
    {
        var count = 0;
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            count += child.TryGetComponent<Block>(out _) ? 1 : CountBlocks(child);
        }

        return count;
    }

    /// <summary>
    /// Clears a carrier and refills it to carrier.StartGroupCount colour groups (default 4, which
    /// matches the level's own sizing exactly — this only ever generates more than the level's
    /// grid would when StartGroupCount is raised above 4). Walks the palette one entry per colour
    /// group so neighbouring groups differ whenever there is more than one colour to draw on.
    /// Colours here are only what the scene view shows — SceneScope rerolls them at run time.
    /// Returns how many blocks were placed.
    ///
    /// Each colour group's blocks are parented under their own CarrierBlockGroupParent container
    /// (a child of carrier.BlockParent), positioned at the matching GroupBlocks entry's exact world
    /// position — the visual mesh this fill is standing in for until the carrier actually plays.
    /// Groups beyond what the level's sizing produces need GroupBlocks slots that don't exist yet;
    /// EnsureGroupBlockCapacity grows the carrier's real GroupBlocks/GroupBlockFilters lists first,
    /// and their blocks are positioned by extrapolating the last real group's layout using the same
    /// spacing the extra GroupBlocks slots were placed at — see the loop below.
    /// </summary>
    public static int FillCarrier(Carrier carrier, Blocks blocks, Colors colors, IReadOnlyList<ColorType> palette,
        Vector3Int blockSize, int groupBlockCount, CarrierConfig.SizeArgs configSize)
    {
        if (carrier.BlockParent == null) return 0;

        var blockData = blocks.Get(FeatureType.None);
        if (blockData.Prefab == null)
        {
            Debug.LogError($"<b>Level Sandbox</b>: Blocks data has no prefab for {nameof(FeatureType)}.None, " +
                           $"cannot fill '{carrier.name}'.", carrier);
            return 0;
        }

        // Defensive clamp — [SerializeField] alone doesn't protect against a hand-edited scene value.
        var startGroupCount = Mathf.Max(4, carrier.StartGroupCount);
        EnsureGroupBlockCapacity(carrier, startGroupCount);

        ClearCarrierBlocks(carrier);

        // maxBaseBlockCount is exactly what the level's own sizing produces (unchanged formula), so
        // when startGroupCount is left at 4 this equals desiredTotalBlockCount and every block below
        // takes the untouched, byte-identical-to-before code path.
        var maxBaseBlockCount = blockSize.x * blockSize.y * blockSize.z;
        var baseGroupCount = groupBlockCount > 0 ? maxBaseBlockCount / groupBlockCount : maxBaseBlockCount;
        var desiredTotalBlockCount = groupBlockCount > 0 ? startGroupCount * groupBlockCount : maxBaseBlockCount;

        var groupDeltaLocal = Vector3.zero;
        if (desiredTotalBlockCount > maxBaseBlockCount && carrier.GroupBlocks.Count >= 2)
        {
            groupDeltaLocal = carrier.BlockParent.InverseTransformPoint(carrier.GroupBlocks[1].transform.position)
                             - carrier.BlockParent.InverseTransformPoint(carrier.GroupBlocks[0].transform.position);
        }

        Transform groupParent = null;
        var currentGroup = -1;

        for (var index = 0; index < desiredTotalBlockCount; index++)
        {
            var group = groupBlockCount > 0 ? index / groupBlockCount : index;
            if (group != currentGroup)
            {
                currentGroup = group;
                groupParent = CreateGroupParent(carrier, group);
            }

            var colorType = palette[group % palette.Count];

            Vector3? positionOverride = null;
            if (index >= maxBaseBlockCount)
            {
                // A group past the level's own grid: reuse the last real group's internal x/y/z-
                // within-group layout (GetBlockCoordinate/CoordinateToLocalPosition never sees an
                // index outside blockSize's bounds) and shift it by the authored GroupBlocks spacing
                // for however many extra groups separate it from that last real group.
                var localIndexInGroup = index % groupBlockCount;
                var anchorIndex = (baseGroupCount - 1) * groupBlockCount + localIndexInGroup;
                var anchorCoordinate = LevelGeometry.GetBlockCoordinate(anchorIndex, blockSize);
                var anchorPosition = LevelGeometry.CoordinateToLocalPosition(anchorCoordinate, blockSize, configSize);
                var extraGroups = group - (baseGroupCount - 1);
                positionOverride = anchorPosition + groupDeltaLocal * extraGroups;
            }

            var block = CreateCarrierBlock(carrier, blockData.Prefab, colors, colorType, FeatureType.None,
                index, blockSize, configSize, groupParent, positionOverride);

            Undo.RegisterCreatedObjectUndo(block.gameObject, "Apply Carrier Modes");
        }

        return desiredTotalBlockCount;
    }

    /// <summary>
    /// One CarrierBlockGroupParent container per colour group, parented under BlockParent and placed
    /// at the matching carrier.GroupBlocks entry's exact world position. Falls back to BlockParent's
    /// own position if the carrier has fewer GroupBlocks entries than groups being filled.
    /// </summary>
    private static Transform CreateGroupParent(Carrier carrier, int groupIndex)
    {
        var go = new GameObject($"Group {groupIndex}", typeof(CarrierBlockGroupParent));
        Undo.RegisterCreatedObjectUndo(go, "Apply Carrier Modes");

        var t = go.transform;
        t.SetParent(carrier.BlockParent, worldPositionStays: false);
        t.rotation = carrier.BlockParent.rotation;
        t.localScale = Vector3.one;

        if (groupIndex >= 0 && groupIndex < carrier.GroupBlocks.Count)
            t.position = carrier.GroupBlocks[groupIndex].transform.position;

        return t;
    }

    /// <summary>
    /// Grows carrier.GroupBlocks/GroupBlockFilters to at least desiredCount entries, permanently —
    /// these are real scene objects that also drive BlockCarrierMeshSystem's group-merge visuals at
    /// run time, not just a sandbox preview. Clones whatever currently sits at index 1 (a generic,
    /// unrotated middle slot) and inserts it right after index 0 each time, so index 0 (front cap)
    /// never moves and the carrier's last authored slot (rear cap) stays last, just pushed further
    /// back. A no-op — zero Undo/dirty churn — when the carrier already has enough slots, which is
    /// always true at the default StartGroupCount 4.
    /// </summary>
    private static void EnsureGroupBlockCapacity(Carrier carrier, int desiredCount)
    {
        if (carrier.GroupBlocks.Count < 2)
        {
            if (desiredCount > carrier.GroupBlocks.Count)
                Debug.LogWarning($"<b>Level Sandbox</b>: '{carrier.name}' has too few GroupBlocks to " +
                                 "expand (needs at least 2 as a spacing template). Leaving it as is.", carrier);
            return;
        }

        if (carrier.GroupBlocks.Count >= desiredCount) return;

        var groupBlocks = carrier.GroupBlocks;
        var groupBlockFilters = carrier.GroupBlockFilters;
        var delta = groupBlocks[1].transform.position - groupBlocks[0].transform.position;

        Undo.RecordObject(carrier, "Apply Carrier Modes");

        while (groupBlocks.Count < desiredCount)
        {
            var template = groupBlocks[1].gameObject;
            var clone = Object.Instantiate(template, template.transform.parent);
            clone.name = template.name;
            Undo.RegisterCreatedObjectUndo(clone, "Apply Carrier Modes");

            groupBlocks.Insert(1, clone.GetComponent<MeshRenderer>());
            groupBlockFilters.Insert(1, clone.GetComponent<MeshFilter>());
        }

        for (var i = 0; i < groupBlocks.Count; i++)
        {
            Undo.RecordObject(groupBlocks[i].transform, "Apply Carrier Modes");
            groupBlocks[i].transform.position = groupBlocks[0].transform.position + delta * i;
        }

        EditorUtility.SetDirty(carrier);
    }

    /// <summary>
    /// Paints a restricted sink's head with the colour it accepts, and puts every other carrier's head
    /// back to the prefab material so toggling the option off and pressing the button again cleans up.
    ///
    /// This survives Play untouched: Carrier.Awake calls base.Awake — and so OnRent's
    /// InjectMaterial(_originalMaterial) — before _originalMaterial is read off the head, so that call
    /// no-ops on a scene carrier's first rent and what is written here stays put.
    /// </summary>
    public static void ApplyCarrierHeadColor(Carrier carrier, Colors colors)
    {
        var headRenderer = carrier.HeadRenderer;
        if (headRenderer == null) return;

        var material = default(Material);
        if (carrier.IsSink() && carrier.OnlyCompatibleColor)
        {
            material = colors.Get(carrier.CompatibleColor).Material;
            if (material == null)
                Debug.LogWarning($"<b>Level Sandbox</b>: '{carrier.name}' only takes " +
                                 $"{carrier.CompatibleColor}, which has no entry in the Colors asset. " +
                                 "Leaving its head alone.", carrier);
        }
        else
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(headRenderer);
            if (source != null) material = source.sharedMaterials[0];
        }

        if (material == null) return;

        var materials = headRenderer.sharedMaterials;
        if (materials.Length == 0 || materials[0] == material) return;

        Undo.RecordObject(headRenderer, "Apply Carrier Modes");
        materials[0] = material;
        headRenderer.sharedMaterials = materials;
    }

    private static void ConfigureColliderMesh(GameObject colliderGo, SplineComputer splineComputer,
        SplineMesh sourceMesh, BlockPhysicsConfig.PhysicsType physicsType)
    {
        if (!colliderGo.TryGetComponent<SplineMesh>(out var colliderMesh)) return;

        colliderMesh.spline = splineComputer;

        if (sourceMesh != null && sourceMesh.GetChannelCount() > 0)
        {
            var originalChannel = sourceMesh.GetChannel(0);
            var channelCount = colliderMesh.GetChannelCount();
            for (var i = 0; i < channelCount; i++)
                colliderMesh.GetChannel(i).count = originalChannel.count;
        }

        // Ceiling channel height per physics type, ported from ConveyorColliderSystem.
        var ceilingY = physicsType switch
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
    }

    private static void CreateEndMesh(Transform parent, GameObject prefab, SplineComputer splineComputer,
        double percent, bool isExit, string name)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = name;

        var sample = splineComputer.Evaluate(percent);
        instance.transform.position = sample.position;
        instance.transform.rotation = isExit
            ? sample.rotation * Quaternion.AngleAxis(180f, Vector3.up)
            : sample.rotation;
        instance.transform.localScale = Vector3.one;

        if (isExit && instance.GetComponent<BlockTrigger>() == null)
            instance.AddComponent<BlockTrigger>();
    }

    private static void CreateCarrierTrigger(Transform parent, Carrier carrier, SplineComputer splineComputer,
        ConveyorConfig conveyorConfig, UnityEngine.SceneManagement.Scene scene)
    {
        var go = NewChild($"Trigger {carrier.name}", parent, scene);

        var boxCollider = go.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        go.AddComponent<BlockTrigger>();
        go.AddComponent<CarrierBlockTrigger>().SetCarrier(carrier);

        var project = splineComputer.Project(carrier.TransferProjectPoint.position);
        var travel = splineComputer.TravelUnclamped(project.percent, conveyorConfig.CarrierTriggerOffset,
            Spline.Direction.Backward);
        var sample = splineComputer.Evaluate(travel);

        go.transform.position = sample.position + Vector3.up;
        go.transform.forward = sample.forward;
        go.transform.localScale = new Vector3(2f, 2f, .2f);
    }

    private static GameObject NewChild(string name, Transform parent, UnityEngine.SceneManagement.Scene scene)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        else if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    private static Result Fail(string error)
    {
        Debug.LogError($"<b>Level Sandbox</b>: {error}");
        return new Result { Error = error };
    }
}

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

                for (var i = 0; i < groupBlockCount; i++, index++)
                {
                    var blockGo = (GameObject)PrefabUtility.InstantiatePrefab(
                        blockData.Prefab.gameObject, carrier.BlockParent);
                    var block = blockGo.GetComponent<Block>();
                    block.name = $"Block {index}";

                    var colorType = levelData.GetColor(args.Color);
                    block.EditorSetColor(colors, colorType, args.Feature);

                    var coordinate = LevelGeometry.GetBlockCoordinate(index, blockSize);
                    var blockT = block.transform;
                    blockT.localPosition = LevelGeometry.CoordinateToLocalPosition(coordinate, blockSize, configSize);
                    blockT.localRotation = Quaternion.identity;
                    blockT.localScale = Vector3.one * configSize.ScaleMultiplier;
                }
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

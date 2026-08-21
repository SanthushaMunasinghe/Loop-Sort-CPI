using System.Collections.Generic;
using System.Linq;
using Cathei.BakingSheet;
using Lean.Touch;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(LevelSandbox))]
public sealed class LevelSandboxEditor : Editor
{
    private const string DataContainerPath = "Assets/_Project/Resources/Data Containers/Data Container Base.asset";

    private static SheetContainer _sheets;
    private static bool _busy;

    private LevelSandbox Sandbox => (LevelSandbox)target;

    private SerializedProperty _levelProperty;
    private SerializedProperty _conveyorProperty;

    private void OnEnable()
    {
        _levelProperty = serializedObject.FindProperty("_level");
        _conveyorProperty = serializedObject.FindProperty("_conveyor");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        DrawSceneSetup();
        EditorGUILayout.Space(10);
        DrawGenerateButtons();
        EditorGUILayout.Space(10);
        DrawEmptyCarrierRows();
    }

    private bool HasLevel => _conveyorProperty != null && _conveyorProperty.objectReferenceValue != null;

    // ------------------------------------------------------------------ setup

    private void DrawSceneSetup()
    {
        EditorGUILayout.LabelField("Scene Setup", EditorStyles.boldLabel);

        var scope = FindScope();
        var scene = Sandbox.gameObject.scene;
        var roots = scene.IsValid() ? scene.GetRootGameObjects() : System.Array.Empty<GameObject>();

        var hasScope = scope != null;
        var hasData = hasScope && GetDataList(scope).Count > 0;
        var hasCamera = hasScope && scope.Camera != null;
        var hasLeanTouch = roots.Any(x => x.GetComponentInChildren<LeanTouch>(true) != null);
        var hasLight = roots.Any(x => x.GetComponentInChildren<Light>(true) != null);
        var hasStartScene = EditorSceneManager.playModeStartScene != null;

        var ready = hasScope && hasData && hasCamera && hasLeanTouch && hasLight && !hasStartScene;

        Status("SceneScope component", hasScope);
        Status("Data assets assigned", hasData);
        Status("Camera assigned", hasCamera);
        Status("LeanTouch in scene (required for input)", hasLeanTouch);
        Status("Light in scene", hasLight);
        Status("Play mode start scene cleared", !hasStartScene);

        EditorGUILayout.Space(4);

        if (ready)
        {
            EditorGUILayout.HelpBox("Scene is ready.", MessageType.Info);
            return;
        }

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Set Up Scene", GUILayout.Height(28)))
                SetUpScene();
        }

        EditorGUILayout.HelpBox(
            "Set Up Scene adds the SceneScope, fills its Data list from Data Container Base, creates a " +
            "camera, a LeanTouch object and a directional light, and clears the play mode start scene.",
            MessageType.Warning);

        return;

        void Status(string label, bool ok)
        {
            EditorGUILayout.LabelField(ok ? "✔  " + label : "✘  " + label);
        }
    }

    private void SetUpScene()
    {
        var scene = Sandbox.gameObject.scene;

        var scope = FindScope();
        if (scope == null)
        {
            scope = Undo.AddComponent<SceneScope>(Sandbox.gameObject);
            Debug.Log("<b>Level Sandbox</b>: added SceneScope.", scope);
        }

        var so = new SerializedObject(scope);

        // Data — same source of truth DataInstaller used at run time.
        var dataProperty = so.FindProperty("_data");
        if (dataProperty.arraySize == 0)
        {
            var container = AssetDatabase.LoadAssetAtPath<DataContainer>(DataContainerPath);
            if (container == null || container.Collection == null)
            {
                Debug.LogError($"<b>Level Sandbox</b>: could not load {DataContainerPath}.");
            }
            else
            {
                var assets = container.Collection.Where(x => x != null).ToArray();
                dataProperty.arraySize = assets.Length;
                for (var i = 0; i < assets.Length; i++)
                    dataProperty.GetArrayElementAtIndex(i).objectReferenceValue = assets[i];
                Debug.Log($"<b>Level Sandbox</b>: filled Data list with {assets.Length} assets from Data Container Base.");
            }
        }

        // Camera.
        var cameraProperty = so.FindProperty("_camera");
        if (cameraProperty.objectReferenceValue == null)
        {
            var camera = Object.FindObjectOfType<Camera>();
            if (camera == null)
            {
                var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                go.transform.SetPositionAndRotation(new Vector3(0f, 20f, -15f), Quaternion.Euler(50f, 0f, 0f));
                Undo.RegisterCreatedObjectUndo(go, "Set Up Scene");
                camera = go.GetComponent<Camera>();
                Debug.Log("<b>Level Sandbox</b>: created a camera. Reposition it to frame your level.", go);
            }

            cameraProperty.objectReferenceValue = camera;
        }

        so.ApplyModifiedProperties();

        // LeanTouch — normally lives on the Monitors UI prefab, which the sandbox never creates.
        if (!scene.GetRootGameObjects().Any(x => x.GetComponentInChildren<LeanTouch>(true) != null))
        {
            var go = new GameObject("Lean Touch", typeof(LeanTouch));
            Undo.RegisterCreatedObjectUndo(go, "Set Up Scene");
            Debug.Log("<b>Level Sandbox</b>: added LeanTouch (input would silently do nothing without it).", go);
        }

        // Light.
        if (!scene.GetRootGameObjects().Any(x => x.GetComponentInChildren<Light>(true) != null))
        {
            var go = new GameObject("Directional Light", typeof(Light));
            go.GetComponent<Light>().type = LightType.Directional;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Undo.RegisterCreatedObjectUndo(go, "Set Up Scene");
        }

        if (EditorSceneManager.playModeStartScene != null)
        {
            EditorSceneManager.playModeStartScene = null;
            Prefs.StartSceneBootstrap.Set(false);
            Debug.Log("<b>Level Sandbox</b>: cleared the play mode start scene.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
    }

    // --------------------------------------------------------------- generate

    private void DrawGenerateButtons()
    {
        EditorGUILayout.LabelField("Generate", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || _busy))
        {
            using (new EditorGUI.DisabledScope(HasLevel))
            {
                if (GUILayout.Button("Spawn Level", GUILayout.Height(28)))
                    SpawnLevel();
            }

            using (new EditorGUI.DisabledScope(!HasLevel))
            {
                if (GUILayout.Button("Clear Level"))
                    ClearLevel();

                if (GUILayout.Button("Apply Carrier Modes"))
                    ApplyCarrierModes();

                if (GUILayout.Button("Frame Camera On Level"))
                    FrameCamera();
            }
        }

        if (_busy)
            EditorGUILayout.HelpBox("Baking sheets...", MessageType.Info);
        else if (!HasLevel)
            EditorGUILayout.HelpBox("No level generated yet. Press Spawn Level.", MessageType.Warning);
    }

    private async void SpawnLevel()
    {
        if (HasLevel)
        {
            Debug.LogError("<b>Level Sandbox</b>: a level is already generated. Press Clear Level first.", Sandbox);
            return;
        }

        var scope = FindScope();
        if (scope == null)
        {
            Debug.LogError("<b>Level Sandbox</b>: no SceneScope. Press Set Up Scene first.", Sandbox);
            return;
        }

        if (!ResolveDataAssets(scope, out var assets)) return;

        _busy = true;
        try
        {
            _sheets = await LevelSandboxGenerator.BakeSheets();
        }
        finally
        {
            _busy = false;
        }

        if (_sheets == null)
        {
            Debug.LogError("<b>Level Sandbox</b>: failed to bake sheets, see errors above.");
            return;
        }

        if (target == null) return; // inspector went away while baking

        var result = LevelSandboxGenerator.Generate(_sheets, _levelProperty.intValue, scope, Sandbox,
            assets.Colors, assets.ConveyorConfig, assets.CarrierConfig, assets.Carriers, assets.Blocks);

        if (!result.Ok) return;

        Undo.RegisterCreatedObjectUndo(result.Root, "Spawn Level");
        Selection.activeGameObject = result.Root;
        serializedObject.Update();

        FrameCamera();
    }

    /// <summary>
    /// Reshapes the generated carriers to match the Mode each one is set to: Start is filled up,
    /// Empty is cleared out, Default is left alone. Only block counts change — the colours are the
    /// scene view's, and SceneScope rerolls them every time you press Play.
    /// </summary>
    private void ApplyCarrierModes()
    {
        var carriersRoot = Sandbox.CarriersRoot;
        if (carriersRoot == null)
        {
            Debug.LogError("<b>Level Sandbox</b>: no carriers root. Press Spawn Level first.", Sandbox);
            return;
        }

        var scope = FindScope();
        if (scope == null)
        {
            Debug.LogError("<b>Level Sandbox</b>: no SceneScope. Press Set Up Scene first.", Sandbox);
            return;
        }

        if (!ResolveDataAssets(scope, out var assets)) return;

        var palette = ResolveFillPalette(scope, assets.Colors, carriersRoot);
        if (palette.Count == 0)
        {
            Debug.LogError("<b>Level Sandbox</b>: no colors to fill with. Add entries to SceneScope's " +
                           "Block Colors, or generate a level that has some.", Sandbox);
            return;
        }

        // Every block created and destroyed below registers its own undo step; collapsing them makes
        // one Ctrl+Z put the level back.
        var undoGroup = Undo.GetCurrentGroup();

        // Same stub defaults the generator runs with, so a filled carrier matches a generated one.
        var physicsType = new BlockPhysicsConfig().Type;
        var carrierBlockArgs = Sandbox.CarrierBlockArgs;
        var blockSize = LevelGeometry.GetBlockSize(carrierBlockArgs);
        var groupBlockCount = LevelGeometry.GetGroupBlockCount(assets.CarrierConfig, physicsType, carrierBlockArgs);
        var configSize = assets.CarrierConfig.Sizes[physicsType];

        var filled = 0;
        var emptied = 0;
        var untouched = 0;

        foreach (var carrier in carriersRoot.GetComponentsInChildren<Carrier>(true))
        {
            switch (carrier.Mode)
            {
                case CarrierMode.Start:
                    LevelSandboxGenerator.FillCarrier(carrier, assets.Blocks, assets.Colors, palette,
                        blockSize, groupBlockCount, configSize);
                    filled++;
                    break;

                case CarrierMode.Empty:
                    LevelSandboxGenerator.ClearCarrierBlocks(carrier);
                    emptied++;
                    break;

                default:
                    untouched++;
                    break;
            }

            // Outside the switch: every carrier either takes the sink tint or gets its head put back.
            // AdditionalModels are painted to match inside ApplyCarrierHeadColor.
            LevelSandboxGenerator.ApplyCarrierHeadColor(carrier, assets.Colors);
        }

        Undo.SetCurrentGroupName("Apply Carrier Modes");
        Undo.CollapseUndoOperations(undoGroup);

        EditorSceneManager.MarkSceneDirty(Sandbox.gameObject.scene);

        Debug.Log($"<b>Level Sandbox</b>: applied carrier modes — {filled} filled, {emptied} emptied, " +
                  $"{untouched} left as generated.", Sandbox);
    }

    /// <summary>
    /// SceneScope's Block Colors when it has any, otherwise the colours the level was generated with,
    /// so the button still works before that list is filled in. Colours with no material in the Colors
    /// asset are dropped — they would render as missing.
    /// </summary>
    private static List<ColorType> ResolveFillPalette(SceneScope scope, Colors colors, Transform carriersRoot)
    {
        var palette = new List<ColorType>();

        foreach (var colorType in scope.BlockColors)
        {
            if (colors.Get(colorType).Material == null)
            {
                Debug.LogWarning($"<b>Level Sandbox</b>: Block Colors entry {colorType} has no entry in " +
                                 "the Colors asset, skipping it.", scope);
                continue;
            }

            if (!palette.Contains(colorType)) palette.Add(colorType);
        }

        if (palette.Count > 0) return palette;

        foreach (var block in carriersRoot.GetComponentsInChildren<Block>(true))
        {
            if (block.ColorType.Base == BaseColor.None) continue;
            if (palette.Contains(block.ColorType)) continue;
            if (colors.Get(block.ColorType).Material == null) continue;
            palette.Add(block.ColorType);
        }

        return palette;
    }

    private void ClearLevel()
    {
        var conveyor = _conveyorProperty.objectReferenceValue as Conveyor;
        var levelRoot = conveyor != null && conveyor.transform.parent != null
            ? conveyor.transform.parent.gameObject
            : null;

        // Null the references before destroying what they point at.
        serializedObject.Update();
        _conveyorProperty.objectReferenceValue = null;
        foreach (var name in new[] { "_carriersRoot", "_slotsRoot", "_triggersRoot" })
        {
            var property = serializedObject.FindProperty(name);
            if (property != null) property.objectReferenceValue = null;
        }
        serializedObject.ApplyModifiedProperties();

        var scope = FindScope();
        if (scope != null) scope.SetSceneReferences(null, null, null);

        if (levelRoot != null) Undo.DestroyObjectImmediate(levelRoot);

        EditorSceneManager.MarkSceneDirty(Sandbox.gameObject.scene);
    }

    /// <summary>Pulls the camera back far enough to see the whole generated level.</summary>
    private void FrameCamera()
    {
        var scope = FindScope();
        if (scope == null || scope.Camera == null) return;

        var conveyor = _conveyorProperty.objectReferenceValue as Conveyor;
        if (conveyor == null) return;

        var levelRoot = conveyor.transform.parent;
        if (levelRoot == null) return;

        var renderers = levelRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        var camera = scope.Camera;
        var size = bounds.extents.magnitude;
        var distance = size / Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);

        var direction = Quaternion.Euler(50f, 0f, 0f) * Vector3.forward;
        Undo.RecordObject(camera.transform, "Frame Camera");
        camera.transform.position = bounds.center - direction * distance;
        camera.transform.rotation = Quaternion.LookRotation(direction);

        EditorSceneManager.MarkSceneDirty(Sandbox.gameObject.scene);
    }

    // ------------------------------------------------------------ empty carrier rows

    /// <summary>
    /// Separate from level generation: spawns/respawns rows of empty carriers under hand-placed row
    /// parent transforms, for editing an already-generated level. Every press clears whatever this
    /// button previously generated and regenerates from the current settings.
    /// </summary>
    private void DrawEmptyCarrierRows()
    {
        EditorGUILayout.LabelField("Empty Carrier Rows", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || _busy))
        {
            if (GUILayout.Button("Generate Empty Carrier Rows", GUILayout.Height(28)))
                GenerateEmptyCarrierRows();
        }

        EditorGUILayout.HelpBox(
            "Spawns evenly spaced empty carriers under each Empty Carrier Row Parent, adds them to " +
            "SceneScope's Empty Carriers list, and records their row order in Empty Carrier Rows. " +
            "Press again to clear the previous rows and respawn. Enable SceneScope's Use Empty Carrier " +
            "Rows toggle to make rows fill front-to-back at runtime. Enable Use Custom Empty Carrier " +
            "Prefab to spawn from Custom Empty Carrier Prefab instead of the Carriers data's default.",
            MessageType.None);
    }

    private void GenerateEmptyCarrierRows()
    {
        var scope = FindScope();
        if (scope == null)
        {
            Debug.LogError("<b>Level Sandbox</b>: no SceneScope. Press Set Up Scene first.", Sandbox);
            return;
        }

        if (!ResolveDataAssets(scope, out var assets)) return;

        Carrier carrierPrefab;
        if (Sandbox.UseCustomEmptyCarrierPrefab)
        {
            carrierPrefab = Sandbox.CustomEmptyCarrierPrefab;
            if (carrierPrefab == null)
            {
                Debug.LogError("<b>Level Sandbox</b>: Use Custom Empty Carrier Prefab is on but no " +
                               "Custom Empty Carrier Prefab is assigned.", Sandbox);
                return;
            }
        }
        else
        {
            carrierPrefab = assets.Carriers.Get(FeatureType.None).Prefab;
            if (carrierPrefab == null)
            {
                Debug.LogError("<b>Level Sandbox</b>: Carriers data has no prefab for FeatureType.None.", Sandbox);
                return;
            }
        }

        var rowParents = Sandbox.EmptyCarrierRowParents;
        if (rowParents == null || !rowParents.Any(x => x != null))
        {
            Debug.LogError("<b>Level Sandbox</b>: assign at least one Empty Carrier Row Parent.", Sandbox);
            return;
        }

        if (Sandbox.EmptyCarriersPerRow <= 0)
        {
            Debug.LogError("<b>Level Sandbox</b>: Empty Carriers Per Row must be greater than zero.", Sandbox);
            return;
        }

        if (scope.BlockColors.Count == 0)
        {
            Debug.LogError("<b>Level Sandbox</b>: SceneScope has no Block Colors to randomize a " +
                           "Compatible Color from. Add entries to Block Colors first.", scope);
            return;
        }

        var undoGroup = Undo.GetCurrentGroup();

        var existing = LevelSandboxGenerator.FindRowCarriers(rowParents);
        RemoveFromEmptyCarriers(scope, existing);
        LevelSandboxGenerator.DestroyCarriers(existing);

        var created = LevelSandboxGenerator.GenerateEmptyCarrierRows(rowParents, Sandbox.EmptyCarriersPerRow,
            Sandbox.EmptyCarrierSpacing, Sandbox.EmptyCarrierPositiveZ, Sandbox.EmptyCarrierScale,
            Sandbox.EmptyCarrierGroupLimit, scope.BlockColors, carrierPrefab);
        AddToEmptyCarriers(scope, created);
        UpdateEmptyCarrierRows(scope, existing, created);

        Undo.SetCurrentGroupName("Generate Empty Carrier Rows");
        Undo.CollapseUndoOperations(undoGroup);

        EditorSceneManager.MarkSceneDirty(Sandbox.gameObject.scene);

        Debug.Log($"<b>Level Sandbox</b>: generated {created.Count} empty carriers across " +
                  $"{rowParents.Count(x => x != null)} row(s), removed {existing.Count} previous.", Sandbox);
    }

    /// <summary>
    /// Removing an object reference array element takes two steps in Unity: the first
    /// DeleteArrayElementAtIndex call on a non-null reference only nulls it out, and only a second
    /// call on an already-null element actually shrinks the array. Nulling first here does both in one pass.
    /// </summary>
    private static void RemoveFromEmptyCarriers(SceneScope scope, IEnumerable<Carrier> toRemove)
    {
        var so = new SerializedObject(scope);
        var property = so.FindProperty("_emptyCarriers");

        var removeSet = new HashSet<Carrier>(toRemove);

        for (var i = property.arraySize - 1; i >= 0; i--)
        {
            var element = property.GetArrayElementAtIndex(i);
            var carrier = element.objectReferenceValue as Carrier;
            if (carrier != null && !removeSet.Contains(carrier)) continue;

            element.objectReferenceValue = null;
            property.DeleteArrayElementAtIndex(i);
        }

        so.ApplyModifiedProperties();
    }

    private static void AddToEmptyCarriers(SceneScope scope, IReadOnlyList<Carrier> toAdd)
    {
        if (toAdd.Count == 0) return;

        var so = new SerializedObject(scope);
        var property = so.FindProperty("_emptyCarriers");

        var start = property.arraySize;
        property.arraySize += toAdd.Count;
        for (var i = 0; i < toAdd.Count; i++)
            property.GetArrayElementAtIndex(start + i).objectReferenceValue = toAdd[i];

        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// Keeps SceneScope's Empty Carrier Rows in sync with what this button just did: drops any row
    /// that referenced a carrier just destroyed, then appends one fresh row per row parent, grouping
    /// the newly created carriers by their (row parent) transform.parent — already front-to-back,
    /// since that's the order LevelSandboxGenerator.GenerateEmptyCarrierRows creates them in.
    /// </summary>
    private static void UpdateEmptyCarrierRows(SceneScope scope, IEnumerable<Carrier> removed, IReadOnlyList<Carrier> created)
    {
        var so = new SerializedObject(scope);
        var rowsProperty = so.FindProperty("_emptyCarrierRows");

        var removeSet = new HashSet<Carrier>(removed);

        for (var i = rowsProperty.arraySize - 1; i >= 0; i--)
        {
            var carriersProperty = rowsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("Carriers");
            var isStale = false;
            for (var j = 0; j < carriersProperty.arraySize; j++)
            {
                var carrier = carriersProperty.GetArrayElementAtIndex(j).objectReferenceValue as Carrier;
                if (carrier != null && !removeSet.Contains(carrier)) continue;
                isStale = true;
                break;
            }

            if (isStale) rowsProperty.DeleteArrayElementAtIndex(i);
        }

        foreach (var group in created.GroupBy(c => c.transform.parent))
        {
            var groupCarriers = group.ToList();

            var rowIndex = rowsProperty.arraySize;
            rowsProperty.arraySize++;

            var carriersProperty = rowsProperty.GetArrayElementAtIndex(rowIndex).FindPropertyRelative("Carriers");
            carriersProperty.arraySize = groupCarriers.Count;
            for (var i = 0; i < groupCarriers.Count; i++)
                carriersProperty.GetArrayElementAtIndex(i).objectReferenceValue = groupCarriers[i];
        }

        so.ApplyModifiedProperties();
    }

    // ------------------------------------------------------------------ data

    private struct DataAssets
    {
        public Colors Colors;
        public ConveyorConfig ConveyorConfig;
        public CarrierConfig CarrierConfig;
        public Carriers Carriers;
        public Blocks Blocks;
    }

    private static List<Data> GetDataList(SceneScope scope)
    {
        var result = new List<Data>();
        var so = new SerializedObject(scope);
        var dataProperty = so.FindProperty("_data");
        if (dataProperty == null) return result;

        for (var i = 0; i < dataProperty.arraySize; i++)
            if (dataProperty.GetArrayElementAtIndex(i).objectReferenceValue is Data data)
                result.Add(data);

        return result;
    }

    private static bool ResolveDataAssets(SceneScope scope, out DataAssets assets)
    {
        assets = new DataAssets();

        foreach (var data in GetDataList(scope))
        {
            switch (data)
            {
                case Colors x: assets.Colors = x; break;
                case ConveyorConfig x: assets.ConveyorConfig = x; break;
                case CarrierConfig x: assets.CarrierConfig = x; break;
                case Carriers x: assets.Carriers = x; break;
                case Blocks x: assets.Blocks = x; break;
            }
        }

        var missing = new List<string>();
        if (assets.Colors == null) missing.Add(nameof(Colors));
        if (assets.ConveyorConfig == null) missing.Add(nameof(ConveyorConfig));
        if (assets.CarrierConfig == null) missing.Add(nameof(CarrierConfig));
        if (assets.Carriers == null) missing.Add(nameof(Carriers));
        if (assets.Blocks == null) missing.Add(nameof(Blocks));

        if (missing.Count == 0) return true;

        Debug.LogError($"<b>Level Sandbox</b>: the SceneScope Data list is missing {string.Join(", ", missing)}. " +
                       "Press Set Up Scene to fill it automatically.", scope);
        return false;
    }

    private SceneScope FindScope()
    {
        var scope = Sandbox.GetComponent<SceneScope>();
        if (scope != null) return scope;

        var scene = Sandbox.gameObject.scene;
        if (!scene.IsValid()) return null;

        return scene.GetRootGameObjects()
            .Select(x => x.GetComponentInChildren<SceneScope>(true))
            .FirstOrDefault(x => x != null);
    }
}

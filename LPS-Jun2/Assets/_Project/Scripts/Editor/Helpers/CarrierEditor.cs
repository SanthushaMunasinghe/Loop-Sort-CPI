using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for <see cref="Carrier"/>: the usual fields, plus a Default Fill section that refills
/// this one carrier to its Default Group Count — the per-carrier counterpart to the Sandbox's Apply
/// Carrier Modes, so changing one carrier's count doesn't mean refilling the whole level.
///
/// Also says outright whether anything will actually feed this carrier at run time — a Default
/// carrier only takes blocks back in through a CarrierBlockTrigger of its own, and only while it is
/// in Scene Scope's Default Carriers list — since both are hand-wired and easy to miss.
/// </summary>
[CustomEditor(typeof(Carrier))]
[CanEditMultipleObjects]
public sealed class CarrierEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        DrawDefaultFill();
    }

    private void DrawDefaultFill()
    {
        EditorGUILayout.LabelField("Default Fill", EditorStyles.boldLabel);

        var carriers = targets.OfType<Carrier>().ToList();
        var defaultCarriers = carriers.Where(x => x.Mode == CarrierMode.Default).ToList();

        if (carriers.Count == 1) DrawStatus(carriers[0]);

        using (new EditorGUI.DisabledScope(defaultCarriers.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            var label = defaultCarriers.Count > 1
                ? $"Apply Default Group Count ({defaultCarriers.Count} carriers)"
                : "Apply Default Group Count";

            if (GUILayout.Button(label, GUILayout.Height(28)))
            {
                LevelSandboxEditor.ApplyDefaultGroupCount(defaultCarriers);
                SceneView.RepaintAll();
                GUIUtility.ExitGUI();
            }
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            EditorGUILayout.HelpBox("Edit mode only — the fill is saved into the scene.", MessageType.None);
        else if (defaultCarriers.Count == 0)
            EditorGUILayout.HelpBox("Set Mode to Default to use this.", MessageType.None);
        else
            EditorGUILayout.HelpBox(
                "Grows or shrinks Group Blocks to exactly Default Group Count and refills the blocks to " +
                "match. Ctrl+Z undoes it in one step.", MessageType.None);
    }

    private static void DrawStatus(Carrier carrier)
    {
        if (carrier.Mode != CarrierMode.Default) return;

        var filledGroups = 0;
        var blockCount = 0;
        if (carrier.BlockParent != null)
        {
            for (var i = 0; i < carrier.BlockParent.childCount; i++)
                if (carrier.BlockParent.GetChild(i).TryGetComponent<CarrierBlockGroupParent>(out _))
                    filledGroups++;

            var blocks = new List<Block>();
            Carrier.GetAuthoredBlocksInOrder(carrier.BlockParent, blocks);
            blockCount = blocks.Count;
        }

        var groupCount = carrier.GetDefaultFillGroupCount();
        var slotCount = carrier.GroupBlocks != null ? carrier.GroupBlocks.Count : 0;
        EditorGUILayout.LabelField($"Holds {filledGroups} groups ({blockCount} blocks), {slotCount} Group Blocks slots");

        if (filledGroups != groupCount && !EditorApplication.isPlaying)
            EditorGUILayout.HelpBox($"Default Group Count is {groupCount} but the carrier holds {filledGroups} " +
                                    "groups. Press Apply Default Group Count to refill it.", MessageType.Warning);

        DrawRunTimeWiring(carrier);
    }

    /// <summary>What decides whether this carrier takes blocks back in once the level is playing.</summary>
    private static void DrawRunTimeWiring(Carrier carrier)
    {
        var scope = carrier.gameObject.scene.GetRootGameObjects()
            .Select(x => x.GetComponentInChildren<SceneScope>(true))
            .FirstOrDefault(x => x != null);

        if (scope != null && (!scope.UseDefaultCarriers || !scope.DefaultCarriers.Contains(carrier)))
            EditorGUILayout.HelpBox("Not running: Scene Scope needs Use Default Carriers on and this carrier " +
                                    "in its Default Carriers list.", MessageType.Warning);

        var triggers = Object.FindObjectsByType<CarrierBlockTrigger>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(x => x.Carrier == carrier && x.gameObject.scene == carrier.gameObject.scene)
            .ToList();

        var activeTriggers = triggers.Count(x => x.gameObject.activeInHierarchy &&
                                                 x.TryGetComponent<Collider>(out var c) && c.enabled);

        if (activeTriggers > 0) return;

        EditorGUILayout.HelpBox(triggers.Count == 0
            ? "No CarrierBlockTrigger points at this carrier, so blocks on the conveyor ride straight past it."
            : "Every CarrierBlockTrigger pointing at this carrier is inactive, so blocks on the conveyor ride " +
              "straight past it.", MessageType.Warning);
    }
}

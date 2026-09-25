using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-shot builder for the grocery setup. Safe to run again — every step finds what an earlier run
/// made instead of making it twice.
///
/// 1. Shopping Cart.prefab — a copy of Carrier.prefab with its cube group blocks and truck lid removed,
///    the ShoppingCart-long-1 model wrapped in "Cart Model" (keeping the Carrier prefab's transform for
///    it) and used as the head renderer, and a ShoppingCart component.
/// 2. Grocery Item.prefab — a copy of Block.prefab without its colour registry, with a Model Root child
///    and a GroceryItem component.
/// 3. The open scene — every SceneScope Default carrier is swapped for a Shopping Cart at the same spot,
///    each of its CarrierBlockTriggers gets a matching GroceryTrigger, every active CarrierBlockTrigger
///    and the old carriers are switched off, and SceneScope is set to run on the carts.
/// </summary>
public static class ShoppingCartSetup
{
    private const string CarrierPrefabPath = "Assets/_Project/Prefabs/Carriers/Carrier.prefab";
    private const string CartPrefabPath = "Assets/_Project/Prefabs/Carriers/Shopping Cart.prefab";
    private const string BlockPrefabPath = "Assets/_Project/Prefabs/Blocks/Block.prefab";
    private const string GroceryFolder = "Assets/_Project/Prefabs/Grocery";
    private const string GroceryPrefabPath = GroceryFolder + "/Grocery Item.prefab";

    private const string CartModelName = "ShoppingCart-long-1";
    private const string CartModelWrapperName = "Cart Model";

    [MenuItem("Tools/Grocery/Build Shopping Cart Setup", priority = -97)]
    public static void BuildAll()
    {
        var log = new StringBuilder();
        var cartPrefab = BuildCartPrefab(log);
        var groceryPrefab = BuildGroceryItemPrefab(log);
        if (cartPrefab != null && groceryPrefab != null) SetUpScene(cartPrefab, groceryPrefab, log);
        Debug.Log($"<b>{nameof(ShoppingCartSetup)}</b>\n{log}");
    }

    private static GameObject BuildCartPrefab(StringBuilder log)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CartPrefabPath) == null)
        {
            if (!AssetDatabase.CopyAsset(CarrierPrefabPath, CartPrefabPath))
            {
                log.AppendLine($"Could not copy {CarrierPrefabPath}.");
                return null;
            }

            log.AppendLine($"Created {CartPrefabPath}.");
        }

        var root = PrefabUtility.LoadPrefabContents(CartPrefabPath);
        try
        {
            root.name = "Shopping Cart";
            var carrier = root.GetComponent<Carrier>();
            var so = new SerializedObject(carrier);

            // Wrap the cart model so ApplyEndTransferMotion's HeadRenderer scale reset hits the fbx's own
            // unit scale rather than the 4.1 the Carrier prefab gives it.
            var model = FindDeep(root.transform, CartModelName);
            if (model == null)
            {
                log.AppendLine($"No '{CartModelName}' in {CarrierPrefabPath} — cart prefab left unfinished.");
                return null;
            }

            if (model.parent.name != CartModelWrapperName)
            {
                var wrapper = new GameObject(CartModelWrapperName).transform;
                wrapper.SetParent(model.parent, false);
                wrapper.SetSiblingIndex(model.GetSiblingIndex());
                wrapper.localPosition = model.localPosition;
                wrapper.localRotation = model.localRotation;
                wrapper.localScale = model.localScale;
                wrapper.gameObject.layer = model.gameObject.layer;

                model.SetParent(wrapper, false);
                model.localPosition = Vector3.zero;
                model.localRotation = Quaternion.identity;
                model.localScale = Vector3.one;
            }

            var cartRenderer = model.GetComponentInChildren<MeshRenderer>(true);

            // Cube group blocks.
            var groupBlocks = so.FindProperty("<GroupBlocks>k__BackingField");
            var groupContainers = new HashSet<Transform>();
            for (var i = 0; i < groupBlocks.arraySize; i++)
            {
                var r = groupBlocks.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
                if (r == null) continue;
                groupContainers.Add(r.transform.parent);
                RemoveObject(root, r.gameObject);
            }

            groupBlocks.ClearArray();
            so.FindProperty("<GroupBlockFilters>k__BackingField").ClearArray();

            foreach (var container in groupContainers)
                if (container != null && container != root.transform && container.childCount == 0 &&
                    container != carrier.BlockParent)
                    Object.DestroyImmediate(container.gameObject);

            // Old truck head and lid.
            var oldHead = so.FindProperty("<HeadRenderer>k__BackingField").objectReferenceValue as Renderer;
            var backTop = so.FindProperty("<BackTopRenderer>k__BackingField").objectReferenceValue as Renderer;
            var backRear = so.FindProperty("<BackRearRenderer>k__BackingField").objectReferenceValue as Renderer;

            so.FindProperty("<HeadRenderer>k__BackingField").objectReferenceValue = cartRenderer;
            so.FindProperty("<BackTopRenderer>k__BackingField").objectReferenceValue = null;
            so.FindProperty("<BackRearRenderer>k__BackingField").objectReferenceValue = null;
            so.FindProperty("<AdditionalModels>k__BackingField").ClearArray();
            so.FindProperty("<Mode>k__BackingField").enumValueIndex = (int)CarrierMode.Default;
            so.FindProperty("<DefaultGroupCount>k__BackingField").intValue = 4;
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (var old in new[] { backTop, backRear, oldHead })
                if (old != null && old != cartRenderer)
                    RemoveObject(root, old.gameObject);

            // Colour registry: only the cart itself.
            foreach (var registry in root.GetComponents<RendererPropertyRegistry>())
            {
                var rso = new SerializedObject(registry);
                var renderers = rso.FindProperty("Renderers");
                renderers.ClearArray();
                renderers.InsertArrayElementAtIndex(0);
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = cartRenderer;
                rso.ApplyModifiedPropertiesWithoutUndo();
            }

            if (!root.TryGetComponent<ShoppingCart>(out _)) root.AddComponent<ShoppingCart>();

            PrefabUtility.SaveAsPrefabAsset(root, CartPrefabPath);
            log.AppendLine($"Built {CartPrefabPath} (head = {cartRenderer?.name}).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(CartPrefabPath);
    }

    private static GameObject BuildGroceryItemPrefab(StringBuilder log)
    {
        if (!AssetDatabase.IsValidFolder(GroceryFolder))
            AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Grocery");

        if (AssetDatabase.LoadAssetAtPath<GameObject>(GroceryPrefabPath) == null)
        {
            if (!AssetDatabase.CopyAsset(BlockPrefabPath, GroceryPrefabPath))
            {
                log.AppendLine($"Could not copy {BlockPrefabPath}.");
                return null;
            }

            log.AppendLine($"Created {GroceryPrefabPath}.");
        }

        var root = PrefabUtility.LoadPrefabContents(GroceryPrefabPath);
        try
        {
            root.name = "Grocery Item";

            foreach (var registry in root.GetComponentsInChildren<RendererPropertyRegistry>(true))
                Object.DestroyImmediate(registry);

            var modelRoot = root.transform.Find("Model Root");
            if (modelRoot == null)
            {
                modelRoot = new GameObject("Model Root").transform;
                modelRoot.SetParent(root.transform, false);
                modelRoot.gameObject.layer = root.layer;
            }

            if (!root.TryGetComponent<GroceryItem>(out var item)) item = root.AddComponent<GroceryItem>();
            var so = new SerializedObject(item);
            so.FindProperty("_modelRoot").objectReferenceValue = modelRoot;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, GroceryPrefabPath);
            log.AppendLine($"Built {GroceryPrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(GroceryPrefabPath);
    }

    private static void SetUpScene(GameObject cartPrefab, GameObject groceryPrefab, StringBuilder log)
    {
        var sceneScope = Object.FindObjectOfType<SceneScope>(true);
        if (sceneScope == null)
        {
            log.AppendLine("No SceneScope in the open scene — scene left untouched.");
            return;
        }

        var scene = sceneScope.gameObject.scene;
        var scopeSo = new SerializedObject(sceneScope);
        var defaultCarriers = scopeSo.FindProperty("_defaultCarriers");
        var carrierTriggers = Object.FindObjectsByType<CarrierBlockTrigger>(FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        var carts = new List<Carrier>();
        for (var i = 0; i < defaultCarriers.arraySize; i++)
        {
            var old = defaultCarriers.GetArrayElementAtIndex(i).objectReferenceValue as Carrier;
            if (old == null || old.IsShoppingCart || old.GetComponent<ShoppingCart>() != null) continue;

            var suffix = old.name.StartsWith("Carrier") ? old.name.Substring("Carrier".Length) : " " + old.name;
            var cart = FindOrCreateCart(cartPrefab, old, "Shopping Cart" + suffix, log);
            carts.Add(cart);

            foreach (var trigger in carrierTriggers.Where(t => t.Carrier == old))
                FindOrCreateGroceryTrigger(trigger, cart, "Grocery Trigger" + suffix, log);

            if (old.gameObject.activeSelf)
            {
                Undo.RecordObject(old.gameObject, "Disable default carrier");
                old.gameObject.SetActive(false);
                log.AppendLine($"Disabled {old.name}.");
            }
        }

        foreach (var trigger in carrierTriggers)
        {
            if (!trigger.gameObject.activeSelf) continue;
            Undo.RecordObject(trigger.gameObject, "Disable carrier trigger");
            trigger.gameObject.SetActive(false);
            log.AppendLine($"Disabled {trigger.name}.");
        }

        Undo.RecordObject(sceneScope, "Use shopping carts");
        scopeSo.Update();
        scopeSo.FindProperty("_useShoppingCarts").boolValue = true;
        scopeSo.FindProperty("_groceryItemPrefab").objectReferenceValue = groceryPrefab.GetComponent<GroceryItem>();
        if (carts.Count > 0)
        {
            var list = scopeSo.FindProperty("_shoppingCarts");
            list.ClearArray();
            for (var i = 0; i < carts.Count; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = carts[i];
            }
        }

        scopeSo.ApplyModifiedProperties();
        log.AppendLine($"SceneScope: Use Shopping Carts on, {carts.Count} carts, Grocery Item Prefab set.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine($"Saved {scene.path}.");
    }

    private static Carrier FindOrCreateCart(GameObject cartPrefab, Carrier old, string cartName, StringBuilder log)
    {
        var parent = old.transform.parent;
        var existing = parent != null ? parent.Find(cartName) : null;
        if (existing != null && existing.TryGetComponent<Carrier>(out var existingCart)) return existingCart;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(cartPrefab, old.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(instance, "Create shopping cart");
        instance.name = cartName;
        instance.transform.SetParent(parent, false);
        instance.transform.SetSiblingIndex(old.transform.GetSiblingIndex() + 1);
        instance.transform.localPosition = old.transform.localPosition;
        instance.transform.localRotation = old.transform.localRotation;
        instance.transform.localScale = old.transform.localScale;
        log.AppendLine($"Created {cartName} at {old.name}'s transform.");
        return instance.GetComponent<Carrier>();
    }

    private static void FindOrCreateGroceryTrigger(CarrierBlockTrigger source, Carrier cart, string triggerName,
        StringBuilder log)
    {
        var parent = source.transform.parent;
        var existing = parent != null ? parent.Find(triggerName) : null;
        GroceryTrigger groceryTrigger;
        if (existing != null && existing.TryGetComponent(out groceryTrigger))
        {
            Undo.RecordObject(groceryTrigger, "Point grocery trigger");
        }
        else
        {
            var go = new GameObject(triggerName);
            Undo.RegisterCreatedObjectUndo(go, "Create grocery trigger");
            go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            go.transform.localPosition = source.transform.localPosition;
            go.transform.localRotation = source.transform.localRotation;
            go.transform.localScale = source.transform.localScale;
            go.layer = source.gameObject.layer;

            groceryTrigger = go.AddComponent<GroceryTrigger>();

            var sourceBox = source.GetComponent<BoxCollider>();
            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = sourceBox.isTrigger;
            box.center = sourceBox.center;
            box.size = sourceBox.size;

            // Same active state as the trigger it mirrors.
            go.SetActive(source.gameObject.activeSelf);
            log.AppendLine($"Created {triggerName} from {source.name}" +
                           (go.activeSelf ? "." : " (inactive, like its source)."));
        }

        groceryTrigger.SetCart(cart);
        EditorUtility.SetDirty(groceryTrigger);
    }

    /// <summary>Deletes go from the prefab contents — or, when it's part of a nested prefab instance (the
    /// Cube group blocks, the BusWithTop lid), that whole instance, since a part can't go on its own.</summary>
    private static void RemoveObject(GameObject root, GameObject go)
    {
        if (go == null || go == root) return;

        var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
        var target = instanceRoot != null && instanceRoot != root ? instanceRoot : go;
        Object.DestroyImmediate(target);
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindDeep(child, name);
            if (found != null) return found;
        }

        return null;
    }
}

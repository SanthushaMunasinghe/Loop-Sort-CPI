using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(MirroredImage)), CanEditMultipleObjects]
public class MirroredImageEditor : Editor
{
    private SerializedProperty _spriteProp, _colorProp, _materialProp, _raycastTargetProp;
    private SerializedProperty _mirrorHorizontalProp, _mirrorVerticalProp;
    private GUIContent _spriteLabel;

    private void OnEnable()
    {
        _spriteProp = serializedObject.FindProperty("m_Sprite");
        _colorProp = serializedObject.FindProperty("m_Color");
        _materialProp = serializedObject.FindProperty("m_Material");
        _raycastTargetProp = serializedObject.FindProperty("m_RaycastTarget");
        _mirrorHorizontalProp = serializedObject.FindProperty("m_MirrorHorizontally");
        _mirrorVerticalProp = serializedObject.FindProperty("m_MirrorVertically");
        _spriteLabel = new GUIContent("Sprite");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_spriteProp, _spriteLabel);
        EditorGUILayout.PropertyField(_colorProp);
        EditorGUILayout.PropertyField(_materialProp);
        EditorGUILayout.PropertyField(_raycastTargetProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mirroring", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_mirrorHorizontalProp, new GUIContent("Mirror Horizontally"));
        EditorGUILayout.PropertyField(_mirrorVerticalProp, new GUIContent("Mirror Vertically"));

        var h = _mirrorHorizontalProp.boolValue;
        var v = _mirrorVerticalProp.boolValue;
        if (h && v)
            EditorGUILayout.HelpBox("Quarter mirror: only the bottom-left corner of the sprite is used.", MessageType.Info);
        else if (h)
            EditorGUILayout.HelpBox("Half mirror: only the left half of the sprite is used.", MessageType.Info);
        else if (v)
            EditorGUILayout.HelpBox("Half mirror: only the bottom half of the sprite is used.", MessageType.Info);

        EditorGUILayout.Space();
        var useSlicingProp = serializedObject.FindProperty("m_UseSlicing");
        EditorGUILayout.PropertyField(useSlicingProp, new GUIContent("Use 9-Slice"));

        if (useSlicingProp.boolValue)
        {
            var img = (MirroredImage)target;
            if (img.sprite != null && img.sprite.border.sqrMagnitude == 0f)
                EditorGUILayout.HelpBox("Sprite has no border data. Configure borders in the Sprite Editor for 9-slice to work.", MessageType.Warning);

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("m_FillCenter"),
                new GUIContent("Fill Center"));
            var ppuProp = serializedObject.FindProperty("m_PixelsPerUnitMultiplier");
            EditorGUILayout.PropertyField(ppuProp, new GUIContent("Pixels Per Unit Multiplier"));
            ppuProp.floatValue = Mathf.Max(0.01f, ppuProp.floatValue);
            EditorGUI.indentLevel--;
        }
        else
        {
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("m_PreserveAspect"),
                new GUIContent("Preserve Aspect"));
        }

        serializedObject.ApplyModifiedProperties();
    }

    [MenuItem("GameObject/UI/Image (Mirrored)", false, 2001)]
    private static void AddMirroredImage(MenuCommand menuCommand)
    {
        var parent = Selection.activeGameObject;
        EditorApplication.ExecuteMenuItem("GameObject/UI/Image"); // Create empty UI element.
        var instance = Selection.activeGameObject;
        DestroyImmediate(instance.GetComponent<Image>());
        instance.AddComponent<MirroredImage>();
        instance.transform.SetParent(parent.transform);
        instance.transform.localPosition = Vector3.zero;
    }
}
#endif

[AddComponentMenu("UI/Mirrored Image")]
public class MirroredImage : Image
{
    [SerializeField] private bool m_MirrorHorizontally;

    public bool MirrorHorizontally
    {
        get => m_MirrorHorizontally;
        set
        {
            if (m_MirrorHorizontally != value)
            {
                m_MirrorHorizontally = value;
                SetVerticesDirty();
            }
        }
    }

    [SerializeField] private bool m_MirrorVertically;

    public bool MirrorVertically
    {
        get => m_MirrorVertically;
        set
        {
            if (m_MirrorVertically != value)
            {
                m_MirrorVertically = value;
                SetVerticesDirty();
            }
        }
    }

    [SerializeField] private bool m_UseSlicing;

    public bool UseSlicing
    {
        get => m_UseSlicing;
        set
        {
            if (m_UseSlicing != value)
            {
                m_UseSlicing = value;
                SetVerticesDirty();
            }
        }
    }

    private Sprite ActiveSprite => overrideSprite != null ? overrideSprite : sprite;

    public void SetMirrorMode(bool horizontal, bool vertical)
    {
        if (m_MirrorHorizontally == horizontal && m_MirrorVertically == vertical)
            return;
        m_MirrorHorizontally = horizontal;
        m_MirrorVertically = vertical;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        var sp = ActiveSprite;
        if (sp == null)
            return;

        var rect = GetPixelAdjustedRect();
        var color32 = (Color32)color;
        var hasBorder = sp.border.sqrMagnitude > 0f;

        if (m_UseSlicing && hasBorder)
        {
            PopulateSliced(vh, sp, rect, color32);
        }
        else
        {
            if (preserveAspect)
                rect = FitToAspect(rect, sp);
            PopulateSimple(vh, sp, rect, color32);
        }
    }

    private void PopulateSimple(VertexHelper vh, Sprite sp, Rect rect, Color32 color32)
    {
        var outer = UnityEngine.Sprites.DataUtility.GetOuterUV(sp);

        float[] xPos, yPos, uvX, uvY;

        if (m_MirrorHorizontally)
        {
            var cx = rect.xMin + rect.width * 0.5f;
            xPos = new[] { rect.xMin, cx, rect.xMax };
            uvX = new[] { outer.x, outer.z, outer.x };
        }
        else
        {
            xPos = new[] { rect.xMin, rect.xMax };
            uvX = new[] { outer.x, outer.z };
        }

        if (m_MirrorVertically)
        {
            var cy = rect.yMin + rect.height * 0.5f;
            yPos = new[] { rect.yMin, cy, rect.yMax };
            uvY = new[] { outer.y, outer.w, outer.y };
        }
        else
        {
            yPos = new[] { rect.yMin, rect.yMax };
            uvY = new[] { outer.y, outer.w };
        }

        GenerateGrid(vh, xPos, yPos, uvX, uvY, color32);
    }

    private void PopulateSliced(VertexHelper vh, Sprite sp, Rect rect, Color32 color32)
    {
        var outer = UnityEngine.Sprites.DataUtility.GetOuterUV(sp);
        var inner = UnityEngine.Sprites.DataUtility.GetInnerUV(sp);
        var border = sp.border / pixelsPerUnitMultiplier;

        var availW = m_MirrorHorizontally ? rect.width * 0.5f : rect.width;
        var availH = m_MirrorVertically ? rect.height * 0.5f : rect.height;
        border = ClampBorder(border, availW, availH);

        float[] xPos, yPos, uvX, uvY;

        if (m_MirrorHorizontally)
        {
            var cx = rect.xMin + rect.width * 0.5f;
            xPos = new[]
            {
                rect.xMin, rect.xMin + border.x, cx - border.z, cx,
                cx + border.z, rect.xMax - border.x, rect.xMax
            };
            uvX = new[]
            {
                outer.x, inner.x, inner.z, outer.z,
                inner.z, inner.x, outer.x
            };
        }
        else
        {
            xPos = new[]
            {
                rect.xMin, rect.xMin + border.x,
                rect.xMax - border.z, rect.xMax
            };
            uvX = new[] { outer.x, inner.x, inner.z, outer.z };
        }

        if (m_MirrorVertically)
        {
            var cy = rect.yMin + rect.height * 0.5f;
            yPos = new[]
            {
                rect.yMin, rect.yMin + border.y, cy - border.w, cy,
                cy + border.w, rect.yMax - border.y, rect.yMax
            };
            uvY = new[]
            {
                outer.y, inner.y, inner.w, outer.w,
                inner.w, inner.y, outer.y
            };
        }
        else
        {
            yPos = new[]
            {
                rect.yMin, rect.yMin + border.y,
                rect.yMax - border.w, rect.yMax
            };
            uvY = new[] { outer.y, inner.y, inner.w, outer.w };
        }

        GenerateGrid(vh, xPos, yPos, uvX, uvY, color32, !fillCenter);
    }

    private Rect FitToAspect(Rect rect, Sprite sp)
    {
        var spriteW = sp.rect.width;
        var spriteH = sp.rect.height;
        if (m_MirrorHorizontally) spriteW *= 2f;
        if (m_MirrorVertically) spriteH *= 2f;

        var spriteAspect = spriteW / spriteH;
        var rectAspect = rect.width / rect.height;

        if (spriteAspect > rectAspect)
        {
            var h = rect.width / spriteAspect;
            var offset = (rect.height - h) * 0.5f;
            return new Rect(rect.x, rect.y + offset, rect.width, h);
        }
        else
        {
            var w = rect.height * spriteAspect;
            var offset = (rect.width - w) * 0.5f;
            return new Rect(rect.x + offset, rect.y, w, rect.height);
        }
    }

    private static Vector4 ClampBorder(Vector4 border, float width, float height)
    {
        var totalX = border.x + border.z;
        if (totalX > width && totalX > 0f)
        {
            var s = width / totalX;
            border.x *= s;
            border.z *= s;
        }

        var totalY = border.y + border.w;
        if (totalY > height && totalY > 0f)
        {
            var s = height / totalY;
            border.y *= s;
            border.w *= s;
        }

        return border;
    }

    private static void GenerateGrid(VertexHelper vh, float[] xPos, float[] yPos,
        float[] uvX, float[] uvY, Color32 color32, bool skipCenter = false)
    {
        var cols = xPos.Length;
        var rows = yPos.Length;

        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
                vh.AddVert(new Vector3(xPos[c], yPos[r]), color32, new Vector2(uvX[c], uvY[r]));

        for (var r = 0; r < rows - 1; r++)
        {
            for (var c = 0; c < cols - 1; c++)
            {
                if (Mathf.Approximately(xPos[c], xPos[c + 1]) ||
                    Mathf.Approximately(yPos[r], yPos[r + 1]))
                    continue;

                if (skipCenter && c % 3 == 1 && r % 3 == 1)
                    continue;

                var i = r * cols + c;
                vh.AddTriangle(i, i + cols, i + cols + 1);
                vh.AddTriangle(i + cols + 1, i + 1, i);
            }
        }
    }

    public override void SetNativeSize()
    {
        var sp = ActiveSprite;
        if (sp == null) return;

        var w = sp.rect.width / sp.pixelsPerUnit;
        var h = sp.rect.height / sp.pixelsPerUnit;

        if (m_MirrorHorizontally) w *= 2f;
        if (m_MirrorVertically) h *= 2f;

        rectTransform.anchorMax = rectTransform.anchorMin;
        rectTransform.sizeDelta = new Vector2(w, h);
    }
}
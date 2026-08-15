using UnityEngine;
using TMPro;

/// <summary>
/// A component that transforms TextMeshPro text into a curved/arc shape.
/// The text can be adjusted from completely flat (0 degrees) to a full half-circle (180 degrees).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public class TMP_CurvedText : MonoBehaviour
{
    private const float MinTextWidth = .001f;

    [SerializeField]
    [Range(-180, 180)]
    [Tooltip("The arc angle of the text. 0 = flat text, 180 = half circle")]
    private float _arcDegrees;

    private TMP_Text _tmp;
    private bool _isSubscribed;

    private void Awake()
    {
        CacheTextComponent();
    }

    private void OnEnable()
    {
        CacheTextComponent();
        SubscribeToTextMesh();
        SetTextDirty();
    }

    private void OnDisable()
    {
        UnsubscribeFromTextMesh();
    }

    private void OnDestroy()
    {
        UnsubscribeFromTextMesh();
    }

    private void OnValidate()
    {
        CacheTextComponent();
        SetTextDirty();
    }

    private void CacheTextComponent()
    {
        if (_tmp != null) return;
        TryGetComponent(out _tmp);
    }

    private void SubscribeToTextMesh()
    {
        if (_tmp == null || _isSubscribed)
            return;

        _tmp.OnPreRenderText += ApplyCurve;
        _isSubscribed = true;
    }

    private void UnsubscribeFromTextMesh()
    {
        if (_tmp == null || !_isSubscribed)
            return;

        _tmp.OnPreRenderText -= ApplyCurve;
        _isSubscribed = false;
    }

    private void SetTextDirty()
    {
        if (_tmp == null)
            return;

        _tmp.havePropertiesChanged = true;
    }

    private void ApplyCurve(TMP_TextInfo textInfo)
    {
        if (Mathf.Approximately(_arcDegrees, 0f))
            return;

        var characterCount = textInfo.characterCount;
        if (characterCount == 0)
            return;

        if (!TryGetVisibleTextBounds(textInfo, out var boundsMin, out var boundsMax))
            return;

        var textWidth = boundsMax.x - boundsMin.x;
        if (Mathf.Abs(textWidth) < MinTextWidth)
            return;

        var textCenter = new Vector3(
            (boundsMin.x + boundsMax.x) * .5f,
            (boundsMin.y + boundsMax.y) * .5f,
            0f
        );

        for (var i = 0; i < characterCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible)
                continue;

            var vertexIndex = textInfo.characterInfo[i].vertexIndex;
            var materialIndex = textInfo.characterInfo[i].materialReferenceIndex;
            var vertices = textInfo.meshInfo[materialIndex].vertices;

            Vector3 charMidBaselinePos = new Vector2(
                (vertices[vertexIndex + 0].x + vertices[vertexIndex + 2].x) * .5f,
                textInfo.characterInfo[i].baseLine);

            vertices[vertexIndex + 0] -= charMidBaselinePos;
            vertices[vertexIndex + 1] -= charMidBaselinePos;
            vertices[vertexIndex + 2] -= charMidBaselinePos;
            vertices[vertexIndex + 3] -= charMidBaselinePos;

            var zeroToOnePos = (charMidBaselinePos.x - boundsMin.x) / textWidth;
            var matrix = ComputeCircleTransformationMatrix(zeroToOnePos, textInfo, i, textWidth);

            vertices[vertexIndex + 0] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 0]);
            vertices[vertexIndex + 1] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 1]);
            vertices[vertexIndex + 2] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 2]);
            vertices[vertexIndex + 3] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 3]);
        }

        OffsetVisibleVerticesToCenter(textInfo, textCenter);
    }

    private static bool TryGetVisibleTextBounds(TMP_TextInfo textInfo, out Vector3 boundsMin, out Vector3 boundsMax)
    {
        boundsMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0f);
        boundsMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0f);

        var hasVisibleCharacter = false;

        for (var i = 0; i < textInfo.characterCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible)
                continue;

            hasVisibleCharacter = true;

            var vertexIndex = textInfo.characterInfo[i].vertexIndex;
            var materialIndex = textInfo.characterInfo[i].materialReferenceIndex;
            var vertices = textInfo.meshInfo[materialIndex].vertices;

            for (var j = 0; j < 4; j++)
            {
                var vertex = vertices[vertexIndex + j];
                boundsMin = Vector3.Min(boundsMin, vertex);
                boundsMax = Vector3.Max(boundsMax, vertex);
            }
        }

        return hasVisibleCharacter;
    }

    private static void OffsetVisibleVerticesToCenter(TMP_TextInfo textInfo, Vector3 textCenter)
    {
        var newCenter = Vector3.zero;
        var totalVertices = 0;

        for (var i = 0; i < textInfo.characterCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible)
                continue;

            var vertexIndex = textInfo.characterInfo[i].vertexIndex;
            var materialIndex = textInfo.characterInfo[i].materialReferenceIndex;
            var vertices = textInfo.meshInfo[materialIndex].vertices;

            for (var j = 0; j < 4; j++)
            {
                newCenter += vertices[vertexIndex + j];
                totalVertices++;
            }
        }

        if (totalVertices == 0)
            return;

        newCenter /= totalVertices;
        newCenter.x = 0.0f;

        var offset = textCenter - newCenter;

        for (var i = 0; i < textInfo.characterCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible)
                continue;

            var vertexIndex = textInfo.characterInfo[i].vertexIndex;
            var materialIndex = textInfo.characterInfo[i].materialReferenceIndex;
            var vertices = textInfo.meshInfo[materialIndex].vertices;

            for (var j = 0; j < 4; j++)
            {
                vertices[vertexIndex + j] += offset;
            }
        }
    }

    /// <summary>
    /// Computes the transformation matrix for a character based on its position in the text
    /// </summary>
    /// <param name="zeroToOnePos">The position of the character in the text (0 to 1)</param>
    /// <param name="textInfo">The TextMeshPro text information</param>
    /// <param name="charIdx">The index of the character</param>
    /// <returns>A transformation matrix that positions and rotates the character</returns>
    private Matrix4x4 ComputeCircleTransformationMatrix(float zeroToOnePos, TMP_TextInfo textInfo, int charIdx, float textWidth)
    {
        var radius = textWidth / (2 * Mathf.Sin(_arcDegrees * .5f * Mathf.Deg2Rad));
        var angle = ((zeroToOnePos - .5f) * _arcDegrees - 90) * Mathf.Deg2Rad;
        var x0 = Mathf.Cos(angle);
        var y0 = Mathf.Sin(angle);

        var radiusForThisLine =
            radius - textInfo.lineInfo[0].lineExtents.max.y * textInfo.characterInfo[charIdx].lineNumber;
        var newMidBaselinePos = new Vector2(x0 * radiusForThisLine, -y0 * radiusForThisLine);

        return Matrix4x4.TRS(
            new Vector3(newMidBaselinePos.x, newMidBaselinePos.y, 0f),
            Quaternion.AngleAxis(-Mathf.Atan2(y0, x0) * Mathf.Rad2Deg - 90, Vector3.forward),
            Vector3.one);
    }
}

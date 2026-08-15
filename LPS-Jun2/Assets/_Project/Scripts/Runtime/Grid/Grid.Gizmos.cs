#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public partial class Grid
{
    private GUIStyle _guiStyle;

    private void OnDrawGizmos()
    {
        _guiStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter
        };
        Handles.Label(transform.position, LayoutType.ToString(), _guiStyle);
        foreach (var cell in _allCells)
        {
            var coordinate = cell.Coordinate;
            var text = new Vector2Int(coordinate.x, coordinate.y).ToString();
            var position = cell.transform.localToWorldMatrix.MultiplyPoint3x4(cell.CenterOffset);
            Handles.Label(position, text, _guiStyle);
        }

        Handles.matrix = Matrix4x4.TRS(Bounds.center, transform.rotation, Bounds.size);
        Handles.DrawWireCube(Vector3.zero, Vector3.one);
    }
}
#endif
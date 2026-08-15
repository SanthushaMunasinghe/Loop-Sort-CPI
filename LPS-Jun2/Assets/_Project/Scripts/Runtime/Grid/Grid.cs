using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

[DefaultExecutionOrder(-10)]
public sealed partial class Grid : GameBehaviourBase
{
    [field: SerializeField] public GridSheet.LayoutType LayoutType { get; private set; }
    [field: SerializeField] public PivotType Pivot { get; private set; }

    [Inject] private GridConfig _config;
    [Inject] private CellElements _cellElements;

    public CellBase[,] Cells { get; private set; }
    public Vector2Int Size { get; private set; }
    public Bounds Bounds { get; private set; }
    public GridSheet.Layout Layout { get; private set; }

    private readonly List<CellBase> _allCells = new();
    private readonly List<CellBase> _tempCells = new();
    private readonly Dictionary<int, List<CellBase>> _columnCells = new();
    private readonly Dictionary<int, List<CellBase>> _rowCells = new();

    private static readonly CellDistanceComparer DistanceComparer = new();

    public enum PivotType
    {
        TopLeft,
        Top,
        TopRight,
        Left,
        Center,
        Right,
        BottomLeft,
        Bottom,
        BottomRight,
    }

    private void Update()
    {
        var extents = Bounds.extents;
        var center = transform.position + transform.rotation * extents;
        Bounds = new Bounds(center, Bounds.size);
    }

    public void SetLayoutType(GridSheet.LayoutType layoutType)
    {
        LayoutType = layoutType;
    }

    public void Initialize(GridSheet.Layout layout)
    {
        Layout = layout;
        var columnSize = layout.Cells.GetLength(0);
        var rowSize = layout.Cells.GetLength(1);
        Size = new Vector2Int(columnSize, rowSize);
        Cells = new CellBase[Size.x, Size.y];

        var data = _config;
        var end = Vector2Int.zero;
        var layoutSize = GetLayoutSize();
        for (var x = 0; x < layoutSize.x; x++)
        {
            for (var y = 0; y < layoutSize.y; y++)
            {
                var cell = layout.Cells[x, y];
                if (cell.Data == "-") continue;

                var cellInstance = Instantiate(data.CellPrefab, transform);
                var cellCoordinate = new Vector2Int(x, y);
                var cellSize = Vector3.one * data.OuterRadius * 2f;
                var cellOffset = new Vector3(data.OuterRadius, 0, data.InnerRadius);
                Cells[x, y] = cellInstance;
                cellInstance.Initialize(this, cellCoordinate, cellSize, cellOffset);
                _allCells.Add(cellInstance);

                var gridInstanceT = cellInstance.transform;
                var worldOffset = CoordinateToWorldOffset(cellCoordinate);
                gridInstanceT.localPosition = worldOffset;

                if (x > end.x) end.x = x;
                if (y > end.y) end.y = y;
            }
        }

        if (_config.MinSize.x > end.x) end.x = _config.MinSize.x;
        if (_config.MinSize.y > end.y) end.y = _config.MinSize.y;

        end += Vector2Int.one;

        PopulateCellElements(layout.Cells);

        var width = end.x * data.OuterRadius * 2f;
        var height = end.y * data.InnerRadius * 2f;
        var size = new Vector3(width, 0, height);
        var extents = size / 2;

        var pivotOffset = transform.rotation * Pivot switch
        {
            PivotType.TopLeft => new Vector3(0, 0, size.z),
            PivotType.Top => new Vector3(0, extents.x, size.z),
            PivotType.TopRight => new Vector3(size.x, 0, size.z),
            PivotType.Left => new Vector3(extents.z, 0, 0),
            PivotType.Center => new Vector3(extents.x, 0, extents.z),
            PivotType.Right => new Vector3(size.x, 0, extents.z),
            PivotType.BottomLeft => new Vector3(0, 0, 0),
            PivotType.Bottom => new Vector3(extents.x, 0, 0),
            PivotType.BottomRight => new Vector3(size.x, 0, 0),
            _ => throw new ArgumentOutOfRangeException()
        };
        transform.position -= pivotOffset;

        var center = transform.position + transform.rotation * extents;
        Bounds = new Bounds(center, size);
    }

    private Vector3 CoordinateToWorldOffset(Vector2Int c)
    {
        if (_config.Type == GridType.Hex)
        {
            var forwardOffset = _config.InnerRadius * 2f * (c.y + c.x * .5f - c.x / 2);
            var rightOffset = _config.OuterRadius * 1.5f * c.x;
            return new Vector3(rightOffset, 0, forwardOffset);
        }

        {
            var forwardOffset = _config.InnerRadius * 2f * c.y;
            var rightOffset = _config.OuterRadius * 2f * c.x;
            return new Vector3(rightOffset, 0, forwardOffset);
        }
    }

    public void GetCellElements<T>(List<T> list) where T : CellElementBase
    {
        list.Clear();
        foreach (var cell in GetCells())
        {
            foreach (var element in cell.Elements)
            {
                if (element is T t) list.Add(t);
            }
        }
    }

    public CellBase GetCellAt(Vector2Int offsetCoordinate)
    {
        if (offsetCoordinate.x < 0 || offsetCoordinate.x >= Size.x ||
            offsetCoordinate.y < 0 || offsetCoordinate.y >= Size.y)
            return null;
        return Cells[offsetCoordinate.x, offsetCoordinate.y];
    }

    public List<CellBase> GetColumn(int column)
    {
        if (_columnCells.TryGetValue(column, out var cells)) return cells;

        var list = new List<CellBase>();
        for (var y = 0; y < Size.y; y++)
        {
            var coordinate = new Vector2Int(column, y);
            var cell = GetCellAt(coordinate);
            if (cell == null) continue;
            list.Add(cell);
        }
        _columnCells[column] = list;

        return list;
    }

    public List<CellBase> GetRow(int row)
    {
        if (_rowCells.TryGetValue(row, out var cells)) return cells;

        var list = new List<CellBase>();
        for (var x = 0; x < Size.x; x++)
        {
            var coordinate = new Vector2Int(x, row);
            var cell = GetCellAt(coordinate);
            if (cell == null) continue;
            list.Add(cell);
        }
        _rowCells[row] = list;

        return list;
    }

    public List<CellBase> GetCells()
    {
        return _allCells;
    }

    public CellBase FindNearestCell(Vector3 worldPosition, float maxDistance = 100f)
    {
        CellBase closestCell = null;
        var closestDistance = maxDistance;
        foreach (var cell in GetCells())
        {
            var distance = Vector3.Distance(cell.CenterPosition, worldPosition);
            if (distance > maxDistance) continue;
            if (distance > closestDistance) continue;
            closestCell = cell;
            closestDistance = distance;
        }

        return closestCell;
    }

    public List<CellBase> GetNearbyCells(Vector3 worldPosition, float maxDistance)
    {
        _tempCells.Clear();
        foreach (var cell in GetCells())
        {
            var distance = Vector3.Distance(cell.CenterPosition, worldPosition);
            if (distance > maxDistance) continue;
            _tempCells.Add(cell);
        }

        DistanceComparer.SetReferencePosition(worldPosition);
        _tempCells.Sort(DistanceComparer);
        return _tempCells;
    }

    public Vector2Int GetLayoutSize()
    {
        var x = Mathf.Clamp(Size.x, _config.MinSize.x, _config.MaxSize.x);
        var y = Mathf.Clamp(Size.y, _config.MinSize.y, _config.MaxSize.y);
        return new Vector2Int(x, y);
    }

    public void PopulateCellElements(GridSheet.Cell[,] cells, CellElementType? allowedType = null)
    {
        var layoutSize = GetLayoutSize();
        for (var x = 0; x < layoutSize.x; x++)
        {
            for (var y = 0; y < layoutSize.y; y++)
            {
                var layoutCell = cells[x, y];
                if (layoutCell.Data == "-") continue;
                var cell = Cells[x, y];
                PopulateCellWithElement(cell, layoutCell, allowedType);
            }
        }
    }

    public void PopulateCellWithElement(CellBase cell, GridSheet.Cell layoutCell, CellElementType? allowedType = null)
    {
        if (allowedType.HasValue)
        {
            InitializeCellElement(cell, layoutCell, allowedType.Value);
            return;
        }

        foreach (var (cellElementType, _) in _cellElements.Collection)
        {
            InitializeCellElement(cell, layoutCell, cellElementType);
        }
    }

    private void InitializeCellElement(CellBase cell, GridSheet.Cell layoutCell, CellElementType cellElementType)
    {
        var data = _cellElements.Get(cellElementType);
        if (!data.Prefab.IsCreateValueValid(layoutCell.Data)) return;
        var instance = PrefabModule.Rent(data.Prefab);
        cell.AddElement(instance);
        instance.OnCreate(layoutCell.Data);
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public abstract class CellBase : GameBehaviourBase
{
    public List<CellElementBase> Elements = new();
    public CellElementBase TopElement => Elements.Count > 0 ? Elements[0] : null;
    public Vector2Int Coordinate { get; private set; }
    public Vector3 Size { get; private set; }
    public Vector3 CenterOffset { get; private set; }
    public Quaternion CenterRotation { get; private set; }
    public Vector3 CenterPosition => transform.position + transform.rotation * CenterOffset;

    protected Grid Grid { get; private set; }

    private readonly Dictionary<Type, CellElementBase> _elementByType = new();

    public void Initialize(Grid grid, Vector2Int coordinate, Vector3 size, Vector3 offset)
    {
        Grid = grid;
        Coordinate = coordinate;
        Size = size;
        CenterOffset = offset;
    }

    public void AddElement(CellElementBase element, bool worldPositionStays = false)
    {
        if (Elements.Contains(element)) return;

        if (element.Size.x == 0) return;
        if (element.Size.y == 0) return;

        using var p = ListPool<CellBase>.Get(out var sizeCells);
        for (var x = 0; x < element.Size.x; x++)
        {
            for (var y = 0; y < element.Size.y; y++)
            {
                var offset = new Vector2Int(x, y);
                var cell = Grid.GetCellAt(Coordinate + offset);
                if (cell == null) continue;
                sizeCells.Add(cell);
            }
        }

        var requiredCellCount = element.Size.x * element.Size.y;
        // if (sizeCells.Count != requiredCellCount) return;

        if (element.Cell != null) element.Cell.RemoveElement(element);
        foreach (var sizeCell in element.SizeCells) sizeCell.RemoveElement(element);
        element.SizeCells.Clear();

        Elements.Add(element);
        Elements.Sort((left, right) => right.Priority.CompareTo(left.Priority));
        _elementByType.Add(element.GetType(), element);

        element.SetGrid(Grid);
        element.SetCell(this);

        sizeCells.Remove(this);
        element.SetSizeCells(sizeCells);
        foreach (var sizeCell in sizeCells) sizeCell.AddSizeElement(element);

        var elementT = element.transform;
        elementT.SetParent(transform, worldPositionStays);
        if (worldPositionStays) return;
        elementT.localPosition = element.GetLocalPositionAt(this);
    }

    public void AddSizeElement(CellElementBase element)
    {
        Elements.Add(element);
        Elements.Sort((left, right) => right.Priority.CompareTo(left.Priority));
        _elementByType.Add(element.GetType(), element);
    }

    public void RemoveElement(CellElementBase cellElement)
    {
        if (!Elements.Remove(cellElement)) return;
        _elementByType.Remove(cellElement.GetType());
    }

    public void SetRotation(Quaternion rotation)
    {
        CenterRotation = rotation;
    }

    public bool TryGetElement<T>() where T : CellElementBase => TryGetElement<T>(out _);

    public bool TryGetElement<T>(out T resultElement) where T : CellElementBase
    {
        resultElement = null;
        if (_elementByType.TryGetValue(typeof(T), out var element))
        {
            resultElement = (T)element;
            return true;
        }

        return false;
    }

    public abstract List<CellBase> GetNeighbors();

    public CellBase GetNeighborAt(Vector2Int offsetDirection)
    {
        return Grid.GetCellAt(Coordinate + offsetDirection);
    }

    public CellBase GetNeighborAt(Vector3Int offsetDirection)
    {
        return GetNeighborAt(new Vector2Int(offsetDirection.x, offsetDirection.z));
    }
}
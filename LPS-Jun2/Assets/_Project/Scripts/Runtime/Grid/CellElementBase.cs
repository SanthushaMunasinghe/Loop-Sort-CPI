using System.Collections.Generic;
using UnityEngine;

public abstract class CellElementBase : GameBehaviourBase
{
    [field: SerializeField] public CellElementType ElementType { get; private set; }
    [field: SerializeField] public int Priority { get; private set; }
    [field: SerializeField] public Vector3 Offset { get; private set; }
    [field: SerializeField] public Vector2Int Size { get; private set; } = Vector2Int.one;

    public Grid Grid { get; private set; }
    public CellBase Cell { get; private set; }
    public readonly List<CellBase> Cells = new();
    public readonly List<CellBase> SizeCells = new();

    public override void OnReturn()
    {
        base.OnReturn();

        if (Cell != null) Cell.RemoveElement(this);
        Cell = null;
        foreach (var sizeCell in SizeCells) sizeCell.RemoveElement(this);
        SizeCells.Clear();
        Cells.Clear();
    }

    public void SetGrid(Grid grid) => Grid = grid;

    public void SetCell(CellBase cell)
    {
        Cells.Add(cell);
        Cell = cell;
    }

    public void SetSizeCells(List<CellBase> sizeCells)
    {
        Cells.AddRange(sizeCells);
        SizeCells.AddRange(sizeCells);
    }

    public Vector3 GetSelfLocalPosition() => GetLocalPositionAt(Cell);
    public Vector3 GetSelfWorldPosition() => GetWorldPositionAt(Cell);
    public Vector3 GetLocalPositionAt(CellBase cell)
        => cell.CenterOffset + cell.CenterRotation * transform.rotation * Offset;
    public Vector3 GetWorldPositionAt(CellBase cell)
        => cell.transform.position + cell.CenterOffset + cell.CenterRotation * transform.rotation * Offset;

    public virtual bool IsCreateValueValid(string value) => false;
    public virtual void OnCreate(string value) { }

}
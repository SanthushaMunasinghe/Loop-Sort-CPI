using System.Collections.Generic;
using UnityEngine;

public sealed class CellDistanceComparer : IComparer<CellBase>
{
    private Vector3 _referencePosition;

    public void SetReferencePosition(Vector3 referencePosition)
    {
        _referencePosition = referencePosition;
    }

    public int Compare(CellBase left, CellBase right)
    {
        if (left == null || right == null) return 0;
        var leftDistance = Vector3.Distance(_referencePosition, left.CenterPosition);
        var rightDistance = Vector3.Distance(_referencePosition, right.CenterPosition);
        return leftDistance.CompareTo(rightDistance);
    }
}
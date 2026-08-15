using System;

public sealed class CellMesh : CellElementBase
{
    public override bool IsCreateValueValid(string value)
    {
        return !value.Contains("-", StringComparison.InvariantCultureIgnoreCase);
    }
}
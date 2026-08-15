using System;

public sealed class CellElements : Data
{
    public GenericDictionary<CellElementType, Data> Collection;

    public Data Get(CellElementType elementType)
    {
        return Collection.TryGetValue(elementType, out var data) ? data : default;
    }

    [Serializable]
    public struct Data
    {
        public CellElementBase Prefab;
    }
}

public enum CellElementType
{
    None = 0,

    Mesh = 1,

    Block = 10,
    Safe = 11,
    Rock = 12, Rk = 12,
    Hidden = 13, H = 13,
    Crate = 14, Cr = 14,
    Ice = 15,
    Key = 16,
    Tower = 17, Twr = 17,
}
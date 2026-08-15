using System;

public sealed class Blocks : Data
{
    public GenericDictionary<FeatureType, Data> Collection;

    public Data Get(FeatureType type)
    {
        return Collection.TryGetValue(type, out var data) ? data : Collection[FeatureType.None];
    }

    [Serializable]
    public struct Data
    {
        public Block Prefab;
    }
}
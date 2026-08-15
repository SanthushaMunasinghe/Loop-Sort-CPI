using System;

public sealed class Carriers : Data
{
    public GenericDictionary<FeatureType, Data> Collection;

    public Data Get(FeatureType type)
    {
        return Collection.TryGetValue(type, out var data) ? data : Collection[FeatureType.None];
    }

    public bool TryGet(FeatureType type, out Data data)
    {
        return Collection.TryGetValue(type, out data);
    }

    [Serializable]
    public struct Data
    {
        public Carrier Prefab;
    }
}
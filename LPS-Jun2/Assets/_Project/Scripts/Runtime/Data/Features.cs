using System;
using Scellecs.Morpeh;
using UnityEngine;

public sealed class Features : Data
{
    public GenericDictionary<FeatureType, Data> Collection;

    public Data Get(FeatureType featureType)
    {
        return Collection.TryGetValue(featureType, out var data) ? data : default;
    }

    [Serializable]
    public struct Data
    {
        public GameObject Prefab;
        public Material Material;
        public Sprite Icon;
        public float IconScaleMultiplier;
    }
}

public enum FeatureType
{
    None = 0, N = 0,

    Hidden = 1, H = 1,
    Rk = 2, Rock = 2,
    Crate = 3, Cr = 3,
    Ice = 4, I = 4,
    Lock = 5, L = 5,
    Key = 6, K = 6,

    Portal = 20,
    HiddenTruck = 21,
    HiddenCube = 22,
    LockKey = 23,
    Tag = 24,
    Connected = 25, ConnectedIce = 25, IceCon = 25,
    Carpet = 26,
    Tunnel = 27,
}

public interface IFeatureProcessor
{
    public void ProcessFeature(FeatureType featureType, string data);
}

public struct FeatureContainer : IComponent
{
    public string Data;
}

public static partial class Extensions
{
    public static string ToAnalyticsName(this FeatureType feature)
    {
        return feature switch
        {
            FeatureType.Rk => "barrier",
            FeatureType.Ice => "ice",
            FeatureType.HiddenTruck => "curtain",
            FeatureType.HiddenCube => "hidden_cube",
            FeatureType.Lock => "lock_key",
            FeatureType.Tag => "flag",
            FeatureType.Connected => "tape",
            FeatureType.Carpet => "carpet",
            FeatureType.Tunnel => "spawner",
            _ => "unknown_feature"
        };
    }
}
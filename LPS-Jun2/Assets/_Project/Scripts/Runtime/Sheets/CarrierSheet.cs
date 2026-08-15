using System;
using System.Collections.Generic;
using Cathei.BakingSheet;
using UnityEngine;
using UnityEngine.Pool;
using NonSerialized = Cathei.BakingSheet.NonSerializedAttribute;

public sealed class CarrierSheet : Sheet<int, CarrierSheet.Row>
{
    public class Row : SheetRow<int>
    {
        public Dictionary<CarrierType, VerticalList<string>> Colors { get; private set; }
        public string Features { get; private set; }

        private readonly Dictionary<CarrierType, List<CarrierColorArgs>> _carriers = new();
        private readonly Dictionary<CarrierType, List<CarrierFeatureArgs>> _features = new();
        private readonly Dictionary<(CarrierType, FeatureType), List<CarrierType>> _groups = new();

        [NonSerialized] public List<CarrierType> CarrierTypes { get; private set; } = new();
        [NonSerialized] public List<ColorType> ColorTypes { get; private set; } = new();
        [NonSerialized] public List<FeatureType> FeatureTypes { get; private set; } = new();

        public override void PostLoad(SheetConvertingContext context)
        {
            base.PostLoad(context);

            if (!Application.isPlaying)
            {
                foreach (var (_, value) in Colors)
                    value.Reverse();
            }

            try
            {
                ParseColors();
                ParseFeatures();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing CarrierSheet Id: {Id}");
                throw;
            }

#if !RELEASE_BUILD
            CheckColorDistribution();
#endif
        }

        private void ParseColors()
        {
            foreach (var (carrierType, colors) in Colors)
            {
                List<CarrierColorArgs> list = null;
                foreach (var color in colors)
                {
                    if (color[0] == '-') continue;

                    list ??= new List<CarrierColorArgs>();
                    var split = color.Split('_');
                    var colorType = new ColorType(split[0]);
                    var blockType = split.Length > 1 ? split[1] : string.Empty;

                    var featureType = FeatureType.None;
                    if (Enum.TryParse(blockType, ignoreCase: true, out FeatureType feature)) featureType = feature;
                    var featureData = split.Length > 2 ? split[2] : string.Empty;

                    var carrierColorArgs = new CarrierColorArgs
                    {
                        Color = colorType,
                        Feature = featureType,
                        FeatureData = featureData
                    };

                    list.Add(carrierColorArgs);
                    _carriers[carrierType] = list;

                    if (!CarrierTypes.Contains(carrierType))
                        CarrierTypes.Add(carrierType);

                    if (!ColorTypes.Contains(colorType)) ColorTypes.Add(colorType);
                    var f = featureType == FeatureType.Hidden ? FeatureType.HiddenCube : featureType;
                    if (f != FeatureType.None && !FeatureTypes.Contains(f))
                        FeatureTypes.Add(f);
                }
            }
        }

        private void ParseFeatures()
        {
            if (Features[0] == '-') return;

            var features = Features.Split('\n');
            foreach (var feature in features)
            {
                var s1 = feature.Split('-');
                var featureArgs = s1[1].Split('_');
                var featureType = Enum.Parse<FeatureType>(featureArgs[0], ignoreCase: true);
                var featureData = featureArgs.Length > 1 ? featureArgs[1] : string.Empty;
                var carrierFeatureArgs = new CarrierFeatureArgs
                {
                    Feature = featureType,
                    FeatureData = featureData
                };

                var groups = new List<CarrierType>();
                var carriers = s1[0].Split(',');
                foreach (var carrier in carriers)
                {
                    var carrierType = Enum.Parse<CarrierType>(carrier, ignoreCase: true);

                    if (!_features.TryGetValue(carrierType, out var featuresList))
                    {
                        featuresList = new List<CarrierFeatureArgs>();
                        _features[carrierType] = featuresList;
                    }
                    featuresList.Add(carrierFeatureArgs);

                    groups.Add(carrierType);
                    _groups[(carrierType, featureType)] = groups;
                }

                var f = featureType == FeatureType.Hidden ? FeatureType.HiddenTruck : featureType;
                if (f != FeatureType.None && !FeatureTypes.Contains(f))
                    FeatureTypes.Add(f);
            }
        }

        public bool TryGetCarrier(CarrierType carrierType, out List<CarrierColorArgs> carrierColors)
        {
            return _carriers.TryGetValue(carrierType, out carrierColors);
        }

        public bool TryGetCarrierFeature(CarrierType carrierType, FeatureType featureType, out CarrierFeatureArgs feature)
        {
            feature = default;
            if (!_features.TryGetValue(carrierType, out var features)) return false;
            foreach (var f in features)
            {
                if (f.Feature != featureType) continue;
                feature = f;
                return true;
            }

            return false;
        }

        public bool TryGetCarrierFeatures(CarrierType carrierType, out List<CarrierFeatureArgs> carrierFeatures)
        {
            return _features.TryGetValue(carrierType, out carrierFeatures);
        }

        public bool TryGetCarrierGroup(CarrierType carrierType, FeatureType featureType, out List<CarrierType> group)
        {
            return _groups.TryGetValue((carrierType, featureType), out group);
        }

        public bool HasCarrierFeature(CarrierType carrierType, FeatureType featureType)
        {
            if (!TryGetCarrierFeatures(carrierType, out var features)) return false;
            foreach (var feature in features)
                if (feature.Feature == featureType)
                    return true;
            return false;
        }

        private void CheckColorDistribution()
        {
            using var p = DictionaryPool<ColorType, int>.Get(out var colorCount);
            foreach (var (_, colors) in _carriers)
            foreach (var color in colors)
            {
                colorCount.TryGetValue(color.Color, out var count);
                colorCount[color.Color] = count + 1;
            }
            foreach (var (color, count) in colorCount)
            {
                if (count % 4 == 0) continue;
                Debug.LogWarning($"[CarrierSheet] Level {Id} Color {color} is not correct: {count}");
            }
        }
    }

    public enum CarrierType
    {
        None = 0,

        A = 1,
        B = 2,
        C = 3,
        D = 4,
        E = 5,
        F = 6,
        G = 7,
        H = 8,
        I = 9,
        J = 10,
        K = 11,
        L = 12,
        M = 13,
        N = 14,
        O = 15,
        P = 16,
//      Q = 17, Conveyor View
        R = 18,
        S = 19,
        T = 20,
        U = 21,
        V = 22,
        W = 23,
        X = 24,
        Y = 25, // Capacity Booster Carrier
        Z = 26, // Capacity Booster Carrier
    }

    public struct CarrierFeatureArgs
    {
        public FeatureType Feature;
        public string FeatureData;
    }

    public struct CarrierColorArgs
    {
        public ColorType Color;
        public FeatureType Feature;
        public string FeatureData;
    }
}

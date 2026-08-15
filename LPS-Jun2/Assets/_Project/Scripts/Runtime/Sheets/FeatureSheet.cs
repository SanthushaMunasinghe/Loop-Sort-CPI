using System.Collections.Generic;
using Cathei.BakingSheet;

public sealed class FeatureSheet : Sheet<FeatureType, FeatureSheet.Feature>
{
    public class Feature : SheetRow<FeatureType>
    {
        public int UnlockLevel { get; private set; }
        public bool Progress { get; private set; }
        public bool ShowOnStart { get; private set; }
    }

    public struct Progress
    {
        public bool HasProgress => NextFeature != null;
        public Feature NextFeature;
        public float FromPercent;
        public float ToPercent;
    }

    private readonly List<Feature> _sortedFeatures = new();
    private readonly PlayerPrefsInt _level = Prefs.Level;

    public override void PostLoad(SheetConvertingContext context)
    {
        base.PostLoad(context);

        foreach (var item in Items) _sortedFeatures.Add(item);
        _sortedFeatures.Sort((left, right) => left.UnlockLevel.CompareTo(right.UnlockLevel));
    }

    public Progress GetCurrentProgress()
    {
        var currentLevel = _level.Value;
        var previousUnlockLevel = 0;
        var nextFeature = default(Feature);

        foreach (var feature in _sortedFeatures)
        {
            if (0 >= feature.UnlockLevel) continue;
            if (!feature.Progress) continue;

            if (currentLevel > feature.UnlockLevel - 1)
            {
                previousUnlockLevel = feature.UnlockLevel - 1;
            }
            else
            {
                nextFeature = feature;
                break;
            }
        }

        if (nextFeature == null) return default;

        var levelProgress = currentLevel - previousUnlockLevel;
        var requiredLevel = nextFeature.UnlockLevel - previousUnlockLevel - 1;
        var fromPercent = (float)(levelProgress - 1) / requiredLevel;
        var toPercent = (float)levelProgress / requiredLevel;

        var progress = new Progress
        {
            NextFeature = nextFeature,
            FromPercent = fromPercent,
            ToPercent = toPercent,
        };
        return progress;
    }

    public bool TryGetUnlockedFeature(int level, out FeatureType unlockedFeature)
    {
        foreach (var feature in _sortedFeatures)
        {
            if (feature.UnlockLevel != level) continue;
            unlockedFeature = feature.Id;
            return true;
        }

        unlockedFeature = FeatureType.None;
        return false;
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cathei.BakingSheet;
using Cathei.BakingSheet.Unity;
using UnityEditor;

public static class LevelInsightsHelper
{
    private static readonly IEnumerable<CarrierSheet.CarrierType> CarrierTypes =
        Enum.GetValues(typeof(CarrierSheet.CarrierType)).Cast<CarrierSheet.CarrierType>();

    [MenuItem("Tools/Levels/Create Insight File", priority = -98)]
    public static async void CreateFiles()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Creating Level Insight", "Loading level sheets...", 0f);
            var emptyDictionary = new Dictionary<string, string>();
            var fileSystem = new CustomFileSystem(emptyDictionary);
            var converters = new ISheetImporter[] { new JsonSheetConverter("Sheets/Local", fileSystem) };
            var sheetContainer = new SheetContainer(converters, new UnityLogger());
            await sheetContainer.Bake(converters);

            EditorUtility.DisplayProgressBar("Creating Level Insight", "Creating files...", .25f);
            var difficulty = new StringBuilder();
            var colorCount = new StringBuilder();
            var carrierCount = new StringBuilder();
            var conveyorCapacity = new StringBuilder();
            var hiddenCube = new StringBuilder();
            var hiddenCarrier = new StringBuilder();
            var rock = new StringBuilder();
            var ice = new StringBuilder();
            var lockKey = new StringBuilder();
            var tag = new StringBuilder();
            var connectedIce = new StringBuilder();
            var carpet = new StringBuilder();
            var tunnel = new StringBuilder();
            foreach (var level in sheetContainer.Levels)
            {
                var carriers = level.Carriers.Ref;
                difficulty.AppendLine(GetDifficulty(level));
                colorCount.AppendLine(GetTotalColorCount(carriers));
                carrierCount.AppendLine(GetTotalCarrierCount(carriers));
                conveyorCapacity.AppendLine(level.SlotCount.ToString());
                hiddenCube.AppendLine(ContainsBlockFeature(carriers, FeatureType.Hidden).ToString());
                hiddenCarrier.AppendLine(ContainsFeature(carriers, FeatureType.Hidden).ToString());
                rock.AppendLine(ContainsFeature(carriers, FeatureType.Rock).ToString());
                ice.AppendLine(ContainsFeature(carriers, FeatureType.Ice).ToString());
                lockKey.AppendLine(ContainsFeature(carriers, FeatureType.Lock).ToString());
                tag.AppendLine(ContainsFeature(carriers, FeatureType.Tag).ToString());
                connectedIce.AppendLine(ContainsFeature(carriers, FeatureType.ConnectedIce).ToString());
                carpet.AppendLine(ContainsFeature(carriers, FeatureType.Carpet).ToString());
                tunnel.AppendLine(ContainsFeature(carriers, FeatureType.Tunnel).ToString());
            }

            Directory.CreateDirectory("Assets/Level Insights");
            await File.WriteAllTextAsync("Assets/Level Insights/Difficulty.txt", difficulty.ToString());
            await File.WriteAllTextAsync("Assets/Level Insights/ColorCount.txt", colorCount.ToString());
            await File.WriteAllTextAsync("Assets/Level Insights/CarrierCount.txt", carrierCount.ToString());
            await File.WriteAllTextAsync("Assets/Level Insights/ConveyorCapacity.txt", conveyorCapacity.ToString());
            await File.WriteAllTextAsync("Assets/Level Insights/HiddenCube.txt", hiddenCube.ToString());
            await File.WriteAllTextAsync("Assets/Level Insights/HiddenCarrier.txt", hiddenCarrier.ToString());
            await File.WriteAllTextAsync("Assets/Level Insights/Rock.txt", rock.ToString());
            await File.WriteAllTextAsync("Assets/Level Insights/Ice.txt", ice.ToString());
            await File.WriteAllTextAsync("Assets/Level Insights/LockKey.txt", lockKey.ToString());
            await File.WriteAllTextAsync("Assets/Level Insights/Tag.txt", tag.ToString());
            await File.WriteAllTextAsync("Assets/Level Insights/ConnectedIce.txt", connectedIce.ToString());
            await File.WriteAllTextAsync("Assets/Level Insights/Carpet.txt", carpet.ToString());
            await File.WriteAllTextAsync("Assets/Level Insights/Tunnel.txt", tunnel.ToString());

            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static string GetDifficulty(LevelSheet.Level level)
    {
        return level.Theme switch
        {
            ThemeType.Default => "Normal",
            ThemeType.Hard => "Hard",
            ThemeType.SuperHard => "Super Hard",
            _ => string.Empty
        };
    }

    private static string GetTotalColorCount(CarrierSheet.Row carriers)
    {
        var colors = new HashSet<ColorType>();
        foreach (var colorType in carriers.ColorTypes)
            colors.Add(colorType);
        return colors.Count.ToString();
    }

    private static string GetTotalCarrierCount(CarrierSheet.Row carriers)
    {
        var count = 0;
        foreach (var carrierType in CarrierTypes)
            if (carriers.TryGetCarrier(carrierType, out _))
                count++;
        return count.ToString();
    }

    private static bool ContainsBlockFeature(CarrierSheet.Row carriers, FeatureType featureType)
    {
        foreach (var carrierType in CarrierTypes)
            if (carriers.TryGetCarrier(carrierType, out var colors))
                foreach (var args in colors)
                    if (args.Feature == featureType)
                        return true;
        return false;
    }

    private static bool ContainsFeature(CarrierSheet.Row carriers, FeatureType featureType)
    {
        foreach (var carrierType in CarrierTypes)
            if (carriers.HasCarrierFeature(carrierType, featureType))
                return true;
        return false;
    }
}
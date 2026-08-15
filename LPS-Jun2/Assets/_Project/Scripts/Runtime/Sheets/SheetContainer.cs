using System;
using Cathei.BakingSheet;
using Cathei.BakingSheet.Raw;
using Cysharp.Threading.Tasks;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

public sealed class SheetContainer : SheetContainerBase
{
    public bool Reloading { get; private set; }

    private readonly ISheetImporter[] _importers;

    public SheetContainer(ILogger logger) : base(logger)
    {
    }

    public SheetContainer(ISheetImporter[] importers, ILogger logger) : base(logger)
    {
        _importers = importers;
    }

    // Main
    public ConstantSheet Constants { get; private set; }
    public AreaSheet Areas { get; private set; }
    public BoosterSheet Boosters { get; private set; }
    public FeatureSheet Features { get; private set; }

    // Levels
    public LevelSheet Levels { get; private set; }
    public SplineSheet Splines { get; private set; }
    public CarrierSheet Carriers { get; private set; }

    // Localization
    public LocalizationSheet Localization { get; private set; }

    // Other
    public GridSheet Grids { get; private set; }

    public async UniTask Reload()
    {
        if (_importers == null || _importers.Length == 0) return;
        if (Reloading) return;

        Reloading = true;
        Debug.Log("Reloading sheets...");

        var success = true;
        try
        {
            foreach (var importer in _importers)
                if (importer is RawSheetImporter rawImporter)
                    rawImporter.Reset();
            await Bake(_importers);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            success = false;
        }

        Reloading = false;
        Debug.Log("Sheets reloaded. Result: " + success);
    }

    public bool IsConverterOfType<T>() where T : ISheetImporter
    {
        var firstImporter = _importers[0];
        return firstImporter is T;
    }
}
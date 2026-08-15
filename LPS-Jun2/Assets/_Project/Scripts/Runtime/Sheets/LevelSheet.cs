using System;
using Cathei.BakingSheet;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;
using NonSerialized = Cathei.BakingSheet.NonSerializedAttribute;

public sealed class LevelSheet : Sheet<int, LevelSheet.Level>
{
    public class Level : SheetRow<int>
    {
        public int Map { get; private set; }
        [NonSerialized] public GridSheet.Reference Grids { get; private set; }
        public CarrierSheet.Reference Carriers { get; private set; }
        public SplineSheet.Reference Spline { get; private set; }
        public ThemeType Theme { get; private set; }
        public int SlotCount { get; private set; }
        public float CameraRotation { get; private set; }
        public int ColorMix { get; private set; }
    }

    private readonly PlayerPrefsInt _level = Prefs.Level;
    private readonly ThemeType[] _themes = { ThemeType.Default, ThemeType.Blue };

    public Level GetCurrent(int maxLevelOffset = 0, int levelOffset = 0)
    {
        var level = _level.Value + levelOffset;
        if (level < Items.Count) return Items[level];
        var loopIndex = (level - Items.Count) % (Items.Count - maxLevelOffset) + maxLevelOffset;
        return Items[loopIndex];
    }

    public ThemeType GetNextTheme(int frequency)
    {
        var i = (_level.Value + 1) / frequency;
        i %= _themes.Length;
        return _themes[i];
    }

    public int GetLoopCount()
    {
        return _level.Value / Items.Count + 1;
    }

#if !RELEASE_BUILD
    public override void PostLoad(SheetConvertingContext context)
    {
        base.PostLoad(context);

        CheckMissingCarrierTypes();
    }

    private async UniTaskVoid CheckMissingCarrierTypes()
    {
        await UniTask.NextFrame();

        foreach (var level in Items)
        {
            var splineRef = level.Spline.Ref;
            var carriersRef = level.Carriers.Ref;

            using var p = ListPool<CarrierSheet.CarrierType>.Get(out var splineCarriers);
            foreach (var cell in splineRef.Data.ActiveCells)
            {
                var data = cell.Data[0].ToString();
                if (!Enum.TryParse<CarrierSheet.CarrierType>(data, ignoreCase: true, out var carrierType))
                    continue;
                splineCarriers.Add(carrierType);
            }
            foreach (var carrierType in carriersRef.CarrierTypes)
            {
                if (splineCarriers.Contains(carrierType)) continue;
                Debug.LogWarning($"[SplineSheet] Level {level.Id} missing carrier type {carrierType}");
            }
        }
    }
#endif
}
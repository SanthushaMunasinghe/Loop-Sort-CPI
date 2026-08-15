#define USE_GPU_NAME_ON_QUALITY
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public enum AutoQualitySetting
{
    VeryLow = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    VeryHigh = 4,
}

public class AutoQualitySetter
{
    public static AutoQualitySetting CurrentQualitySetting { get; private set; } = AutoQualitySetting.Medium;

    [Tooltip("Apply quality setting automatically in Start()")]
    [SerializeField] private bool applyOnStart = true;

    private static readonly Dictionary<string, AutoQualitySetting> TierToQuality = new Dictionary<string, AutoQualitySetting>(StringComparer.OrdinalIgnoreCase)
    {
        {"A+", AutoQualitySetting.VeryHigh},
        {"A",  AutoQualitySetting.High},
        {"B",  AutoQualitySetting.Medium},
        {"C",  AutoQualitySetting.Low},
        {"D",  AutoQualitySetting.VeryLow},
    };

    private static Dictionary<AutoQualitySetting, string> QualityToTier;
    private const AutoQualitySetting defaultQualitySetting = AutoQualitySetting.Medium;

    [System.Serializable]
    public class GpuTierData
    {
        public string gpu;
        public string soc;
        public string family;
        public string tier;
    }

    public static AutoQualitySetting DetectSetting()
    {
        CurrentQualitySetting = DetectTierFromHardware();
        return CurrentQualitySetting;
    }

    private static bool DetectTierFromRAM(out AutoQualitySetting detectedTier)
    {
        try
        {
            const int limit1 = 7500;
            const int limit2 = 6000;
            const int limit3 = 4500;
            const int limit4 = 3000;

            bool isVeryHigh = SystemInfo.systemMemorySize > limit1;
            bool isHigh = SystemInfo.systemMemorySize > limit2 && SystemInfo.systemMemorySize <= limit1;
            bool isMedium = SystemInfo.systemMemorySize > limit3 && SystemInfo.systemMemorySize <= limit2;
            bool isLow = SystemInfo.systemMemorySize <= limit3 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3;
            bool isVeryLow = SystemInfo.systemMemorySize <= limit4 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3;

            if (isVeryHigh)
            {
                detectedTier = AutoQualitySetting.VeryHigh;
                return true;
            }
            else if (isHigh)
            {
                detectedTier = AutoQualitySetting.High;
                return true;
            }
            else if (isMedium)
            {
                detectedTier = AutoQualitySetting.Medium;
                return true;
            }
            else if (isLow)
            {
                detectedTier = AutoQualitySetting.Low;
                return true;
            }
            else if (isVeryLow)
            {
                detectedTier = AutoQualitySetting.VeryLow;
                return true;
            }
            else
            {
                detectedTier = defaultQualitySetting;
                return false;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AutoQualitySetter] Error detecting RAM tier: {ex.Message}");
            detectedTier = defaultQualitySetting;
            return false;
        }
    }

#if USE_GPU_NAME_ON_QUALITY

    private static AutoQualitySetting DetectTierFromHardware()
    {
        if (QualityToTier == null || QualityToTier.Count == 0)
        {
            QualityToTier = new();
            foreach (var kvp in TierToQuality)
            {
                QualityToTier.Add(kvp.Value, kvp.Key);
            }
        }

        try
        {
            var gpuTierListJSON = Resources.Load<TextAsset>("Configs/gpu_tier_list");
            var currentHardwareInfo = GpuDetect.GetRaw();
            AutoQualitySetting detectedTier = defaultQualitySetting;

            if (gpuTierListJSON != null && !string.IsNullOrEmpty(gpuTierListJSON.text))
            {
                var gpuTierList = JsonUtility.FromJson<GpuTierListWrapper>($"{{\"gpus\":{gpuTierListJSON.text}}}");
                if (gpuTierList?.gpus == null || gpuTierList.gpus.Length == 0)
                {
                    Debug.LogWarning("[AutoQualitySetter] Failed to parse GPU tier list or it's empty.");
                    return defaultQualitySetting;
                }

                var normalizedHardware = GpuDetect.GetNormalized();
                Debug.Log($"[AutoQualitySetter] Current hardware: {currentHardwareInfo.DeviceName} (Family: {normalizedHardware.Family}, Model: {normalizedHardware.Model})");

                // First, try exact GPU name matching
                string detectedTierString = FindTierByExactGpuName(gpuTierList.gpus, currentHardwareInfo.DeviceName);
                if (!string.IsNullOrEmpty(detectedTierString))
                {
                    Debug.Log($"[AutoQualitySetter] Found tier '{detectedTierString}' by exact GPU name match: {currentHardwareInfo.DeviceName}");
                    return TierToQuality[detectedTierString];
                }

                // Second, try family + model matching
                detectedTierString = FindTierByFamilyAndModel(gpuTierList.gpus, normalizedHardware.Family, normalizedHardware.Model);
                if (!string.IsNullOrEmpty(detectedTierString))
                {
                    Debug.Log($"[AutoQualitySetter] Found tier '{detectedTierString}' by family and model match: {normalizedHardware.Family} {normalizedHardware.Model}");
                    return TierToQuality[detectedTierString];
                }

                // // Third, try partial GPU name matching
                // detectedTier = FindTierByPartialGpuName(gpuTierList.gpus, currentHardwareInfo.DeviceName);
                // if (!string.IsNullOrEmpty(detectedTier))
                // {
                //     Debug.Log($"[AutoQualitySetter] Found tier '{detectedTier}' by partial GPU name match: {currentHardwareInfo.DeviceName}");
                //     return detectedTier;
                // }
            }
            else
            {
                Debug.LogWarning("[AutoQualitySetter] GPU tier list JSON is null or empty.");
            }

            // Third, try ram info
            if (DetectTierFromRAM(out detectedTier))
            {
                Debug.Log($"[AutoQualitySetter] Found tier '{detectedTier}' by RAM detection.");
                return detectedTier;
            }

            Debug.LogWarning($"[AutoQualitySetter] No matching tier found for GPU: {currentHardwareInfo.DeviceName}");
            return defaultQualitySetting;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AutoQualitySetter] Error detecting tier from hardware: {ex.Message}");
            return defaultQualitySetting;
        }
    }

    private static string Normalize(string s)
    {
        return s.Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
    }

    /// <summary>
    /// Finds tier by exact GPU name matching (case-insensitive).
    /// </summary>
    private static string FindTierByExactGpuName(GpuTierData[] gpuList, string deviceName)
    {
        try
        {
            if (string.IsNullOrEmpty(deviceName)) return null;

            string cleanDeviceName = CleanGpuName(deviceName);

            return gpuList
                .Where(gpu => !string.IsNullOrEmpty(gpu.gpu))
                .FirstOrDefault(gpu => string.Equals(CleanGpuName(gpu.gpu), cleanDeviceName, StringComparison.OrdinalIgnoreCase))
                ?.tier;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AutoQualitySetter] Error in FindTierByExactGpuName: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Finds tier by family and model matching.
    /// </summary>
    private static string FindTierByFamilyAndModel(GpuTierData[] gpuList, string family, string model)
    {
        try
        {
            if (string.IsNullOrEmpty(family) || string.IsNullOrEmpty(model)) return null;

            return gpuList
                .Where(gpu => !string.IsNullOrEmpty(gpu.family) && !string.IsNullOrEmpty(gpu.gpu))
                .FirstOrDefault(gpu =>
                    string.Equals(gpu.family, family, StringComparison.OrdinalIgnoreCase) &&
                    gpu.gpu.IndexOf(model, StringComparison.OrdinalIgnoreCase) >= 0)
                ?.tier;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AutoQualitySetter] Error in FindTierByFamilyAndModel: {ex.Message}");
            return null;
        }
    }

    // /// <summary>
    // /// Finds tier by partial GPU name matching (contains).
    // /// </summary>
    // private static string FindTierByPartialGpuName(GpuTierData[] gpuList, string deviceName)
    // {
    //     try
    //     {
    //         if (string.IsNullOrEmpty(deviceName)) return null;

    //         string cleanDeviceName = CleanGpuName(deviceName);

    //         // Try to find any GPU entry that contains key parts of our device name
    //         var deviceNameWords = cleanDeviceName.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
    //             .Where(word => word.Length > 2) // Only consider words longer than 2 characters
    //             .ToArray();

    //         return gpuList
    //             .Where(gpu => !string.IsNullOrEmpty(gpu.gpu))
    //             .FirstOrDefault(gpu =>
    //             {
    //                 string cleanGpuName = CleanGpuName(gpu.gpu);
    //                 return deviceNameWords.Any(word =>
    //                     cleanGpuName.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
    //             })
    //             ?.tier;
    //     }
    //     catch (System.Exception ex)
    //     {
    //         Debug.LogWarning($"[AutoQualitySetter] Error in FindTierByPartialGpuName: {ex.Message}");
    //         return null;
    //     }
    // }

    /// <summary>
    /// Cleans GPU name by removing common trademark symbols and normalizing spaces.
    /// </summary>
    private static string CleanGpuName(string gpuName)
    {
        if (string.IsNullOrEmpty(gpuName)) return string.Empty;

        return gpuName
            .Replace("(TM)", "")
            .Replace("(tm)", "")
            .Replace("(R)", "")
            .Replace("(r)", "")
            .Replace("  ", " ")
            .Trim();
    }

    [System.Serializable]
    private class GpuTierListWrapper
    {
        public GpuTierData[] gpus;
    }
#endif
}
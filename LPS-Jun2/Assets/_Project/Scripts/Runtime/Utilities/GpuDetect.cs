using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

public static class GpuDetect
{
    /// <summary>Raw values straight from Unity's SystemInfo.</summary>
    public struct Raw
    {
        public string DeviceName;          // e.g., "Adreno (TM) 640", "Mali-G76", "Apple A16 GPU"
        public string Vendor;              // e.g., "Qualcomm", "ARM", "Apple", "Imagination"
        public string DeviceVersion;       // e.g., "Vulkan 1.1.128" or "OpenGL ES 3.2 V@..."
        public GraphicsDeviceType Api;     // Vulkan, OpenGLES3, Metal, etc.
        public int ShaderLevel;            // e.g., 45 = SM 4.5 equivalent (Unity scale)
        public int UavSupport;             // 0/1; from SystemInfo.supportsComputeShaders etc.
        public bool SupportsCompute;
        public bool SupportsGeometryShaders;
        public bool SupportsInstancing;

        public override string ToString()
        {
            return $"GPU Raw Data:\n" +
                $"  Device Name: {DeviceName ?? "null"}\n" +
                $"  Vendor: {Vendor ?? "null"}\n" +
                $"  Device Version: {DeviceVersion ?? "null"}\n" +
                $"  API: {Api}\n" +
                $"  Shader Level: {ShaderLevel}\n" +
                $"  UAV Support: {UavSupport}\n" +
                $"  Supports Compute: {SupportsCompute}\n" +
                $"  Supports Geometry Shaders: {SupportsGeometryShaders}\n" +
                $"  Supports Instancing: {SupportsInstancing}";
        }
    }

    /// <summary>Normalized & parsed view for lookups/tiering.</summary>
    public struct Normalized
    {
        public string Key;         // e.g., "ADRENO_640", "MALI_G76", "APPLE_A16"
        public string Family;      // "Adreno", "Mali", "Apple", "PowerVR", "NVIDIA", "AMD", "Intel"
        public string Model;       // "640", "G76", "A16"
        public string Api;         // "Vulkan", "OpenGLES3", "Metal", etc.
        public int ShaderLevel;    // Unity shader level

        public override string ToString()
        {
            return $"GPU Normalized Data:\n" +
                $"  Key: {Key ?? "null"}\n" +
                $"  Family: {Family ?? "null"}\n" +
                $"  Model: {Model ?? "null"}\n" +
                $"  API: {Api ?? "null"}\n" +
                $"  Shader Level: {ShaderLevel}";
        }
    }

    public static Raw GetRaw()
    {
        return new Raw
        {
            DeviceName = SystemInfo.graphicsDeviceName?.Trim(),
            Vendor = SystemInfo.graphicsDeviceVendor?.Trim(),
            DeviceVersion = SystemInfo.graphicsDeviceVersion?.Trim(),
            Api = SystemInfo.graphicsDeviceType,
            ShaderLevel = SystemInfo.graphicsShaderLevel,
            UavSupport = SystemInfo.supports32bitsIndexBuffer ? 1 : 0, // simple extra capability flag
            SupportsCompute = SystemInfo.supportsComputeShaders,
            SupportsGeometryShaders = SystemInfo.supportsGeometryShaders,
            SupportsInstancing = SystemInfo.supportsInstancing
        };
    }

    public static Normalized GetNormalized()
    {
        var raw = GetRaw();

        // Start from the device name; fall back to vendor if missing.
        var name = string.IsNullOrEmpty(raw.DeviceName) ? raw.Vendor ?? "" : raw.DeviceName;

        // Strip common trademark clutter and spaces.
        var cleaned = Regex.Replace(name, @"\s+", " ").Trim();
        cleaned = cleaned.Replace("(TM)", "").Replace("(tm)", "").Replace("(R)", "").Replace("(r)", "").Trim();

        string family = "Unknown";
        string model = "";

        // Very light heuristics for common mobile families.
        // Extend this if you need more precise parsing.
        if (cleaned.StartsWith("Adreno", System.StringComparison.OrdinalIgnoreCase))
        {
            family = "Adreno";
            model = Regex.Match(cleaned, @"Adreno\s*([0-9]+[A-Za-z]?)").Groups[1].Value;
        }
        else if (cleaned.StartsWith("Mali", System.StringComparison.OrdinalIgnoreCase))
        {
            family = "Mali";
            // Matches "Mali-G76", "Mali T860", etc.
            var m = Regex.Match(cleaned, @"Mali[-\s]*([A-Za-z]?[0-9]+[A-Za-z0-9]*)");
            model = m.Success ? m.Groups[1].Value.ToUpperInvariant() : "";
        }
        else if (cleaned.StartsWith("Apple", System.StringComparison.OrdinalIgnoreCase))
        {
            family = "Apple";
            // Usually "Apple A16 GPU", "Apple A15 GPU"
            var m = Regex.Match(cleaned, @"Apple\s*A?([0-9X]+)", RegexOptions.IgnoreCase);
            model = m.Success ? ("A" + m.Groups[1].Value.ToUpperInvariant()) : "";
        }
        else if (cleaned.StartsWith("PowerVR", System.StringComparison.OrdinalIgnoreCase) ||
                 cleaned.StartsWith("IMG", System.StringComparison.OrdinalIgnoreCase))
        {
            family = "PowerVR";
            var m = Regex.Match(cleaned, @"(PowerVR|IMG)\s*([A-Z0-9\-]+)", RegexOptions.IgnoreCase);
            model = m.Success ? m.Groups[2].Value.ToUpperInvariant() : "";
        }
        else if (cleaned.StartsWith("NVIDIA", System.StringComparison.OrdinalIgnoreCase) ||
                 cleaned.StartsWith("GeForce", System.StringComparison.OrdinalIgnoreCase))
        {
            family = "NVIDIA";
            model = cleaned.Replace("NVIDIA", "").Replace("GeForce", "").Trim().Replace(" ", "_").ToUpperInvariant();
        }
        else if (cleaned.StartsWith("AMD", System.StringComparison.OrdinalIgnoreCase) ||
                 cleaned.IndexOf("Radeon", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            family = "AMD";
            model = cleaned.Replace("AMD", "").Replace("Radeon", "").Trim().Replace(" ", "_").ToUpperInvariant();
        }
        else if (cleaned.StartsWith("Intel", System.StringComparison.OrdinalIgnoreCase))
        {
            family = "Intel";
            model = cleaned.Replace("Intel", "").Trim().Replace(" ", "_").ToUpperInvariant();
        }
        else
        {
            // Try to infer from vendor if name was too generic
            if (!string.IsNullOrEmpty(raw.Vendor))
            {
                if (raw.Vendor.IndexOf("Qualcomm", System.StringComparison.OrdinalIgnoreCase) >= 0) family = "Adreno";
                else if (raw.Vendor.IndexOf("ARM", System.StringComparison.OrdinalIgnoreCase) >= 0) family = "Mali";
                else if (raw.Vendor.IndexOf("Apple", System.StringComparison.OrdinalIgnoreCase) >= 0) family = "Apple";
                else if (raw.Vendor.IndexOf("Imagination", System.StringComparison.OrdinalIgnoreCase) >= 0) family = "PowerVR";
                else if (raw.Vendor.IndexOf("NVIDIA", System.StringComparison.OrdinalIgnoreCase) >= 0) family = "NVIDIA";
                else if (raw.Vendor.IndexOf("AMD", System.StringComparison.OrdinalIgnoreCase) >= 0) family = "AMD";
                else if (raw.Vendor.IndexOf("Intel", System.StringComparison.OrdinalIgnoreCase) >= 0) family = "Intel";
            }
        }

        // Build a lookup-friendly key
        var key = (family + "_" + (string.IsNullOrEmpty(model) ? cleaned : model))
                  .ToUpperInvariant()
                  .Replace("-", "_")
                  .Replace(" ", "_");

        return new Normalized
        {
            Key = key,
            Family = family,
            Model = string.IsNullOrEmpty(model) ? cleaned : model,
            Api = raw.Api.ToString(),
            ShaderLevel = raw.ShaderLevel
        };
    }

    /// <summary>Convenience: returns a human-readable summary line.</summary>
    public static string Describe()
    {
        var raw = GetRaw();
        var norm = GetNormalized();
        return $"GPU: {raw.DeviceName} ({raw.Vendor}) | API: {norm.Api} | ShaderLevel: {norm.ShaderLevel} | Key: {norm.Key} | Compute: {raw.SupportsCompute} | Instancing: {raw.SupportsInstancing}";
    }
}
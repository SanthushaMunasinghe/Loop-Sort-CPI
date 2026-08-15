using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Cathei.BakingSheet.Internal;

public sealed class CustomFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _customData;

    public CustomFileSystem(Dictionary<string, string> args)
    {
        _customData = args;
    }

    public IEnumerable<string> GetFiles(string path, string extension)
    {
        path = UpdatePath(path);
        var objects = Resources.LoadAll(path);
        foreach (var o in objects)
            yield return Path.Combine(path, o.name);
    }

    public bool Exists(string path)
    {
        path = UpdatePath(path);
        return Resources.Load<TextAsset>(path) != null;
    }

    public Stream OpenRead(string path)
    {
        path = UpdatePath(path);

        try
        {
            var configName = path.Substring(path.LastIndexOf('/') + 1);
            var sheetConfigNames = new[] { configName + "Sheet", configName + "OverrideSheet" };
            foreach (var sheetConfigName in sheetConfigNames)
                if (_customData.TryGetValue(sheetConfigName, out var data))
                {
                    const string emptyData = "[{\"Data\":\"\\\"\\\"\"}]";
                    if (!string.Equals(data, emptyData))
                    {
                        data = data.Substring(10); // remove [{"Data":" }]"
                        data = data.Substring(0, data.Length - 3); // remove "}]
                        data = data.Replace("\\\"", "\"");
                        data = data.Replace("\\\\n", "\\n");
                        var buffer = Encoding.UTF8.GetBytes(data);
                        return new MemoryStream(buffer);
                    }
                }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        var textAsset = Resources.Load<TextAsset>(path);
        return textAsset != null
            ? new MemoryStream(textAsset.bytes)
            : throw new FileNotFoundException($"File not found in Resources: {path}");
    }

    public void CreateDirectory(string path)
    {
        throw new NotSupportedException($"CreateDirectory is not supported in {GetType().Name}.");
    }

    public Stream OpenWrite(string path)
    {
        throw new NotSupportedException($"Writing is not supported in {GetType().Name}.");
    }

    private static string UpdatePath(string path)
    {
        path = path.Substring(0, path.LastIndexOf('.'));
        return path;
    }
}
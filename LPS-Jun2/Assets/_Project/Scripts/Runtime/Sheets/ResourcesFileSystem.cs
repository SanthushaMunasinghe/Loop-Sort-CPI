using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Cathei.BakingSheet.Internal;

public class ResourcesFileSystem : IFileSystem
{
    public virtual IEnumerable<string> GetFiles(string path, string extension)
    {
        var objects = Resources.LoadAll(path);
        foreach (var o in objects)
        {
            yield return Path.Combine(path, o.name);
        }
    }

    public virtual bool Exists(string path)
    {
        return Resources.Load<TextAsset>(path) != null;
    }

    public virtual Stream OpenRead(string path)
    {
        var textAsset = Resources.Load<TextAsset>(path);
        if (textAsset != null)
        {
            return new MemoryStream(textAsset.bytes);
        }

        throw new FileNotFoundException($"File not found in Resources: {path}");
    }

    public virtual void CreateDirectory(string path)
    {
        throw new System.NotSupportedException("CreateDirectory is not supported in ResourcesFileSystem.");
    }

    public virtual Stream OpenWrite(string path)
    {
        throw new System.NotSupportedException("Writing is not supported in ResourcesFileSystem.");
    }
}
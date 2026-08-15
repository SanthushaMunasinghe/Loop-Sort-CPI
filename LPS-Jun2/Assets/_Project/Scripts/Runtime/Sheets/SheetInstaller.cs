using System;
using System.Collections.Generic;
using Cathei.BakingSheet;
using Cathei.BakingSheet.Unity;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

public sealed class SheetInstaller : InstallerBase
{
    private SheetContainer _container;
    private CustomFileSystem _customFileSystem;
    private bool _isBakeSuccessful;

    public override async UniTask Initialize(Dictionary<string, string> args)
    {
        _customFileSystem = new CustomFileSystem(args);
        ISheetImporter[] converters = await LoadLocalSheetConverters();
        try
        {
            await BakeContainer(converters);

            if (!_isBakeSuccessful)
            {
                throw new Exception("Failed to bake sheet container!");
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public override void Install(IContainerBuilder builder)
    {
        if (!_isBakeSuccessful) return;
        builder.RegisterInstance(_container).AsSelf();
    }

    private async UniTask BakeContainer(ISheetImporter[] converters)
    {
        var logger = new UnityLogger();
        _container = new SheetContainer(converters, logger);
        _isBakeSuccessful = await _container.Bake(converters);
    }

    private UniTask<ISheetImporter[]> LoadLocalSheetConverters()
    {
        var converters = new ISheetImporter[] { new JsonSheetConverter("Sheets/Local", _customFileSystem) };
        return UniTask.FromResult(converters);
    }
}

[Serializable]
public struct Spreadsheets
{
    public SheetGroup[] Groups;

    public SheetGroup GetSheetGroup(string targetName)
    {
        foreach (var group in Groups)
        {
            if (!string.Equals(group.Name, targetName, StringComparison.InvariantCultureIgnoreCase)) continue;
            return group;
        }

        return default;
    }

    public int GetIndexOf(string targetName)
    {
        for (var i = 0; i < Groups.Length; i++)
        {
            var group = Groups[i];
            if (!string.Equals(group.Name, targetName, StringComparison.InvariantCultureIgnoreCase)) continue;
            return i;
        }

        return 0;
    }

    public string GetNameOf(int idx)
    {
        return Groups[idx].Name;
    }
}

[Serializable]
public struct SheetGroup
{
    public string Name;
    public string[] Addresses;
}
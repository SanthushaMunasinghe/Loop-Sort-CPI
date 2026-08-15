using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using VContainer.Unity;

public sealed class DataInstaller : InstallerBase
{
    private readonly List<DataContainer> _dataContainers = new();

    public override async UniTask Initialize(Dictionary<string, string> args)
    {
        using var p = ListPool<string>.Get(out var paths);

        if (args.TryGetValue("DataContainerConfig", out var dataContainerConfigJson))
        {
            dataContainerConfigJson = dataContainerConfigJson.Substring(1, dataContainerConfigJson.Length - 2);
            var dataContainerConfig = JsonUtility.FromJson<DataContainerConfig>(dataContainerConfigJson);
            paths.AddRange(dataContainerConfig.Paths);
        }

        paths.AddRange(Prefs.DataContainerPaths.Value.Split('\n'));

        foreach (var path in paths)
        {
            if (string.IsNullOrEmpty(path)) continue;
            var dataContainer = (DataContainer)await Resources.LoadAsync<DataContainer>(path);
            if (dataContainer == null) continue;
            _dataContainers.Add(dataContainer);
        }

        const string pathBase = "Data Containers/Data Container Base";
        var dataContainerBase = (DataContainer)await Resources.LoadAsync<DataContainer>(pathBase);
        _dataContainers.Add(dataContainerBase);
    }

    public override void Install(IContainerBuilder builder)
    {
        using var p1 = ListPool<Data>.Get(out var dataList);
        var flagList = new List<string>();
        foreach (var dataContainer in _dataContainers)
        {
            Debug.Log($"<b>{nameof(DataInstaller)}</b>: Install ({dataContainer.name})");
            foreach (var data in dataContainer.Collection)
            {
                if (dataList.Contains(data)) continue;
                if (dataList.Exists(x => x.GetType() == data.GetType())) continue;
                dataList.Add(data);
            }

            flagList.AddRange(dataContainer.Flags);
        }
        foreach (var data in dataList)
        {
            builder.RegisterComponent(data).AsSelf();
        }

        var dataFlags = new DataFlags { Flags = flagList };
        builder.RegisterInstance(dataFlags).AsSelf();
    }
}

public struct DataFlags
{
    public List<string> Flags;

    public bool Contains(string flag)
    {
        return Flags.Contains(flag);
    }
}
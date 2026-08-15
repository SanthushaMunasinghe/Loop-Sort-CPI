using System;
using Cysharp.Threading.Tasks;
using VContainer;

public sealed class RemoteConfigModule : ModuleBase
{
    public T GetDataClassNew<T>() where T : class, new()
    {
        return new T();
    }

    public string[] GetActiveCohortNames()
    {
        return Array.Empty<string>();
    }
}
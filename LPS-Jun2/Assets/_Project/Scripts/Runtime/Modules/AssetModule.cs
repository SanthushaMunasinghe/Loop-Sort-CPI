using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer;

public sealed class AssetModule : ModuleBase
{
    [Inject] private IPublisher<AssetLoadStartMessage> _assetLoadStartPub;
    [Inject] private IPublisher<AssetLoadCompleteMessage> _assetLoadCompletePub;
}

public struct AssetLoadStartMessage
{
}

public struct AssetLoadCompleteMessage
{
    public bool AllAssetsLoaded;
}
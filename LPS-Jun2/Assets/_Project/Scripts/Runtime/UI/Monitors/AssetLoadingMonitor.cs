using MessagePipe;
using VContainer;

public sealed class AssetLoadingMonitor : MonitorBase
{
    [Inject] private ISubscriber<AssetLoadStartMessage> _assetLoadStartSub;
    [Inject] private ISubscriber<AssetLoadCompleteMessage> _assetLoadCompleteSub;

    public override void Setup()
    {
        base.Setup();

        _assetLoadStartSub.Subscribe(OnAssetLoadStart);
        _assetLoadCompleteSub.Subscribe(OnAssetLoadComplete);
    }

    private void OnAssetLoadStart(AssetLoadStartMessage m)
    {
        Monitors.Additive<AssetLoadingMonitor>(NoTransition.Instance);
    }

    private void OnAssetLoadComplete(AssetLoadCompleteMessage m)
    {
        if (m.AllAssetsLoaded)
        {
            Monitors.Deactivate(this);
        }
    }
}
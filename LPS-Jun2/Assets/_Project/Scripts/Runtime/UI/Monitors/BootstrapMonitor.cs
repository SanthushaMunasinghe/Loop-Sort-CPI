using UnityEngine;

public sealed class BootstrapMonitor : MonitorBase
{
    protected override void OnEnable()
    {
        base.OnEnable();

        SetText(TextRole.Version, "");
        SetText(TextRole.Build, "");
        GetImage(ImageRole.Fill).rectTransform.anchorMax = new Vector2(0f, 1f);
    }
}
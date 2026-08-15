using MessagePipe;
using UnityEngine;
using VContainer;

public sealed class LevelEnvironmentSystem : SystemBase
{
    [Inject] private PrefabModule _prefabModule;
    [Inject] private ConveyorConfig _conveyorConfig;
    [Inject] private Surface _surface;
    [Inject] private Camera _camera;

    [Inject] private ISubscriber<CameraUpdateCompleteMessage> _cameraUpdateCompleteSub;
    [Inject] private ISubscriber<LevelEnvironmentUpdateMessage> _levelEnvironmentUpdateSub;

    private GameObject _shineInstance;
    private Color? _originalBackgroundColor;

    protected override void BuildMessages(DisposableBagBuilder bag)
    {
        _cameraUpdateCompleteSub.Subscribe(OnCameraUpdateComplete).AddTo(bag);
        _levelEnvironmentUpdateSub.Subscribe(OnLevelEnvironmentUpdate).AddTo(bag);
    }

    private void OnCameraUpdateComplete(CameraUpdateCompleteMessage m)
    {
        CreateShineModel(m.Center);
        SetGreenLevel(Prefs.GreenLevel);
    }

    private void OnLevelEnvironmentUpdate(LevelEnvironmentUpdateMessage m)
    {
        SetGreenLevel(m.IsGreenLevel);
    }

    private void CreateShineModel(Vector3 center)
    {
        if (_shineInstance != null) return;
        _shineInstance = _prefabModule.Rent(_conveyorConfig.ShinePrefab);
        _shineInstance.transform.position = center + Vector3.down * _conveyorConfig.ConveyorHeight + Vector3.up * .01f;
    }

    private void SetGreenLevel(bool isGreenLevel)
    {
        _surface.gameObject.SetActive(!isGreenLevel);
        _shineInstance?.SetActive(!isGreenLevel);
        _originalBackgroundColor ??= _camera.backgroundColor;
        _camera.backgroundColor = isGreenLevel ? Color.green : _originalBackgroundColor.Value;
    }
}

public struct LevelEnvironmentUpdateMessage
{
    public bool IsGreenLevel;
}
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public sealed class ConveyorRedSystem : SystemBase
{
    [Inject] private Conveyor _conveyor;
    [Inject] private ConveyorConfig _config;

    private Color _defaultColor;
    private Color _previousColor;
    private float _redStartTimestamp;

    public override void OnAwake()
    {
        base.OnAwake();

        var propertyRegistry = _conveyor.GetComponent<RendererPropertyRegistry>();
        var targets = propertyRegistry.GetTargets("Side Color");
        var target = targets[0];
        var renderer = target.TargetRenderers[0];
        using var p = ListPool<Material>.Get(out var materials);
        renderer.GetSharedMaterials(materials);
        _defaultColor = materials[target.MaterialIndex].GetColor(target.PropertyId);
        _previousColor = _defaultColor;
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        var ratio = _conveyor.GetOccupiedSlotRatio01();
        var red = ratio >= _config.RedRatio;

        if (!red) _redStartTimestamp = Time.time;
        var t = Time.time - _redStartTimestamp;

        var pingPong = Mathf.PingPong(t * _config.ColorPingPongModifier, 1f);
        var targetColor = red
            ? Color.Lerp(_defaultColor, _config.SideRedColor, pingPong)
            : _defaultColor;
        var color = red
            ? targetColor
            : Color.Lerp(_previousColor, targetColor, deltaTime * _config.ColorLerpModifier);
        _conveyor.SetSideColor(color);
        _previousColor = color;
    }
}
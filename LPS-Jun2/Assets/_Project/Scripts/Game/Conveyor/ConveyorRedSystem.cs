using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public sealed class ConveyorRedSystem : SystemBase
{
    private enum CyclePhase { ToEnd, HoldEnd, ToStart, HoldStart }

    private const float DefaultLerpDuration = 0.6667f; // matches old ColorPingPongModifier (1.5) half-period

    [Inject] private Conveyor _conveyor;
    [Inject] private ConveyorConfig _config;
    [Inject] private SceneScope _sceneScope;

    private Color _startColor;
    private Color _endColor;
    private float _lerpDuration;
    private float _interval;

    private CyclePhase _phase;
    private float _phaseElapsed;
    private bool _wasRed;

    public override void OnAwake()
    {
        base.OnAwake();

        var propertyRegistry = _conveyor.GetComponent<RendererPropertyRegistry>();
        var targets = propertyRegistry.GetTargets("Side Color");
        var target = targets[0];
        var renderer = target.TargetRenderers[0];
        using var p = ListPool<Material>.Get(out var materials);
        renderer.GetSharedMaterials(materials);
        var materialDefaultColor = materials[target.MaterialIndex].GetColor(target.PropertyId);

        _startColor = _sceneScope.ConveyorRedCycleStartColorOverride.a > 0f
            ? _sceneScope.ConveyorRedCycleStartColorOverride
            : materialDefaultColor;
        _endColor = _sceneScope.ConveyorRedCycleEndColorOverride.a > 0f
            ? _sceneScope.ConveyorRedCycleEndColorOverride
            : _config.SideRedColor;
        _lerpDuration = _sceneScope.ConveyorRedCycleLerpDurationOverride > 0f
            ? _sceneScope.ConveyorRedCycleLerpDurationOverride
            : DefaultLerpDuration;
        _interval = Mathf.Max(0f, _sceneScope.ConveyorRedCycleIntervalOverride);

        _phase = CyclePhase.ToEnd;
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        if (!_sceneScope.ConveyorNearFullWarningEnabled)
        {
            if (_wasRed)
            {
                _conveyor.ClearSideColor();
                _wasRed = false;
            }
            return;
        }

        var ratio = _conveyor.GetOccupiedSlotRatio01();
        var redRatio = _sceneScope.ConveyorRedRatioOverride > 0f ? _sceneScope.ConveyorRedRatioOverride : _config.RedRatio;
        var red = ratio >= redRatio;

        if (red)
        {
            if (!_wasRed)
            {
                _phase = CyclePhase.ToEnd;
                _phaseElapsed = 0f;
            }
            _conveyor.SetSideColor(AdvanceCycle(deltaTime));
        }
        else if (_wasRed)
        {
            _conveyor.ClearSideColor();
        }

        _wasRed = red;
    }

    private Color AdvanceCycle(float deltaTime)
    {
        _phaseElapsed += deltaTime;

        switch (_phase)
        {
            case CyclePhase.ToEnd:
            {
                var t = _lerpDuration > 0f ? Mathf.Clamp01(_phaseElapsed / _lerpDuration) : 1f;
                var color = Color.Lerp(_startColor, _endColor, t);
                if (_phaseElapsed >= _lerpDuration)
                {
                    _phase = _interval > 0f ? CyclePhase.HoldEnd : CyclePhase.ToStart;
                    _phaseElapsed = 0f;
                }
                return color;
            }
            case CyclePhase.HoldEnd:
                if (_phaseElapsed >= _interval)
                {
                    _phase = CyclePhase.ToStart;
                    _phaseElapsed = 0f;
                }
                return _endColor;
            case CyclePhase.ToStart:
            {
                var t = _lerpDuration > 0f ? Mathf.Clamp01(_phaseElapsed / _lerpDuration) : 1f;
                var color = Color.Lerp(_endColor, _startColor, t);
                if (_phaseElapsed >= _lerpDuration)
                {
                    _phase = _interval > 0f ? CyclePhase.HoldStart : CyclePhase.ToEnd;
                    _phaseElapsed = 0f;
                }
                return color;
            }
            default: // HoldStart
                if (_phaseElapsed >= _interval)
                {
                    _phase = CyclePhase.ToEnd;
                    _phaseElapsed = 0f;
                }
                return _startColor;
        }
    }
}

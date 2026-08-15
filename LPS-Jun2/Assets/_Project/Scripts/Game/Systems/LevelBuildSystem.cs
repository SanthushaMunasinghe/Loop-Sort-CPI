using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using Dreamteck.Splines;
using MessagePipe;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public sealed class LevelBuildSystem : SystemBase
{
    [Inject] private PrefabModule _prefabModule;
    [Inject] private SplineComputer _splineComputer;
    [Inject] private LevelSheet.Level _level;
    [Inject] private Conveyor _conveyor;
    [Inject] private ConveyorConfig _conveyorConfig;
    [Inject] private CarrierConfig _carrierConfig;
    [Inject] private Carriers _carriers;
    [Inject] private BlockConfig _blockConfig;
    [Inject] private RemoteConfigModule _remoteConfigModule;

    [Inject] private IPublisher<LevelBuildCompleteMessage> _levelBuildCompletePub;
    [Inject] private IPublisher<CameraUpdateBoundsMessage> _cameraUpdateBoundsPub;

    private Transform _parent;
    private SplineSheet.Layout _splineRef;
    private BlockPhysicsConfig _blockPhysicsConfig;
    private LoseConfig _loseConfig;

    public override void OnAwake()
    {
        base.OnAwake();

        _blockPhysicsConfig = _remoteConfigModule.GetDataClassNew<BlockPhysicsConfig>();
        _loseConfig = _remoteConfigModule.GetDataClassNew<LoseConfig>();

        Build().Forget();
    }

    private async UniTaskVoid Build()
    {
        _parent = new GameObject("Level").transform;
        _conveyor.transform.parent = _parent;

        _splineRef = _level.Spline.Ref;
        var cells = _splineRef.Data.Cells;
        CreateSplinePoints(cells);

        if (_splineRef.Closed)
            _splineComputer.Close();

        for (var i = 0; i < _splineRef.Subdivide; i++)
            ApplySplineSubdivide();

        await UniTask.NextFrame(SceneLoadToken);

        _splineComputer.RebuildImmediate();

        await UniTask.NextFrame(SceneLoadToken);

        SetCarrierBlockArgs();

        await UniTask.NextFrame(SceneLoadToken);

        CreateCarriers(cells);
        ApplyCameraRotation();

        await UniTask.DelayFrame(2, cancellationToken: SceneLoadToken);

        _levelBuildCompletePub.Publish(new LevelBuildCompleteMessage());

        await UniTask.NextFrame(SceneLoadToken);

        _cameraUpdateBoundsPub.Publish(new CameraUpdateBoundsMessage());
    }

    private void CreateSplinePoints(List<SplineSheet.Cell> cells)
    {
        foreach (var cell in cells)
        {
            var data = cell.Data;
            var x = cell.X;
            var y = cell.Y;
            if (string.IsNullOrEmpty(data)) continue;
            if (data[0] == '-') continue;

            var split = data.Split('\n');
            var first = split[0].Split('-')[0];

            if (int.TryParse(first, out _))
            {
                foreach (var s in split)
                {
                    var s2 = s.Split('-');
                    if (!int.TryParse(s2[0], out var idx)) continue;
                    var height = s2.Length > 1 ? ParseFloat(s2[1]) : 0f;
                    CreateSplinePoint(idx - 1, x, y, height);
                }
            }
        }
    }

    private void CreateCarriers(List<SplineSheet.Cell> cells)
    {
        foreach (var cell in cells)
        {
            var data = cell.Data;
            var x = cell.X;
            var y = cell.Y;
            if (string.IsNullOrEmpty(data)) continue;
            if (data[0] == '-') continue;

            var split = data.Split('\n');
            var first = split[0].Split('-')[0];

            if (int.TryParse(first, out _)) continue;

            var angle = ParseAngle(split);
            var rotation = Quaternion.AngleAxis(angle, Vector3.up);
            var worldOffset = ParseWorldOffset(split, x, y, rotation);

            if (Enum.TryParse(first, ignoreCase: true, out CarrierSheet.CarrierType type))
            {
                var instance = CreateCarrier(type);
                var instanceT = instance.transform;
                worldOffset.y -= _conveyorConfig.ConveyorHeight - .1f;
                instanceT.position = worldOffset;
                instanceT.rotation = rotation;
                instanceT.position -= instanceT.rotation * instance.Pivot.localPosition;
            }
        }
    }

    private void CreateSplinePoint(int idx, int x, int y, float height)
    {
        var offsetY = _splineComputer.transform.position.y;
        var worldOffset = new Vector3(x * _splineRef.Spacing, height + offsetY, y * _splineRef.Spacing);
        var splinePoint = new SplinePoint(worldOffset);
        _splineComputer.SetPoint(idx, splinePoint);
    }

    private Carrier CreateCarrier(CarrierSheet.CarrierType carrierType)
    {
        var featureType = FeatureType.None;
        var carriersRef = _level.Carriers.Ref;
        if (carriersRef.TryGetCarrierFeatures(carrierType, out var features))
        {
            foreach (var feature in features)
            {
                if (!_carriers.TryGet(feature.Feature, out _)) continue;
                featureType = feature.Feature;
                break;
            }
        }

        var data = _carriers.Get(featureType);
        var instance = _prefabModule.Rent(data.Prefab, _parent);
        instance.SetType(carrierType);

        if (instance.TryGetComponent<IFeatureProcessor>(out var featureProcessor))
            foreach (var feature in features)
                featureProcessor.ProcessFeature(feature.Feature, feature.FeatureData);

        return instance;
    }

    private ConveyorView CreateConveyorView()
    {
        var instance = _prefabModule.Rent(_conveyorConfig.ViewPrefab, _parent);
        return instance;
    }

    private void ApplySplineSubdivide()
    {
        var newPoints = new List<SplinePoint>();
        var splinePoints = _splineComputer.GetPoints();
        for (var i = 0; i < splinePoints.Length; i++)
        {
            var current = splinePoints[i];
            newPoints.Add(current);

            if (!_splineRef.Closed)
                if (i == splinePoints.Length - 1)
                    break;

            var next = splinePoints.GetWrapped(i + 1);
            var center = new SplinePoint((next.position + current.position) / 2);
            newPoints.Add(center);
        }
        _splineComputer.SetPoints(newPoints.ToArray());
    }

    private void ApplyCameraRotation()
    {
        var cameraRotation = _splineRef.CameraRotation + _level.CameraRotation;
        if (cameraRotation == 0f) return;
        _parent.Rotate(Vector3.up, cameraRotation);
    }

    private Vector3 ParseWorldOffset(string[] s1, int x, int y, Quaternion rotation)
    {
        var worldOffset = new Vector3(x * _splineRef.Spacing, 0, y * _splineRef.Spacing);
        if (s1.Length > 2)
        {
            var s = s1[2].Split(',');
            var offsetX = s.Length > 0 ? ParseFloat(s[0]) : 0;
            var offsetY = s.Length > 1 ? ParseFloat(s[1]) : 0;
            var offsetZ = s.Length > 2 ? ParseFloat(s[2]) : 0;
            worldOffset += rotation * new Vector3(offsetX, offsetY, offsetZ);
        }

        return worldOffset;
    }

    private float ParseAngle(string[] s1)
    {
        return s1.Length > 1 ? ParseFloat(s1[1]) : 0f;
    }

    private float ParseFloat(string s)
    {
        return float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private void SetCarrierBlockArgs()
    {
        var carrierConfigSize = _carrierConfig.Sizes[_blockPhysicsConfig.Type];

        var slotCount = _level.SlotCount;
        if (_loseConfig.FailOnFull) slotCount++;
        var splineLength = _splineComputer.CalculateLength();
        var groupSplineLength = splineLength / slotCount;
        var groupBlockCount = groupSplineLength * carrierConfigSize.SplineLengthBlockMultiplier;
        var carrierBlockLength = carrierConfigSize.SingleColorMultiplier * 4;

        using var p = ListPool<Vector3Int>.Get(out var possibleSizes);
        if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.Free || _blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.FreePlus)
        {
            possibleSizes.Add(new Vector3Int(3, 3, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(4, 3, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(4, 4, carrierBlockLength));
        }
        else if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.FreeAdaptive ||
                 _blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.FlatCubes ||
                 _blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.NoTraffic)
        {
            possibleSizes.Add(new Vector3Int(3, 3, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(4, 3, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(4, 4, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(5, 4, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(5, 5, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(6, 5, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(6, 6, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(7, 6, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(7, 7, carrierBlockLength));
        }
        else if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.SandLoop)
        {
            possibleSizes.Add(new Vector3Int(5, 5, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(6, 5, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(6, 6, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(7, 6, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(7, 7, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(8, 7, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(8, 8, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(9, 8, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(9, 9, carrierBlockLength));
        }
        else if (_blockPhysicsConfig.Type == BlockPhysicsConfig.PhysicsType.SandLoopLite)
        {
            possibleSizes.Add(new Vector3Int(4, 4, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(5, 4, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(5, 5, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(6, 5, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(6, 6, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(7, 6, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(7, 7, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(8, 7, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(8, 8, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(9, 8, carrierBlockLength));
            possibleSizes.Add(new Vector3Int(9, 9, carrierBlockLength));
        }

        var bestSize = Vector3Int.zero;
        var bestDistance = float.MaxValue;
        foreach (var possibleSize in possibleSizes)
        {
            var size = possibleSize.x * possibleSize.y * carrierConfigSize.SingleColorMultiplier;
            var distance = Mathf.Abs(size - groupBlockCount);
            if (distance > bestDistance) continue;
            bestSize = possibleSize;
            bestDistance = distance;
        }

        var args = new CarrierBlockArgs
        {
            Size = bestSize,
            SlotCount = slotCount,
        };
        _conveyor.SetCarrierBlockArgs(args);
    }
}

public struct LevelBuildCompleteMessage
{
}

public struct CarrierBlockArgs
{
    public Vector3Int Size;
    public int SlotCount;
}
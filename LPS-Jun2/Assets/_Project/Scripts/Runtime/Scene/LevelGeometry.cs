using System;
using System.Collections.Generic;
using System.Globalization;
using Dreamteck.Splines;
using UnityEngine;

/// <summary>
/// The level build math, lifted out of the ECS systems so it has no DI and can run in edit mode.
///
/// Ported verbatim from LevelBuildSystem (spline points, carrier placement, carrier block args),
/// ConveyorSlotCreateSystem (slot layout) and Carrier (block coordinates). The editor generator is
/// the only caller today; keeping the maths here means there is exactly one implementation of it.
/// </summary>
public static class LevelGeometry
{
    // ---------------------------------------------------------------- spline

    /// <summary>Rebuilds the spline in place from a Splines.json row.</summary>
    public static void BuildSpline(SplineComputer splineComputer, SplineSheet.Layout layout)
    {
        CreateSplinePoints(splineComputer, layout);

        if (layout.Closed)
            splineComputer.Close();

        for (var i = 0; i < layout.Subdivide; i++)
            ApplySubdivide(splineComputer, layout);

        splineComputer.RebuildImmediate();
    }

    private static void CreateSplinePoints(SplineComputer splineComputer, SplineSheet.Layout layout)
    {
        foreach (var cell in layout.Data.Cells)
        {
            if (!TrySplitCell(cell, out var split, out var first)) continue;
            if (!int.TryParse(first, out _)) continue;

            foreach (var s in split)
            {
                var s2 = s.Split('-');
                if (!int.TryParse(s2[0], out var idx)) continue;
                var height = s2.Length > 1 ? ParseFloat(s2[1]) : 0f;
                CreateSplinePoint(splineComputer, layout, idx - 1, cell.X, cell.Y, height);
            }
        }
    }

    private static void CreateSplinePoint(SplineComputer splineComputer, SplineSheet.Layout layout,
        int idx, int x, int y, float height)
    {
        var offsetY = splineComputer.transform.position.y;
        var worldOffset = new Vector3(x * layout.Spacing, height + offsetY, y * layout.Spacing);
        splineComputer.SetPoint(idx, new SplinePoint(worldOffset));
    }

    private static void ApplySubdivide(SplineComputer splineComputer, SplineSheet.Layout layout)
    {
        var newPoints = new List<SplinePoint>();
        var splinePoints = splineComputer.GetPoints();

        for (var i = 0; i < splinePoints.Length; i++)
        {
            var current = splinePoints[i];
            newPoints.Add(current);

            if (!layout.Closed)
                if (i == splinePoints.Length - 1)
                    break;

            var next = splinePoints.GetWrapped(i + 1);
            newPoints.Add(new SplinePoint((next.position + current.position) / 2));
        }

        splineComputer.SetPoints(newPoints.ToArray());
    }

    // --------------------------------------------------------------- carriers

    public struct CarrierPlacement
    {
        public CarrierSheet.CarrierType Type;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    /// <summary>
    /// Every carrier described by the spline layout. Position still needs the carrier's own
    /// Pivot subtracted once the prefab exists — see <see cref="ApplyPivot"/>.
    /// </summary>
    public static List<CarrierPlacement> GetCarrierPlacements(SplineSheet.Layout layout, float conveyorHeight)
    {
        var placements = new List<CarrierPlacement>();

        foreach (var cell in layout.Data.Cells)
        {
            if (!TrySplitCell(cell, out var split, out var first)) continue;
            if (int.TryParse(first, out _)) continue; // numeric cells are spline points
            if (!Enum.TryParse(first, ignoreCase: true, out CarrierSheet.CarrierType type)) continue;

            var angle = split.Length > 1 ? ParseFloat(split[1]) : 0f;
            var rotation = Quaternion.AngleAxis(angle, Vector3.up);
            var position = ParseWorldOffset(split, cell.X, cell.Y, layout.Spacing, rotation);
            position.y -= conveyorHeight - .1f;

            placements.Add(new CarrierPlacement { Type = type, Position = position, Rotation = rotation });
        }

        return placements;
    }

    /// <summary>Carriers are positioned by their Pivot, not their transform origin.</summary>
    public static void ApplyPivot(Transform carrierTransform, Transform pivot, CarrierPlacement placement)
    {
        carrierTransform.position = placement.Position;
        carrierTransform.rotation = placement.Rotation;
        carrierTransform.position -= carrierTransform.rotation * pivot.localPosition;
    }

    private static Vector3 ParseWorldOffset(string[] split, int x, int y, float spacing, Quaternion rotation)
    {
        var worldOffset = new Vector3(x * spacing, 0, y * spacing);
        if (split.Length <= 2) return worldOffset;

        var s = split[2].Split(',');
        var offsetX = s.Length > 0 ? ParseFloat(s[0]) : 0f;
        var offsetY = s.Length > 1 ? ParseFloat(s[1]) : 0f;
        var offsetZ = s.Length > 2 ? ParseFloat(s[2]) : 0f;
        return worldOffset + rotation * new Vector3(offsetX, offsetY, offsetZ);
    }

    // -------------------------------------------------------- carrier sizing

    /// <summary>
    /// Ported from LevelBuildSystem.SetCarrierBlockArgs. Picks the block grid whose capacity is
    /// closest to what one conveyor slot group can hold.
    /// </summary>
    public static CarrierBlockArgs ComputeCarrierBlockArgs(
        CarrierConfig carrierConfig,
        BlockPhysicsConfig.PhysicsType physicsType,
        float splineLength,
        int levelSlotCount,
        bool failOnFull)
    {
        var carrierConfigSize = carrierConfig.Sizes[physicsType];

        var slotCount = levelSlotCount;
        if (failOnFull) slotCount++;

        var groupSplineLength = splineLength / slotCount;
        var groupBlockCount = groupSplineLength * carrierConfigSize.SplineLengthBlockMultiplier;
        var carrierBlockLength = carrierConfigSize.SingleColorMultiplier * 4;

        var possibleSizes = GetPossibleSizes(physicsType, carrierBlockLength);

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

        return new CarrierBlockArgs { Size = bestSize, SlotCount = slotCount };
    }

    private static List<Vector3Int> GetPossibleSizes(BlockPhysicsConfig.PhysicsType type, int carrierBlockLength)
    {
        var sizes = new List<Vector3Int>();

        switch (type)
        {
            case BlockPhysicsConfig.PhysicsType.Free:
            case BlockPhysicsConfig.PhysicsType.FreePlus:
                Add(3, 3); Add(4, 3); Add(4, 4);
                break;

            case BlockPhysicsConfig.PhysicsType.FreeAdaptive:
            case BlockPhysicsConfig.PhysicsType.FlatCubes:
            case BlockPhysicsConfig.PhysicsType.NoTraffic:
                Add(3, 3); Add(4, 3); Add(4, 4); Add(5, 4); Add(5, 5);
                Add(6, 5); Add(6, 6); Add(7, 6); Add(7, 7);
                break;

            case BlockPhysicsConfig.PhysicsType.SandLoop:
                Add(5, 5); Add(6, 5); Add(6, 6); Add(7, 6); Add(7, 7);
                Add(8, 7); Add(8, 8); Add(9, 8); Add(9, 9);
                break;

            case BlockPhysicsConfig.PhysicsType.SandLoopLite:
                Add(4, 4); Add(5, 4); Add(5, 5); Add(6, 5); Add(6, 6);
                Add(7, 6); Add(7, 7); Add(8, 7); Add(8, 8); Add(9, 8); Add(9, 9);
                break;
        }

        return sizes;

        void Add(int x, int y) => sizes.Add(new Vector3Int(x, y, carrierBlockLength));
    }

    /// <summary>The derived counts Conveyor.SetCarrierBlockArgs would compute at runtime.</summary>
    public static Vector3Int GetBlockSize(CarrierBlockArgs args) => args.Size;

    public static int GetGroupBlockCount(CarrierConfig carrierConfig, BlockPhysicsConfig.PhysicsType physicsType,
        CarrierBlockArgs args)
    {
        var carrierSize = carrierConfig.Sizes[physicsType];
        return args.Size.x * args.Size.y * carrierSize.SingleColorMultiplier;
    }

    public static int GetGroupSlotCount(CarrierConfig carrierConfig, BlockPhysicsConfig.PhysicsType physicsType,
        CarrierBlockArgs args)
    {
        var carrierSize = carrierConfig.Sizes[physicsType];
        return GetGroupBlockCount(carrierConfig, physicsType, args) / carrierSize.SlotElementCount;
    }

    // ------------------------------------------------------------ conveyor slots

    public struct SlotLayout
    {
        public int TotalSlotCount;
        public int ExtraSlotCount;
        public float ConveyorSpeed;
        public float SlotCollisionDistance;
        public int LengthBasedSlotCount;
        public double[] Percents;
    }

    /// <summary>Ported from ConveyorSlotCreateSystem.CreateSlots.</summary>
    public static SlotLayout ComputeSlotLayout(
        SplineComputer splineComputer,
        ConveyorConfig conveyorConfig,
        PaceConfig paceConfig,
        ConveyorSystemConfig conveyorSystemConfig,
        int groupSlotCount,
        int slotCount)
    {
        var length = splineComputer.CalculateLength();
        var totalSlotCount = groupSlotCount * slotCount;

        var configuredCollisionDistance = conveyorConfig.SlotCollisionDistance;
        var lengthBasedSlotCount = NormalizeSlotCount(
            Mathf.FloorToInt(length / configuredCollisionDistance), groupSlotCount);

        var lengthSlot = (float)lengthBasedSlotCount / groupSlotCount;
        var conveyorSpeed = conveyorConfig.Speed +
                            (conveyorConfig.Speed * (lengthSlot / slotCount) - conveyorConfig.Speed) * .5f;
        conveyorSpeed *= paceConfig.ConveyorSpeedMultiplier;

        var extraSlotCount = 0;
        if (conveyorSystemConfig.LoanSystem)
        {
            extraSlotCount = groupSlotCount * conveyorConfig.LoanSystemSlotCount;
            totalSlotCount += extraSlotCount;
        }

        var adaptiveSize = length / totalSlotCount;
        var slotCollisionDistance = conveyorSystemConfig.AdaptiveSize
            ? adaptiveSize
            : Mathf.Min(configuredCollisionDistance, adaptiveSize);

        var percents = new double[totalSlotCount];
        for (var i = 0; i < totalSlotCount; i++)
            percents[i] = splineComputer.Travel(0d, slotCollisionDistance * i);

        return new SlotLayout
        {
            TotalSlotCount = totalSlotCount,
            ExtraSlotCount = extraSlotCount,
            ConveyorSpeed = conveyorSpeed,
            SlotCollisionDistance = slotCollisionDistance,
            LengthBasedSlotCount = lengthBasedSlotCount / groupSlotCount,
            Percents = percents
        };
    }

    private static int NormalizeSlotCount(int count, int groupSlotCount)
    {
        var a = count % groupSlotCount;
        var b = groupSlotCount - a;
        if (b > a) count -= a;
        else count += b;
        return count;
    }

    // ------------------------------------------------------------------ blocks

    /// <summary>Ported from Carrier.GetBlockCoordinate(int).</summary>
    public static Vector3Int GetBlockCoordinate(int index, Vector3Int blockSize)
    {
        var coordinate = Vector3Int.zero;
        var idx = index % (blockSize.x * blockSize.y);
        coordinate.x = idx % blockSize.x;
        coordinate.y = idx / blockSize.x;
        coordinate.z = index / (blockSize.x * blockSize.y);

        if (coordinate.y % 2 != 0)
            coordinate.x = blockSize.x - 1 - idx % blockSize.x;

        return coordinate;
    }

    /// <summary>Ported from Carrier.CoordinateToLocalPosition.</summary>
    public static Vector3 CoordinateToLocalPosition(Vector3Int coordinate, Vector3Int blockSize,
        CarrierConfig.SizeArgs configSize)
    {
        var size = configSize.ContainerSize;
        var x = Mathf.Lerp(0f, size.x, coordinate.x / (float)(blockSize.x - 1));
        var y = Mathf.Lerp(0f, size.y, coordinate.y / (float)(blockSize.y - 1));
        var z = Mathf.Lerp(0f, size.z, coordinate.z / (float)(blockSize.z - 1));
        x = float.IsNaN(x) ? 0f : x;
        y = float.IsNaN(y) ? 0f : y;
        z = float.IsNaN(z) ? 0f : z;

        var offset = configSize.ContainerOffset;
        return new Vector3(x + offset.x, y + offset.y, z + offset.z);
    }

    // ------------------------------------------------------------------ shared

    private static bool TrySplitCell(SplineSheet.Cell cell, out string[] split, out string first)
    {
        split = null;
        first = null;

        var data = cell.Data;
        if (string.IsNullOrEmpty(data)) return false;
        if (data[0] == '-') return false;

        split = data.Split('\n');
        first = split[0].Split('-')[0];
        return true;
    }

    private static float ParseFloat(string s)
    {
        return float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}

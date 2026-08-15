using Dreamteck.Splines;
using UnityEngine;

public static partial class Extensions
{
    public static double TravelUnclamped(this SplineFollower f, double start, float distance,
        Spline.Direction direction = Spline.Direction.Forward)
    {
        return f.spline.TravelUnclamped(start, distance, direction);
    }

    public static double TravelUnclamped(this SplineComputer s, double start, float distance,
        Spline.Direction direction = Spline.Direction.Forward)
    {
        var travel = start;
        while (distance > .01f)
        {
            travel = s.Travel(start, distance, out var moved, direction);
            distance -= moved;
            start = direction == Spline.Direction.Forward ? 0d : 1d;
        }
        return travel;
    }

    public static float CalculateLengthUnclamped(this SplineComputer s, double from, double to)
    {
        if (to >= from)
            return s.CalculateLength(from, to);

        return s.CalculateLength(from, 1) + s.CalculateLength(0, to);
    }

    public static bool IsInRange(this SplineComputer s, double current, double from, double to)
    {
        if (from > to)
            return current > from || to > current;
        return current > from && current < to;
    }

    public static float DistanceTo(this SplineComputer s, double from, double to)
    {
        var left = from > to ? to : from;
        var right = from > to ? from : to;
        return s.CalculateLengthUnclamped(left, right);
    }

    public static float Angle(this SplineComputer s, double from, double to)
    {
        var fromSample = s.Evaluate(from);
        var toSample = s.Evaluate(to);
        return Vector3.Angle(fromSample.forward, toSample.forward);
    }

    public static float SignedAngle(this SplineComputer s, double from, double to)
    {
        var fromSample = s.Evaluate(from);
        var toSample = s.Evaluate(to);
        return Vector3.SignedAngle(fromSample.forward, toSample.forward, Vector3.up);
    }
}
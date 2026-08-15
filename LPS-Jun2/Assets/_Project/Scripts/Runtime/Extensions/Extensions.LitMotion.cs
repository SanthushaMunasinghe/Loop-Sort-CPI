using Coffee.UIEffects;
using Dreamteck.Splines;
using LitMotion;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Pool;

public static partial class Extensions
{
    private static readonly MaterialPropertyBlock PropertyBlock = new();

    public static MotionHandle BindToLocalScaleNonNegative<TOptions, TAdapter>(
        this MotionBuilder<Vector3, TOptions, TAdapter> builder, Transform transform)
        where TOptions : unmanaged, IMotionOptions
        where TAdapter : unmanaged, IMotionAdapter<Vector3, TOptions>
    {
        return builder.Bind(transform, static (x, t) =>
        {
            t.localScale = Vector3.Max(Vector3H.AlmostZero, x);
        });
    }

    public static MotionHandle BindToBlendShape<TOptions, TAdapter>(
        this MotionBuilder<float, TOptions, TAdapter> builder, SkinnedMeshRenderer renderer, int idx)
        where TOptions : unmanaged, IMotionOptions
        where TAdapter : unmanaged, IMotionAdapter<float, TOptions>
    {
        return builder.Bind((renderer, idx), static (x, tuple) =>
        {
            var (renderer, idx) = tuple;
            renderer.SetBlendShapeWeight(idx, x);
        });
    }

    public static MotionHandle BindToFollowerSpeed<TOptions, TAdapter>(
        this MotionBuilder<float, TOptions, TAdapter> builder, SplineFollower follower)
        where TOptions : unmanaged, IMotionOptions
        where TAdapter : unmanaged, IMotionAdapter<float, TOptions>
    {
        return builder.Bind(follower, static (v, follower) => follower.followSpeed = v);
    }

    public static MotionHandle BindToTransitionRate<TOptions, TAdapter>(
        this MotionBuilder<float, TOptions, TAdapter> builder, UIEffect uiEffect)
        where TOptions : unmanaged, IMotionOptions
        where TAdapter : unmanaged, IMotionAdapter<float, TOptions>
    {
        return builder.Bind(uiEffect, static (v, uiEffect) => uiEffect.transitionRate = v);
    }

    public static MotionHandle BindToCenterOffset<TOptions, TAdapter>(
        this MotionBuilder<Vector2, TOptions, TAdapter> builder, CinemachineGroupFraming groupFraming)
        where TOptions : unmanaged, IMotionOptions
        where TAdapter : unmanaged, IMotionAdapter<Vector2, TOptions>
    {
        return builder.Bind(groupFraming, static (v, groupFraming) => groupFraming.CenterOffset = v);
    }

    public static MotionHandle BindToTimeScale<TOptions, TAdapter>(
        this MotionBuilder<float, TOptions, TAdapter> builder)
        where TOptions : unmanaged, IMotionOptions
        where TAdapter : unmanaged, IMotionAdapter<float, TOptions>
    {
        return builder.Bind(static v => Time.timeScale = v);
    }

    public static MotionHandle BindToPropertyBlock<TOptions, TAdapter>(
        this MotionBuilder<float, TOptions, TAdapter> builder, Renderer r, string propertyName, int idx = 0)
        where TOptions : unmanaged, IMotionOptions
        where TAdapter : unmanaged, IMotionAdapter<float, TOptions>
    {
        return builder.Bind((r, propertyName, idx), static (v, tuple) =>
        {
            var (r, propertyName, idx) = tuple;

            using var p = ListPool<Material>.Get(out var materials);
            r.GetSharedMaterials(materials);
            if (idx >= materials.Count) return;

            PropertyBlock.Clear();
            PropertyBlock.SetFloat(propertyName, v);
            r.SetPropertyBlock(PropertyBlock, idx);
        });
    }

    public static MotionHandle BindToJump<TOptions, TAdapter>(
        this MotionBuilder<float, TOptions, TAdapter> builder, Transform transform, Vector3 toPosition, float jumpPower)
        where TOptions : unmanaged, IMotionOptions
        where TAdapter : unmanaged, IMotionAdapter<float, TOptions>
    {
        var fromPosition = transform.position;
        return builder.Bind((transform, fromPosition, toPosition, jumpPower), static (t, tuple) =>
        {
            var (transform, fromPosition, toPosition, jumpPower) = tuple;
            var lerpPosition = Vector3.Lerp(fromPosition, toPosition, t);
            lerpPosition.y += jumpPower * Mathf.PI * t * Mathf.Sin(t * Mathf.PI);
            transform.position = lerpPosition;
        });
    }

    public static MotionHandle BindToLocalScaleAround<TOptions, TAdapter>(
        this MotionBuilder<Vector3, TOptions, TAdapter> builder, Transform t, Vector3 pivot)
        where TOptions : unmanaged, IMotionOptions
        where TAdapter : unmanaged, IMotionAdapter<Vector3, TOptions>
    {
        return builder.Bind((t, pivot), static (v, tuple) =>
        {
            var (t, pivot) = tuple;
            t.ScaleAround(pivot, v);
        });
    }

    /// <summary>
    /// Link the motion lifecycle to the target object.
    /// </summary>
    /// <param name="handle">This motion handle</param>
    /// <param name="target">Target object</param>
    public static MotionHandle AddTo(this MotionHandle handle, GameBehaviourBase target)
    {
        target.Register(handle);
        return handle;
    }

    public static bool HasPlayingMotions(this CompositeMotionHandle motions)
    {
        foreach (var motion in motions)
            if (motion.IsPlaying())
                return true;
        return false;
    }
}
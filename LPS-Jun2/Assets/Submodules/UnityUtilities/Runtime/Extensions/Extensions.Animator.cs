using System;
using System.Collections.Generic;
using UnityEngine;

public static partial class Extensions
{
    /// <summary>
    /// Copies the avatar and runtime animator controller from one Animator to another and then rebinds the target Animator.
    /// </summary>
    /// <param name="target">The Animator that will receive the copied properties.</param>
    /// <param name="source">The Animator from which the properties will be copied.</param>
    public static void CopyFrom(this Animator target, Animator source)
    {
        target.avatar = source.avatar;
        target.runtimeAnimatorController = source.runtimeAnimatorController;
        target.applyRootMotion = source.applyRootMotion;
        target.Rebind();
    }

    /// <summary>
    /// Attempts to retrieve the runtime parameter values from the specified Animator.
    /// Populates a list with the parameter names, types, and current values (float, int, bool).
    /// </summary>
    /// <param name="target">The Animator from which to retrieve the parameters.</param>
    /// <param name="runtimeParameters">The list that will be filled with the runtime parameters of the animator.</param>
    /// <returns>True if parameters were successfully retrieved; false if the runtimeAnimatorController or list are null.</returns>
    public static bool TryGetRuntimeParameterValues(this Animator target, List<AnimatorRuntimeParameter> runtimeParameters)
    {
        if (target.runtimeAnimatorController == null) return false;
        if (runtimeParameters == null) return false;

        var parameters = target.parameters;
        foreach (var parameter in parameters)
        {
            var nameHash = parameter.nameHash;
            var animatorRuntimeParameter = new AnimatorRuntimeParameter
            {
                Type = parameter.type,
                NameHash = nameHash
            };
            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Float:
                    animatorRuntimeParameter.FloatValue = target.GetFloat(nameHash);
                    break;
                case AnimatorControllerParameterType.Int:
                    animatorRuntimeParameter.IntValue = target.GetInteger(nameHash);
                    break;
                case AnimatorControllerParameterType.Bool:
                    animatorRuntimeParameter.BoolValue = target.GetBool(nameHash);
                    break;
                case AnimatorControllerParameterType.Trigger:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            runtimeParameters.Add(animatorRuntimeParameter);
        }

        return true;
    }

    /// <summary>
    /// Sets the runtime parameter values on the specified Animator from a list of AnimatorRuntimeParameters.
    /// Updates the Animator's parameters based on the types (float, int, bool).
    /// </summary>
    /// <param name="target">The Animator on which to set the runtime parameter values.</param>
    /// <param name="runtimeParameters">The list containing parameter types, names, and values to set on the animator.</param>
    /// <returns>True if parameters were successfully set; false if the runtimeAnimatorController or list are null.</returns>
    public static bool SetRuntimeParameterValues(this Animator target, List<AnimatorRuntimeParameter> runtimeParameters)
    {
        if (target.runtimeAnimatorController == null) return false;
        if (runtimeParameters == null) return false;

        foreach (var runtimeParameter in runtimeParameters)
        {
            var nameHash = runtimeParameter.NameHash;
            switch (runtimeParameter.Type)
            {
                case AnimatorControllerParameterType.Float:
                    target.SetFloat(nameHash, runtimeParameter.FloatValue);
                    break;
                case AnimatorControllerParameterType.Int:
                    target.SetInteger(nameHash, runtimeParameter.IntValue);
                    break;
                case AnimatorControllerParameterType.Bool:
                    target.SetBool(nameHash, runtimeParameter.BoolValue);
                    break;
                case AnimatorControllerParameterType.Trigger:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return true;
    }
}

public struct AnimatorRuntimeParameter
{
    public AnimatorControllerParameterType Type { get; set; }
    public int NameHash { get; set; }
    public float FloatValue { get; set; }
    public int IntValue { get; set; }
    public bool BoolValue { get; set; }
}
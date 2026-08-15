using System;
using UnityEngine;

public sealed class CarrierConfig : Data
{
    public GenericDictionary<BlockPhysicsConfig.PhysicsType, SizeArgs> Sizes;

    [Serializable]
    public struct SizeArgs
    {
        public Vector3 ContainerSize;
        public Vector3 ContainerOffset;
        public float ScaleMultiplier;
        public int SingleColorMultiplier;
        public int SlotElementCount;
        public float SplineLengthBlockMultiplier;
    }
}
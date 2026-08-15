using System;

[Serializable]
public class BlockPhysicsConfig
{
    public PhysicsType Type = PhysicsType.FreeAdaptive;
    public bool Sphere;

    public enum PhysicsType
    {
        None = 0,
        Free = 1, FreeAdaptive = 2, FreePlus = 3,
        SandLoop = 4, SandLoopLite = 5, FlatCubes = 6, NoTraffic = 7
    }
}
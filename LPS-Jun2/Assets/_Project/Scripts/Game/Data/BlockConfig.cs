using StatefulUI.Runtime.Core;
using UnityEngine;

public sealed class BlockConfig : Data
{
    [Header("Block Mesh")]
    public Mesh BeveledMesh;
    public Mesh FlatMesh;
    public Mesh CornerMesh;
    public Mesh MiddleMesh;
    public Mesh SphereMesh;

    [Header("Group Mesh")]
    public Mesh GroupBeveledMesh;
    public Mesh GroupCornerMesh;
    public Mesh GroupCenterMesh;

    [Header("Prefab")]
    public ConveyorBlock ConveyorBlock;
    public StatefulComponent BlockGroupShapePrefab;

    [Header("Physics")]
    public float ConveyorForce;
    public float Mass;
    public float Drag;
    public float AngularDrag;
    public float InitialImpulseForce;
    public float MaxLinearVelocity;

    [Header("Physics Force Direction")]
    public float ForceDirectionOffset;
    public int BakeSampleCount;
    public float CellSize;
    public int MaxSearchRadius;
}
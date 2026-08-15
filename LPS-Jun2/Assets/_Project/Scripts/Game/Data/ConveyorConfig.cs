using UnityEngine;

public sealed class ConveyorConfig : Data
{
    [Header("General")]
    public float Speed;
    public float ConveyorHeight;
    public GameObject EndPrefab;
    public GameObject ShinePrefab;
    public GameObject ColliderPrefab;

    [Header("Arrow")]
    public GameObject ArrowPrefab;
    public float ArrowPerDistance;

    [Header("Slot")]
    public GameObject SlotPrefab;
    public float SlotElementOffset;
    public float SlotCollisionDistance;
    public int ClosestSlotDepth;

    [Header("Conveyor")]
    public float CarrierTriggerOffset;
    public int LoanSystemSlotCount;

    [Header("Color")]
    public Color SideRedColor;
    public float RedRatio;
    public float ColorPingPongModifier;
    public float ColorLerpModifier;

    [Header("View")]
    public ConveyorView ViewPrefab;
}
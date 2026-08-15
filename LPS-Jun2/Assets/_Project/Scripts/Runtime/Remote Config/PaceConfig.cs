using System;

[Serializable]
public class PaceConfig
{
    public float ConveyorSpeedMultiplier = 1.1f;
    public float BlockAnimationDurationMultiplier = .5f;
    public float HoldSpeedMultiplier = 1f;

    public bool FasterWhenConveyorFull;
    public bool SlowerWhenConveyorEmpty = true;
    public bool HoldToSpeed;
    public bool SlowerWhenGiveUp;

    public static float SpeedMultiplier = 1f;
}
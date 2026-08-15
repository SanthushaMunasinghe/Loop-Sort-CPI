using UnityEngine;
using VContainer;

public sealed class Surface : GameBehaviourBase
{
    [Inject] private ConveyorConfig _conveyorConfig;

    protected override void Start()
    {
        base.Start();

        transform.position = Vector3.down * _conveyorConfig.ConveyorHeight;
    }
}
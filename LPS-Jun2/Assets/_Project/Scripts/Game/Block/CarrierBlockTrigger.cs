using UnityEngine;

/// <summary>
/// Pairs a generated trigger volume with the carrier it feeds.
///
/// BlockTriggerSystem used to create these at run time and capture the carrier in a closure. Now
/// that the triggers are generated into the scene, the pairing has to be serialized instead.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(BlockTrigger))]
public sealed class CarrierBlockTrigger : MonoBehaviour
{
    [SerializeField] private Carrier _carrier;

    public Carrier Carrier => _carrier;

#if UNITY_EDITOR
    public void SetCarrier(Carrier carrier) => _carrier = carrier;
#endif
}

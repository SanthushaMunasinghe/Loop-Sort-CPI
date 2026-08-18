using UnityEngine;

/// <summary>
/// Marks the one hand-placed trigger blocks pass through to be routed to a compatible, not-yet-full
/// Empty carrier anywhere in the level, rather than one tied to a specific carrier (compare
/// CarrierBlockTrigger). Not generated — you create and place this one yourself.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(BlockTrigger))]
public sealed class GlobalTrigger : MonoBehaviour
{
}

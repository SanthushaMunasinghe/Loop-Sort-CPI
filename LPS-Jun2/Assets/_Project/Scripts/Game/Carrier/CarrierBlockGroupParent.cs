using UnityEngine;

/// <summary>
/// Marks a container the Sandbox's Apply Carrier Modes Start fill creates under a carrier's
/// BlockParent to hold one group block's worth of blocks. Purely structural — lets
/// LevelSandboxGenerator.ClearCarrierBlocks find and remove these containers again; nothing reads
/// it at run time.
/// </summary>
public sealed class CarrierBlockGroupParent : MonoBehaviour
{
}

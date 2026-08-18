using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;

/// <summary>
/// Drives a level that was generated into the scene by LevelSandboxGenerator.
///
/// Nothing is built at play time. This applies the values the generator baked (which the runtime
/// can no longer derive, because the sheets are not loaded), then publishes the handful of messages
/// that the deleted build / camera / animation chain used to publish. LevelBuildCompleteMessage in
/// particular is not just a "spawn now" signal — it is what initialises BlockTransferSystem and
/// triggers BlockPhysicsSystem's spline bake.
///
/// Runs at +50 so every GameBehaviourBase (order 0) has been injected by SceneScope (order -100)
/// before we touch the conveyor.
/// </summary>
[DefaultExecutionOrder(50)]
public sealed class LevelSandbox : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Row number in Levels.json this level was generated from. Informational at run time.")]
    [SerializeField] private int _level = 1;

    [Header("Scene")]
    [SerializeField] private Conveyor _conveyor;
    [SerializeField] private Transform _carriersRoot;
    [SerializeField] private Transform _slotsRoot;
    [SerializeField] private Transform _triggersRoot;

    [Header("Baked (press Re-bake Derived Values after editing the spline)")]
    [SerializeField] private CarrierBlockArgs _carrierBlockArgs;
    [SerializeField] private float _conveyorSpeed;
    [SerializeField] private float _slotCollisionDistance;
    [SerializeField] private int _lengthBasedSlotCount;

    [Inject] private SceneScope _sceneScope;

    [Inject] private IPublisher<LevelBuildCompleteMessage> _levelBuildCompletePub;
    [Inject] private IPublisher<SlotCreateCompleteMessage> _slotCreateCompletePub;
    [Inject] private IPublisher<CarrierBlockCreateCompleteMessage> _carrierBlockCreateCompletePub;
    [Inject] private IPublisher<LevelAnimationEnterCompleteMessage> _levelAnimationEnterCompletePub;

    public int Level => _level;
    public Transform CarriersRoot => _carriersRoot;
    public Transform SlotsRoot => _slotsRoot;
    public Transform TriggersRoot => _triggersRoot;

    public CarrierBlockArgs CarrierBlockArgs => _carrierBlockArgs;
    public float ConveyorSpeed => _conveyorSpeed;
    public float SlotCollisionDistance => _slotCollisionDistance;
    public int LengthBasedSlotCount => _lengthBasedSlotCount;

    private void Awake()
    {
        LifetimeScopeH.InjectIfNotScoped<SceneScope>(this);

        if (_conveyor == null)
        {
            Debug.LogError($"<b>{nameof(LevelSandbox)}</b>: no Conveyor assigned. Generate a level first.", this);
            enabled = false;
            return;
        }

        // Every downstream system reads GroupSlotCount / GroupBlockCount / BlockSize off the
        // conveyor, so this has to land before anything else runs.
        _conveyor.SetCarrierBlockArgs(_carrierBlockArgs);

        AdoptCarrierBlocks();
    }

    private void Start()
    {
        PublishBuildMessages().Forget();
    }

    /// <summary>
    /// Carriers hold their blocks as plain children in the generated scene. Register them with the
    /// carrier's block list without the async placement motion AddBlock would normally run.
    ///
    /// Only SceneScope's hand-curated Start/Empty carriers are adopted — not every Carrier under
    /// CarriersRoot. A carrier left out (disabled, or just not added) never gets its blocks touched
    /// here, which matters for a disabled one specifically: Unity never runs Awake() under a
    /// disabled hierarchy, so its blocks' Rigidbody/MeshFilter/etc. are never assigned, and touching
    /// them here would throw.
    /// </summary>
    private void AdoptCarrierBlocks()
    {
        foreach (var carrier in _sceneScope.AllCarriers)
            carrier.AdoptAuthoredBlocks();
    }

    /// <summary>
    /// Morpeh defers SystemBase.OnAwake (and therefore every message subscription) to the first
    /// World.Update, which happens after Start. Publishing a frame later is what makes these
    /// messages observable at all.
    /// </summary>
    private async UniTaskVoid PublishBuildMessages()
    {
        var token = this.GetCancellationTokenOnDestroy();
        await UniTask.NextFrame(token);

        _levelBuildCompletePub.Publish(new LevelBuildCompleteMessage());

        await UniTask.NextFrame(token);

        _slotCreateCompletePub.Publish(new SlotCreateCompleteMessage());
        _carrierBlockCreateCompletePub.Publish(new CarrierBlockCreateCompleteMessage());

        // Stands in for LevelAnimationSystem, which publishes this immediately anyway whenever
        // LevelAnimationConfig.Enter is false (it defaults to false).
        _levelAnimationEnterCompletePub.Publish(new LevelAnimationEnterCompleteMessage());
    }

#if UNITY_EDITOR
    public void SetBakedValues(int level, Conveyor conveyor, Transform carriersRoot, Transform slotsRoot,
        Transform triggersRoot, CarrierBlockArgs carrierBlockArgs, LevelGeometry.SlotLayout slotLayout)
    {
        _level = level;
        _conveyor = conveyor;
        _carriersRoot = carriersRoot;
        _slotsRoot = slotsRoot;
        _triggersRoot = triggersRoot;
        _carrierBlockArgs = carrierBlockArgs;
        _conveyorSpeed = slotLayout.ConveyorSpeed;
        _slotCollisionDistance = slotLayout.SlotCollisionDistance;
        _lengthBasedSlotCount = slotLayout.LengthBasedSlotCount;
    }
#endif
}

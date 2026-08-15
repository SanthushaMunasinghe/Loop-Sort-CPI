using Lean.Touch;
using Unity.Cinemachine;
using UnityEngine;
using VContainer;

public sealed class ShuffleBooster : BoosterBase
{
    [Inject] private CinemachineCamera _sceneCinemachineCamera;
    [Inject] private ShuffleBoosterSystem _shuffleBoosterSystem;

    protected override void Start()
    {
        base.Start();

        _shuffleBoosterSystem.ShowHighlight();

        Cinemachine.Target = _sceneCinemachineCamera.Target;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        _shuffleBoosterSystem.HideHighlight();
    }

    public override bool CanSelect(IObjectResolver resolver)
    {
        return resolver.TryResolve<ShuffleBoosterSystem>(out var system) && system.CanShuffle();
    }

    public override void Intercept(LeanFinger finger, RaycastHit hitInfo)
    {
        base.Intercept(finger, hitInfo);

        var carrier = hitInfo.transform.GetComponentInParent<Carrier>();
        if (carrier == null)
        {
            System.TryCancel();
            return;
        }

        var shuffled = _shuffleBoosterSystem.TryShuffle(carrier);
        if (!shuffled)
        {
            System.TryCancel();
            return;
        }

        System.Complete();
    }
}
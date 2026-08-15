using Cysharp.Threading.Tasks;
using Lean.Touch;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public abstract class BoosterBase : GameBehaviourBase, ITouchInterceptor
{
    [Inject] protected BoosterSystem System;
    [Inject] protected HapticModule HapticModule;

    protected CinemachineCamera Cinemachine { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        Cinemachine = GetComponentInChildren<CinemachineCamera>();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        HandleCameraBlend();
    }

    private async UniTaskVoid HandleCameraBlend()
    {
        if (Cinemachine == null) return;

        using var p = ListPool<MonoBehaviour>.Get(out var tempList);
        Cinemachine.GetComponents(tempList);
        foreach (var behaviour in tempList)
            behaviour.enabled = true;

        Cinemachine.Priority = -1;
        Cinemachine.transform.parent = null;
        await UniTask.WaitWhile(Cinemachine.IsParticipatingInBlend);

        if (Cinemachine == null) return;
        Destroy(Cinemachine.gameObject);
    }

    public virtual bool CanSelect(IObjectResolver resolver)
    {
        return true;
    }

    public virtual bool TryCancel()
    {
        return true;
    }

    public virtual void Intercept(LeanFinger finger, RaycastHit hitInfo)
    {
    }
}
using UnityEngine;

public abstract class EffectBase : MonoBehaviour
{
    [SerializeField] protected bool UnscaledTime;

    public float GetEffectTime() => UnscaledTime ? Time.unscaledTime : Time.time;
    public float GetEffectDeltaTime() => UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
}
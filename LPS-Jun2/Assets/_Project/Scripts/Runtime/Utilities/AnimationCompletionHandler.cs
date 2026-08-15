using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class AnimationCompletionHandler : MonoBehaviour
{
    private UniTaskCompletionSource _completionTask;

    private void OnEnable()
    {
        _completionTask = new UniTaskCompletionSource();
    }

    public void OnComplete() => _completionTask.TrySetResult();

    public UniTask OnCompleteAsync() => _completionTask.Task;
}
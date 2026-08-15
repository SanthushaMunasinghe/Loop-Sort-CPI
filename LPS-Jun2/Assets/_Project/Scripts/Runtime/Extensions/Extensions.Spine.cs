using System.Threading;
using Cysharp.Threading.Tasks;
using Spine;

public static partial class Extensions
{
    public static async UniTask WaitUntilComplete(this TrackEntry trackEntry, CancellationToken token = default)
    {
        while (!trackEntry.IsComplete)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }

    public static async UniTask WaitUntilFirstEvent(this TrackEntry trackEntry, CancellationToken token = default)
    {
        var wait = true;
        trackEntry.Event += (_, _) => wait = false;
        while (wait)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }
}
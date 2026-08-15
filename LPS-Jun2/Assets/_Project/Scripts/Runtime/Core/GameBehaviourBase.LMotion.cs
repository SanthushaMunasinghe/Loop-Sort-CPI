using LitMotion;

public partial class GameBehaviourBase
{
    private CompositeMotionHandle _objectMotionHandle;

    public void Register(MotionHandle handle)
    {
        _objectMotionHandle ??= new CompositeMotionHandle();
        _objectMotionHandle.Add(handle);
    }
}
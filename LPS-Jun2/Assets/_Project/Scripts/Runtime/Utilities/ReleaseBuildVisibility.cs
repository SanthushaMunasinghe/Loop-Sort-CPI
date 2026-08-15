using UnityEngine;

public sealed class ReleaseBuildVisibility : MonoBehaviour
{
    [SerializeField] private VisibilityState State = VisibilityState.Hide;

    private enum VisibilityState
    {
        Show,
        Hide,
        Destroy
    }

#if RELEASE_BUILD
    private void Awake()
    {
        switch (State)
        {
            case VisibilityState.Show:
                gameObject.SetActive(true);
                break;
            case VisibilityState.Hide:
                gameObject.SetActive(false);
                break;
            case VisibilityState.Destroy:
                Destroy(gameObject);
                break;
            default:
                throw new System.ArgumentOutOfRangeException();
        }
    }
#endif
}
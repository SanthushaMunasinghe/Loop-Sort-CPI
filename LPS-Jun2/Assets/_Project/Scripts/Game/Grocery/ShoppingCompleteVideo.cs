using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// The full-screen video that closes a Use Shopping Cart Required Types level. ShoppingCartExit
/// calls Play once every cart has completed and left; the hand pointer is switched off first so it
/// isn't left drawn over the video.
///
/// The Video Player on this object does the rest — it renders over the camera and plays the clip's
/// own audio track.
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public sealed class ShoppingCompleteVideo : MonoBehaviour
{
    [Tooltip("Hidden just before the video starts.")]
    [SerializeField] private GameObject _handPointer;

    private VideoPlayer _player;

    private void Awake()
    {
        _player = GetComponent<VideoPlayer>();

        // Decoded up front, so the first frame is on screen the moment the last cart is gone.
        _player.Prepare();
    }

    public void Play()
    {
        if (_handPointer != null) _handPointer.SetActive(false);
        _player.Play();
    }
}

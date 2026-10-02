using UnityEngine;

/// <summary>
/// The shopper standing behind a Shopping Cart. Both hands are IK-pinned to the cart's handle the
/// whole time, on top of whatever the Animator is playing — so the idle still breathes and sways
/// through the rest of the body while the hands stay put on the bar.
///
/// ShoppingCartExit calls StartWalking as the cart sets off: the controller cross-fades into the
/// walk, and its playback speed is driven by how fast the cart is actually carrying the shopper
/// along their own facing — backward while the cart reverses out of its row, forward once it has
/// turned and is leaving — so the feet keep pace with the ground instead of sliding over it.
///
/// Has to sit on the Animator's own GameObject (OnAnimatorIK is only sent there), and the
/// controller's base layer needs IK Pass ticked.
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class CartPusher : MonoBehaviour
{
    private static readonly int IsWalkingParam = Animator.StringToHash("IsWalking");
    private static readonly int WalkSpeedParam = Animator.StringToHash("WalkSpeed");

    [Header("Grip")]
    [Tooltip("Where the left wrist goes and how the hand is turned: its forward is the way the fingers " +
             "point, its up the back of the hand. Parented to the cart, not the shopper.")]
    [SerializeField] private Transform _leftGrip;

    [Tooltip("Same as Left Grip, for the right hand.")]
    [SerializeField] private Transform _rightGrip;

    [Range(0f, 1f)]
    [SerializeField] private float _gripWeight = 1f;

    [Header("Walk")]
    [Tooltip("Ground the walk clip covers per second at playback speed 1, in this object's own units " +
             "(so before its scale). Measured off the clip: how fast a planted foot travels back under the hips.")]
    [Min(0.01f)]
    [SerializeField] private float _strideSpeed = .92f;

    [Tooltip("Cap on the walk's playback speed, either direction. The cart's real pace is matched up to " +
             "this; past it the feet slide rather than the legs turning into a blur.")]
    [Min(0.01f)]
    [SerializeField] private float _maxPlaybackSpeed = 10f;

    [Header("Colour")]
    [Tooltip("The shopper's body. Painted through a property block, so the shared material is left alone.")]
    [SerializeField] private Renderer _bodyRenderer;

    [Tooltip("The shopper's cap. Keeps its own material colour unless their cart has a required type.")]
    [SerializeField] private Renderer _capRenderer;

    [Tooltip("What the shopper wears unless their cart has a required type — then SceneScope paints " +
             "them that type's Shopper Colour and Cap Colour instead.")]
    [SerializeField] private Color _defaultColor = new(.25f, .55f, 1f);

    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    private Animator _animator;
    private MaterialPropertyBlock _propertyBlock;
    private Vector3 _lastPosition;
    private bool _isWalking;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        Paint(_bodyRenderer, _defaultColor);
    }

    public void SetColors(Color bodyColor, Color capColor)
    {
        Paint(_bodyRenderer, bodyColor);
        Paint(_capRenderer, capColor);
    }

    private void Paint(Renderer target, Color color)
    {
        if (target == null) return;

        _propertyBlock ??= new MaterialPropertyBlock();
        target.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(ColorProperty, color);
        target.SetPropertyBlock(_propertyBlock);
    }

    /// <summary>Cross-fades into the walk. Its pace then follows the cart every frame until StopWalking.</summary>
    public void StartWalking()
    {
        if (_isWalking) return;
        _isWalking = true;
        _lastPosition = transform.position;
        _animator.SetFloat(WalkSpeedParam, 0f);
        _animator.SetBool(IsWalkingParam, true);
    }

    /// <summary>Cross-fades back to the idle, hands still on the handle.</summary>
    public void StopWalking()
    {
        if (!_isWalking) return;
        _isWalking = false;
        _animator.SetBool(IsWalkingParam, false);
    }

    // LateUpdate, so the cart has been moved for this frame whichever order the motions ran in.
    private void LateUpdate()
    {
        if (!_isWalking) return;

        var t = transform;
        var position = t.position;
        var delta = position - _lastPosition;
        _lastPosition = position;

        var deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;

        // Signed: negative while the cart drags the shopper backward, which plays the walk in reverse.
        // Measured on the shopper rather than the cart, so the swing they get from a turning cart counts.
        var groundSpeed = Vector3.Dot(delta, t.forward) / deltaTime;
        var clipSpeed = _strideSpeed * Mathf.Max(Mathf.Abs(t.lossyScale.z), 0.0001f);
        var playbackSpeed = Mathf.Clamp(groundSpeed / clipSpeed, -_maxPlaybackSpeed, _maxPlaybackSpeed);

        _animator.SetFloat(WalkSpeedParam, playbackSpeed);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        PinHand(AvatarIKGoal.LeftHand, _leftGrip);
        PinHand(AvatarIKGoal.RightHand, _rightGrip);
    }

    private void PinHand(AvatarIKGoal goal, Transform grip)
    {
        if (grip == null) return;

        _animator.SetIKPositionWeight(goal, _gripWeight);
        _animator.SetIKRotationWeight(goal, _gripWeight);
        _animator.SetIKPosition(goal, grip.position);
        _animator.SetIKRotation(goal, grip.rotation);
    }
}

using UnityEngine;

/// <summary>
/// Manages two hand visual GameObjects for the driver.
///
/// FREE MODE:  Each hand visual mirrors its OVR controller anchor every
///             LateUpdate, so it always matches where the physical controller is.
///
/// GRAB MODE:  When the player grabs the steering wheel, the grabbing hand is
///             re-parented to a point ON the wheel rim.  Because that point is a
///             child of the wheel, the hand rotates with the wheel automatically —
///             no per-frame positioning needed.  On release it is re-parented back
///             to its controller anchor and resumes free-follow mode.
///
/// FINGER POSE: If the hand mesh has an Animator with a float parameter named
///              "Grip" (0 = open, 1 = closed), the script drives it smoothly on
///              grab and release.  Works with the standard Meta SDK hand animators.
///
/// ───────────────────────────────────────────────────────────────────────────
/// SCENE SETUP
/// ───────────────────────────────────────────────────────────────────────────
/// 1. Create two GameObjects — e.g. "LeftDrivingHand" and "RightDrivingHand".
///    Add your hand mesh (SkinnedMeshRenderer) as a child of each, or directly
///    on each if the mesh is on the root.
///
///    TIP — the easiest way to get a proper hand mesh:
///    In the OVR rig, find  OVRCameraRig > TrackingSpace > LeftHandAnchor >
///    LeftHandOnControllerAnchor.  That GameObject already has the SDK hand mesh
///    on it.  Duplicate it, move it OUT of the OVR rig into the scene root, and
///    rename it "LeftDrivingHand".  Repeat for right.
///    The originals in the rig can be hidden (disable their MeshRenderer) so only
///    your driving hands are visible.
///
/// 2. Add DriverHandsController to the car root (or any persistent GameObject).
///    Assign:
///      Left Controller Anchor  → OVRCameraRig/TrackingSpace/LeftHandAnchor
///      Right Controller Anchor → OVRCameraRig/TrackingSpace/RightHandAnchor
///      Left Hand Visual        → "LeftDrivingHand"
///      Right Hand Visual       → "RightDrivingHand"
///
/// 3. (Optional) If your hand mesh has an Animator, make sure it has a float
///    parameter called "Grip".  The Meta SDK "default" hand animator already has
///    this parameter.
///
/// 4. Assign this DriverHandsController to SteeringWheelInteraction_OVR.
/// ───────────────────────────────────────────────────────────────────────────
/// </summary>
public class DriverHandsController : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────── //

    [Header("Controller Anchors  (OVR rig / TrackingSpace children)")]
    [Tooltip("OVRCameraRig > TrackingSpace > LeftHandAnchor")]
    public Transform leftControllerAnchor;

    [Tooltip("OVRCameraRig > TrackingSpace > RightHandAnchor")]
    public Transform rightControllerAnchor;

    [Header("Hand Visuals")]
    [Tooltip("The GameObject that holds the LEFT hand mesh. " +
             "Must NOT be inside the OVR rig hierarchy.")]
    public GameObject leftHandVisual;

    [Tooltip("The GameObject that holds the RIGHT hand mesh.")]
    public GameObject rightHandVisual;

    [Header("Grab Pose")]
    [Tooltip("Name of the Animator float that controls finger curl. " +
             "0 = fully open, 1 = fully closed.  Leave blank to skip animation.")]
    public string gripParameterName = "Grip";

    [Tooltip("How fast fingers curl / uncurl (units per second).")]
    [Range(1f, 20f)]
    public float gripBlendSpeed = 8f;

    [Tooltip("Rotation applied to the hand visual when it snaps onto the wheel rim. " +
             "Tweak X/Y/Z in the Inspector until the hand looks natural gripping the wheel.")]
    public Vector3 gripRotationOffset = new Vector3(-90f, 0f, 0f);

    // ── Runtime state ─────────────────────────────────────────────────────  //

    // Cached animators — null if mesh has none.
    private Animator leftAnim;
    private Animator rightAnim;

    private bool leftGrabbing  = false;
    private bool rightGrabbing = false;

    // Grip blend targets (0 = open, 1 = closed)
    private float leftGripTarget  = 0f;
    private float rightGripTarget = 0f;

    // Current smoothed grip values
    private float leftGripCurrent  = 0f;
    private float rightGripCurrent = 0f;

    // The original parent of each hand visual before a grab.
    // Stored so we can restore it precisely on release.
    private Transform leftOriginalParent;
    private Transform rightOriginalParent;

    // ── Unity callbacks ────────────────────────────────────────────────────  //

    void Awake()
    {
        if (leftHandVisual  != null) leftAnim  = leftHandVisual.GetComponentInChildren<Animator>();
        if (rightHandVisual != null) rightAnim = rightHandVisual.GetComponentInChildren<Animator>();

        if (leftHandVisual  != null) leftOriginalParent  = leftHandVisual.transform.parent;
        if (rightHandVisual != null) rightOriginalParent = rightHandVisual.transform.parent;
    }

    void Start()
    {
        // Auto-find controller anchors from OVR rig if not assigned
        if (leftControllerAnchor == null || rightControllerAnchor == null)
        {
            OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();
            if (rig != null)
            {
                if (leftControllerAnchor  == null) leftControllerAnchor  = rig.leftHandAnchor;
                if (rightControllerAnchor == null) rightControllerAnchor = rig.rightHandAnchor;
            }
        }
    }

    /// <summary>
    /// LateUpdate runs after OVR has updated controller positions.
    /// We copy those positions to our hand visuals when not grabbing.
    /// </summary>
    void LateUpdate()
    {
        // Smooth grip blend
        int gripID = string.IsNullOrEmpty(gripParameterName)
            ? -1
            : Animator.StringToHash(gripParameterName);

        UpdateHand(
            leftHandVisual,  leftControllerAnchor,  leftAnim,
            leftGrabbing,    ref leftGripCurrent,    leftGripTarget,  gripID);

        UpdateHand(
            rightHandVisual, rightControllerAnchor, rightAnim,
            rightGrabbing,   ref rightGripCurrent,   rightGripTarget, gripID);
    }

    private void UpdateHand(
        GameObject visual,  Transform anchor,     Animator anim,
        bool       grabbing, ref float gripCurrent, float gripTarget,
        int        gripID)
    {
        if (visual == null) return;

        // ── Position ───────────────────────────────────────────────────── //
        if (grabbing)
        {
            // The hand is parented to the wheel grab anchor — Unity's transform
            // hierarchy already handles position.  We forcibly re-apply it here
            // to win the per-frame fight against the OVR SDK, which also tries
            // to write this transform in its own LateUpdate.
            // (No-op when the visual is NOT inside the OVR rig.)
            Transform parent = visual.transform.parent;
            if (parent != null)
            {
                visual.transform.position = parent.position;
                visual.transform.rotation = parent.rotation * Quaternion.Euler(gripRotationOffset);
            }
        }
        else if (anchor != null)
        {
            // Free mode — mirror the controller anchor.
            // Running in LateUpdate ensures we overwrite OVR's position write.
            visual.transform.position = anchor.position;
            visual.transform.rotation = anchor.rotation;
        }
        // If grabbing, the visual is parented to the wheel grab point, so
        // position is also reinforced above.

        // ── Finger curl ────────────────────────────────────────────────── //
        if (anim == null || gripID < 0) return;

        gripCurrent = Mathf.MoveTowards(gripCurrent, gripTarget, gripBlendSpeed * Time.deltaTime);
        anim.SetFloat(gripID, gripCurrent);
    }

    // ── Public API (called by SteeringWheelInteraction_OVR) ───────────────  //

    /// <summary>
    /// Snap a hand onto a point on the steering wheel rim.
    /// The hand is re-parented to <paramref name="rimPoint"/>, which is itself
    /// a child of the wheel — so the hand will rotate with the wheel.
    /// </summary>
    /// <param name="isLeft">True = left hand, false = right hand.</param>
    /// <param name="rimPoint">The Transform on the wheel rim where the hand should sit.</param>
    public void GrabWheel(bool isLeft, Transform rimPoint)
    {
        GameObject visual = isLeft ? leftHandVisual : rightHandVisual;
        if (visual == null) return;

        // Snap world position to the rim contact point
        visual.transform.position = rimPoint.position;

        // Apply grip rotation offset so fingers wrap around the wheel naturally
        visual.transform.rotation = rimPoint.rotation
                                   * Quaternion.Euler(gripRotationOffset);

        // Parent to wheel — hand now rotates with the wheel for free
        visual.transform.SetParent(rimPoint, worldPositionStays: true);

        // Close fingers
        if (isLeft) { leftGrabbing  = true; leftGripTarget  = 1f; }
        else        { rightGrabbing = true; rightGripTarget = 1f; }
    }

    /// <summary>
    /// Release a hand from the wheel and return it to free-follow mode.
    /// </summary>
    public void ReleaseWheel(bool isLeft)
    {
        GameObject visual         = isLeft ? leftHandVisual  : rightHandVisual;
        Transform  originalParent = isLeft ? leftOriginalParent : rightOriginalParent;

        if (visual == null) return;

        // Restore original parent (or scene root if it had none)
        visual.transform.SetParent(originalParent, worldPositionStays: true);

        // Open fingers
        if (isLeft) { leftGrabbing  = false; leftGripTarget  = 0f; }
        else        { rightGrabbing = false; rightGripTarget = 0f; }
    }

    /// <summary>
    /// Show or hide a hand visual.  Useful for hiding during UI interactions.
    /// </summary>
    public void SetHandVisible(bool isLeft, bool visible)
    {
        GameObject visual = isLeft ? leftHandVisual : rightHandVisual;
        if (visual != null) visual.SetActive(visible);
    }
}

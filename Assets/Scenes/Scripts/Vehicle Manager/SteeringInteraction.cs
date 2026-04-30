using UnityEngine;

/// <summary>
/// Steering wheel grab — supports ONE or TWO hands simultaneously.
///
/// • One hand:  that hand drives the wheel angle directly.
/// • Two hands: the angle is averaged from both grabs, giving a natural
///              two-handed steering feel.
/// • Release one hand while the other is still holding → seamlessly
///              continues with the remaining hand.
///
/// Calls DriverHandsController to snap hand meshes to the rim on grab
/// and restore them on release.
/// </summary>
public class SteeringWheelInteraction_OVR : MonoBehaviour
{
    [Header("References")]
    public VehicleManager vehicleManager;

    [Tooltip("Drag the Cars GameObject here — it has the DriverHandsController on it.")]
    public DriverHandsController driverHands;

    [Header("Steering Settings")]
    [Tooltip("How close (metres) a hand must be to the wheel centre to grab.")]
    public float grabDistance = 0.25f;

    [Tooltip("Max degrees the wheel can rotate each way.")]
    public float maxSteeringAngle = 90f;

    [Tooltip("How fast the wheel self-centres when both hands are released.")]
    public float centreReturnSpeed = 360f;

    [Header("Interaction Feedback")]
    public Material highlightMaterial;

    // ── Private: per-hand grab state ──────────────────────────────────────  //

    private Transform leftHandAnchor;
    private Transform rightHandAnchor;

    // Each hand tracks its own grab independently
    private bool    leftGrabbing       = false;
    private bool    rightGrabbing      = false;
    private Vector3 leftGrabOffset;      // hand pos in wheel-local XZ at grab start
    private Vector3 rightGrabOffset;
    private float   leftAngleAtStart;
    private float   rightAngleAtStart;

    // One grab anchor per hand, parented to the wheel
    private Transform leftGrabAnchor;
    private Transform rightGrabAnchor;

    private float     currentWheelAngle = 0f;
    private Quaternion initialWheelRotation;

    // Highlight
    private Renderer  wheelRenderer;
    private Material  originalMaterial;
    private bool      isHighlighted = false;

    // ── Init ──────────────────────────────────────────────────────────────  //

    void Start()
    {
        OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();
        if (rig != null)
        {
            leftHandAnchor  = rig.leftHandAnchor;
            rightHandAnchor = rig.rightHandAnchor;
        }

        initialWheelRotation = transform.localRotation;

        wheelRenderer = GetComponent<Renderer>();
        if (wheelRenderer != null)
            originalMaterial = wheelRenderer.material;

        // Two reusable anchors, both children of the wheel
        leftGrabAnchor  = CreateAnchor("_GrabAnchor_L");
        rightGrabAnchor = CreateAnchor("_GrabAnchor_R");

        if (driverHands == null)
            driverHands = Object.FindFirstObjectByType<DriverHandsController>();
    }

    private Transform CreateAnchor(string goName)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        return go.transform;
    }

    // ── Update ────────────────────────────────────────────────────────────  //

    void Update()
    {
        UpdateHighlight();
        HandleLeftHand();
        HandleRightHand();
        ComputeAndApplySteering();
        SendSteeringValue();
    }

    // ── Per-hand input ────────────────────────────────────────────────────  //

    private void HandleLeftHand()
    {
        bool triggerHeld = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch);
        bool nearWheel   = leftHandAnchor != null &&
                           Vector3.Distance(transform.position, leftHandAnchor.position) < grabDistance;

        if (!leftGrabbing)
        {
            if (nearWheel && OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch))
                BeginGrab(isLeft: true);
        }
        else
        {
            if (!triggerHeld)
                EndGrab(isLeft: true);
            else
                UpdateGrabAnchor(isLeft: true); // keep anchor glued to real hand
        }
    }

    private void HandleRightHand()
    {
        bool triggerHeld = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);
        bool nearWheel   = rightHandAnchor != null &&
                           Vector3.Distance(transform.position, rightHandAnchor.position) < grabDistance;

        if (!rightGrabbing)
        {
            if (nearWheel && OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch))
                BeginGrab(isLeft: false);
        }
        else
        {
            if (!triggerHeld)
                EndGrab(isLeft: false);
            else
                UpdateGrabAnchor(isLeft: false);
        }
    }

    // ── Grab / Release ────────────────────────────────────────────────────  //

    private void BeginGrab(bool isLeft)
    {
        Transform hand   = isLeft ? leftHandAnchor  : rightHandAnchor;
        Transform anchor = isLeft ? leftGrabAnchor  : rightGrabAnchor;

        // Record contact point in wheel-local XZ (flat — Y stripped for angle math)
        Vector3 localPos = transform.InverseTransformPoint(hand.position);
        Vector3 flatOffset = new Vector3(localPos.x, 0f, localPos.z);

        anchor.localPosition = localPos;
        anchor.localRotation = Quaternion.identity;

        if (isLeft)
        {
            leftGrabbing      = true;
            leftGrabOffset    = flatOffset;
            leftAngleAtStart  = currentWheelAngle;
        }
        else
        {
            rightGrabbing     = true;
            rightGrabOffset   = flatOffset;
            rightAngleAtStart = currentWheelAngle;
        }

        // Snap hand mesh to rim
        if (driverHands != null)
            driverHands.GrabWheel(isLeft, anchor);
    }

    private void EndGrab(bool isLeft)
    {
        if (isLeft) leftGrabbing  = false;
        else        rightGrabbing = false;

        if (driverHands != null)
            driverHands.ReleaseWheel(isLeft);

        // Restore material only when BOTH hands have released
        if (!leftGrabbing && !rightGrabbing && wheelRenderer != null && originalMaterial != null)
        {
            wheelRenderer.material = originalMaterial;
            isHighlighted = false;
        }
    }

    // Keep the grab anchor world-position glued to the physical hand each frame.
    // This makes the snapped hand mesh track the controller naturally as the
    // wheel rotates under it.
    private void UpdateGrabAnchor(bool isLeft)
    {
        Transform hand   = isLeft ? leftHandAnchor  : rightHandAnchor;
        Transform anchor = isLeft ? leftGrabAnchor  : rightGrabAnchor;
        if (hand != null) anchor.position = hand.position;
    }

    // ── Steering math ─────────────────────────────────────────────────────  //

    private void ComputeAndApplySteering()
    {
        bool anyGrab = leftGrabbing || rightGrabbing;

        if (!anyGrab)
        {
            // Self-centre
            if (Mathf.Abs(currentWheelAngle) > 0.5f)
                currentWheelAngle = Mathf.MoveTowards(currentWheelAngle, 0f, centreReturnSpeed * Time.deltaTime);
            else
                currentWheelAngle = 0f;
        }
        else
        {
            float totalAngle  = 0f;
            int   grabCount   = 0;

            if (leftGrabbing)
            {
                totalAngle += ComputeHandAngle(leftHandAnchor, leftGrabOffset, leftAngleAtStart);
                grabCount++;
            }
            if (rightGrabbing)
            {
                totalAngle += ComputeHandAngle(rightHandAnchor, rightGrabOffset, rightAngleAtStart);
                grabCount++;
            }

            // Average when both hands are grabbing
            currentWheelAngle = Mathf.Clamp(totalAngle / grabCount, -maxSteeringAngle, maxSteeringAngle);
        }

        transform.localRotation = initialWheelRotation * Quaternion.Euler(0f, currentWheelAngle, 0f);
    }

    /// <summary>
    /// Returns the steering angle contribution from a single hand.
    /// Uses the same absolute-angle method as before — no drift.
    /// </summary>
    private float ComputeHandAngle(Transform hand, Vector3 grabOffset, float angleAtStart)
    {
        if (hand == null) return angleAtStart;

        Vector3 localPos      = transform.InverseTransformPoint(hand.position);
        Vector3 currentOffset = new Vector3(localPos.x, 0f, localPos.z);

        if (currentOffset.sqrMagnitude < 0.0001f) return angleAtStart;

        float delta = Vector3.SignedAngle(grabOffset, currentOffset, Vector3.up);
        return angleAtStart + delta;
    }

    private void SendSteeringValue()
    {
        if (vehicleManager == null) return;
        vehicleManager.SetSteering(currentWheelAngle / maxSteeringAngle);
    }

    // ── Highlight ─────────────────────────────────────────────────────────  //

    private void UpdateHighlight()
    {
        if (wheelRenderer == null || highlightMaterial == null) return;

        bool handNear =
            (leftHandAnchor  != null && Vector3.Distance(transform.position, leftHandAnchor.position)  < grabDistance) ||
            (rightHandAnchor != null && Vector3.Distance(transform.position, rightHandAnchor.position) < grabDistance);

        bool anyGrabbing = leftGrabbing || rightGrabbing;

        if (handNear && !isHighlighted)
        {
            wheelRenderer.material = highlightMaterial;
            isHighlighted = true;
        }
        else if (!handNear && !anyGrabbing && isHighlighted)
        {
            wheelRenderer.material = originalMaterial;
            isHighlighted = false;
        }
    }

    // ── Gizmos ────────────────────────────────────────────────────────────  //

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, grabDistance);
    }
}

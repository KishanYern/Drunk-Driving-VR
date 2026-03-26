using UnityEngine;

public class SteeringWheelInteraction_OVR : MonoBehaviour
{
    [Header("References")]
    public VehicleManager vehicleManager;

    [Header("Steering Settings")]
    public float grabDistance = 0.2f;
    [Tooltip("Total degrees the physical wheel can rotate each way (e.g. 450 = 1.25 turns)")]
    public float maxSteeringAngle = 90f;
    [Tooltip("How quickly the wheel self-centres after release (degrees per second)")]
    public float centreReturnSpeed = 360f;

    [Header("Hand Lock Settings")]
    [Tooltip("Assign the left hand visual GameObject (the rendered hand mesh, not the anchor)")]
    public GameObject leftHandVisual;
    [Tooltip("Assign the right hand visual GameObject (the rendered hand mesh, not the anchor)")]
    public GameObject rightHandVisual;
    [Tooltip("A small sphere or hand-shaped mesh to show the locked grab point on the rim")]
    public GameObject grabMarkerPrefab;

    [Header("Interaction Feedback")]
    [Tooltip("Material to swap to when a hand is close enough to grab")]
    public Material highlightMaterial;

    // ------------------------------------------------------------------ //
    // Private state
    // ------------------------------------------------------------------ //
    private Transform leftHand;
    private Transform rightHand;

    private bool isGrabbing = false;
    private Transform activeHand = null;

    private float currentWheelAngle = 0f;
    private Vector3 grabLocalOffset;          // contact point in wheel-local XZ at grab start
    private float angleAtGrabStart;         // wheel angle at grab start

    private Quaternion initialWheelRotation;

    // The grab anchor lives as a child of the wheel and rotates with it.
    // It is placed at the contact point in local space when the grab starts.
    private Transform grabAnchor;
    private GameObject grabMarkerInstance;

    // Feedback
    private Renderer wheelRenderer;
    private Material originalMaterial;
    private bool isHighlighted = false;

    // ------------------------------------------------------------------ //

    void Start()
    {
        OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null)
        {
            leftHand = rig.leftHandAnchor;
            rightHand = rig.rightHandAnchor;
        }

        initialWheelRotation = transform.localRotation;

        wheelRenderer = GetComponent<Renderer>();
        if (wheelRenderer != null)
            originalMaterial = wheelRenderer.material;

        // Create the grab anchor once and reuse it — cheaper than Instantiate each grab
        GameObject anchorGO = new GameObject("_GrabAnchor");
        anchorGO.transform.SetParent(transform);
        anchorGO.transform.localPosition = Vector3.zero;
        anchorGO.transform.localRotation = Quaternion.identity;
        grabAnchor = anchorGO.transform;
    }

    void Update()
    {
        UpdateHighlight();
        HandleGrabInput();

        if (isGrabbing && activeHand != null)
            UpdateSteering();
        else
            CentreWheel();

        ApplyWheelRotation();
        SendSteeringValue();
    }

    // ------------------------------------------------------------------ //
    // Proximity highlight
    // ------------------------------------------------------------------ //

    private void UpdateHighlight()
    {
        if (wheelRenderer == null || highlightMaterial == null) return;

        bool handNear = (leftHand != null && Vector3.Distance(transform.position, leftHand.position) < grabDistance)
                     || (rightHand != null && Vector3.Distance(transform.position, rightHand.position) < grabDistance);

        if (handNear && !isHighlighted)
        {
            wheelRenderer.material = highlightMaterial;
            isHighlighted = true;
        }
        else if (!handNear && !isGrabbing && isHighlighted)
        {
            wheelRenderer.material = originalMaterial;
            isHighlighted = false;
        }
    }

    // ------------------------------------------------------------------ //
    // Grab input
    // ------------------------------------------------------------------ //

    private void HandleGrabInput()
    {
        if (!isGrabbing)
        {
            if (leftHand != null
                && Vector3.Distance(transform.position, leftHand.position) < grabDistance
                && OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch))
            {
                BeginGrab(leftHand, leftHandVisual);
            }
            else if (rightHand != null
                && Vector3.Distance(transform.position, rightHand.position) < grabDistance
                && OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch))
            {
                BeginGrab(rightHand, rightHandVisual);
            }
        }
        else
        {
            OVRInput.Controller ctrl = (activeHand == leftHand)
                ? OVRInput.Controller.LTouch
                : OVRInput.Controller.RTouch;

            if (!OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, ctrl))
                EndGrab();
        }
    }

    private void BeginGrab(Transform hand, GameObject handVisual)
    {
        isGrabbing = true;
        activeHand = hand;
        angleAtGrabStart = currentWheelAngle;

        // --- Compute the contact point ---
        // Project the hand into wheel-local space, flatten onto XZ plane (Y = wheel spin axis)
        Vector3 localPos = transform.InverseTransformPoint(hand.position);
        grabLocalOffset = new Vector3(localPos.x, 0f, localPos.z);

        // --- Place the grab anchor at the contact point ---
        // Because grabAnchor is a child of the wheel, it will rotate WITH the wheel
        // every frame, making it a true locked-on-rim reference point.
        grabAnchor.localPosition = localPos;
        grabAnchor.localRotation = Quaternion.identity;

        // --- Spawn a visible marker at the contact point on the rim ---
        if (grabMarkerPrefab != null)
        {
            if (grabMarkerInstance != null) Destroy(grabMarkerInstance);
            grabMarkerInstance = Instantiate(grabMarkerPrefab, grabAnchor);
            grabMarkerInstance.transform.localPosition = Vector3.zero;
            grabMarkerInstance.transform.localScale = Vector3.one * 0.025f;
        }

        // --- Hide the floating hand visual so it doesn't fight the locked position ---
        // The hand will visually "lock" because the marker rides the wheel rim.
        // If you have a hand mesh (not just controller model), hide it here
        // so players see the marker on the rim instead of a detached floating hand.
        if (handVisual != null)
            handVisual.SetActive(false);
    }

    private void EndGrab()
    {
        // Restore hand visual
        GameObject activeHandVisual = (activeHand == leftHand) ? leftHandVisual : rightHandVisual;
        if (activeHandVisual != null)
            activeHandVisual.SetActive(true);

        isGrabbing = false;
        activeHand = null;

        // Remove marker
        if (grabMarkerInstance != null)
        {
            Destroy(grabMarkerInstance);
            grabMarkerInstance = null;
        }

        // Restore material
        if (wheelRenderer != null && originalMaterial != null)
        {
            wheelRenderer.material = originalMaterial;
            isHighlighted = false;
        }
    }

    // ------------------------------------------------------------------ //
    // Steering — locked contact method using grab anchor
    // ------------------------------------------------------------------ //

    private void UpdateSteering()
    {
        // Project the live hand position into the wheel's current local XZ plane
        Vector3 localPos = transform.InverseTransformPoint(activeHand.position);
        Vector3 currentOffset = new Vector3(localPos.x, 0f, localPos.z);

        if (currentOffset.sqrMagnitude < 0.0001f) return;

        // SignedAngle from the ORIGINAL grab offset (not last frame) to the current hand.
        // This is absolute — no per-frame accumulation, no drift.
        // The result is how far the hand has rotated around the rim since grab start.
        float angleDelta = Vector3.SignedAngle(grabLocalOffset, currentOffset, Vector3.up);

        currentWheelAngle = Mathf.Clamp(
            angleAtGrabStart + angleDelta,
            -maxSteeringAngle,
            maxSteeringAngle
        );

        // Move the grab anchor in world space to sit exactly under the real hand.
        // This keeps the marker glued to where your physical hand is touching.
        // Since grabAnchor is a child of the wheel, this also implicitly shows
        // the difference between "where your hand is" and "where the rim is."
        grabAnchor.position = activeHand.position;
    }

    // ------------------------------------------------------------------ //

    private void CentreWheel()
    {
        if (Mathf.Abs(currentWheelAngle) < 0.5f) { currentWheelAngle = 0f; return; }
        currentWheelAngle = Mathf.MoveTowards(currentWheelAngle, 0f, centreReturnSpeed * Time.deltaTime);
    }

    private void ApplyWheelRotation()
    {
        // Wheel rotates around local Y — confirmed from your measured transforms.
        // Negate currentWheelAngle if left/right is inverted in-game.
        transform.localRotation = initialWheelRotation * Quaternion.Euler(0f, currentWheelAngle, 0f);
    }

    private void SendSteeringValue()
    {
        if (vehicleManager == null) return;
        vehicleManager.SetSteering(currentWheelAngle / maxSteeringAngle);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, grabDistance);
    }
}
using UnityEngine;

/// <summary>
/// Fixed version of BottleGrabbable.
/// 
/// KEY FIXES vs original:
///   1. Bottle starts kinematic and parented to the car seat so it never flies around.
///   2. On grab: unparents from seat, stays kinematic (follows hand).
///   3. On drop: re-parents back to seat anchor so it settles back in the car.
///   4. Throw: if released with fast hand velocity, unparents fully and goes physical.
/// 
/// SETUP:
///   - Attach this to your bottle GameObject
///   - Assign the "Seat Anchor" or any stable Transform inside the car as bottleAnchor
///   - Assign DrunkEffect as before
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BottleGrabbable : MonoBehaviour
{
    [Header("Anchor — keeps bottle in the car")]
    [Tooltip("Drag in the seat anchor or a child Transform of the car where the bottle should sit. " +
             "The bottle will be parented here at start and returned here on drop.")]
    public Transform bottleAnchor;

    [Tooltip("Local position offset from the anchor (tweak so bottle sits naturally in cup holder / seat).")]
    public Vector3 anchorPositionOffset = new Vector3(0.2f, 0.05f, 0.1f);

    [Tooltip("Local rotation of the bottle when resting in the car.")]
    public Vector3 anchorRotationOffset = new Vector3(0f, 0f, 0f);

    [Header("Interaction Settings")]
    [Tooltip("How close the right hand must be to grab the bottle.")]
    public float grabRange = 0.15f;

    [Tooltip("Minimum hand speed (m/s) to count as a throw rather than a gentle drop back.")]
    public float throwThreshold = 1.5f;

    [Header("Drinking Settings")]
    [Tooltip("How close the bottle must be to the head to count as drinking.")]
    public float drinkRange = 0.25f;
    [Tooltip("Seconds of continuous holding near face before registering a sip.")]
    public float drinkDuration = 1.5f;
    [Tooltip("Drag the DrunkEffect script (on OVRCameraRig) here.")]
    public DrunkEffect drunkEffect;

    [Header("Visual Feedback")]
    public bool showHighlight = true;
    public Material highlightMaterial;

    // ------------------------------------------------------------------ //
    // Private state
    // ------------------------------------------------------------------ //
    private Transform rightHand;
    private Transform headAnchor;
    private Rigidbody rb;
    private Renderer bottleRenderer;
    private Material originalMaterial;

    private bool isHeld = false;
    private bool isInRange = false;
    private bool isDrinking = false;
    private float drinkTimer = 0f;

    private Vector3 prevHandPos;
    private Vector3 handVelocity;

    // ------------------------------------------------------------------ //

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        bottleRenderer = GetComponentInChildren<Renderer>();
        if (bottleRenderer != null)
            originalMaterial = bottleRenderer.material;

        // Find OVR anchors
        OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null)
        {
            rightHand = rig.rightHandAnchor;
            headAnchor = rig.centerEyeAnchor;
        }

        if (rightHand == null) Debug.LogWarning("[Bottle] Could not find RightHandAnchor.");
        if (headAnchor == null) Debug.LogWarning("[Bottle] Could not find CenterEyeAnchor.");

        // --- FIX: Anchor bottle to the car so it never flies ---
        AnchorToSeat();
    }

    void Update()
    {
        if (rightHand == null) return;

        TrackHandVelocity();

        if (isHeld)
        {
            FollowHand();
            HandleDrinking();
            HandleDrop();
        }
        else
        {
            CheckGrabRange();
            if (isInRange) HandleGrab();
        }
    }

    // ------------------------------------------------------------------ //
    // Seat anchoring
    // ------------------------------------------------------------------ //

    private void AnchorToSeat()
    {
        if (bottleAnchor == null)
        {
            // No anchor assigned — just freeze in place so it doesn't fly
            rb.isKinematic = true;
            Debug.LogWarning("[Bottle] No bottleAnchor assigned! Bottle frozen in world position. " +
                             "Drag a car child Transform into the Bottle Anchor field.");
            return;
        }

        rb.isKinematic = true;
        transform.SetParent(bottleAnchor, worldPositionStays: false);
        transform.localPosition = anchorPositionOffset;
        transform.localRotation = Quaternion.Euler(anchorRotationOffset);
    }

    private void ReturnToSeat()
    {
        if (bottleAnchor == null)
        {
            // No anchor — just freeze wherever it is
            rb.isKinematic = true;
            return;
        }

        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.SetParent(bottleAnchor, worldPositionStays: false);
        transform.localPosition = anchorPositionOffset;
        transform.localRotation = Quaternion.Euler(anchorRotationOffset);
    }

    // ------------------------------------------------------------------ //
    // Hand tracking
    // ------------------------------------------------------------------ //

    void TrackHandVelocity()
    {
        handVelocity = (rightHand.position - prevHandPos) / Time.deltaTime;
        prevHandPos = rightHand.position;
    }

    // While held, bottle follows the hand kinematically (no physics jitter)
    void FollowHand()
    {
        transform.position = rightHand.position;
        transform.rotation = rightHand.rotation;
    }

    // ------------------------------------------------------------------ //
    // Grab / Drop
    // ------------------------------------------------------------------ //

    void CheckGrabRange()
    {
        bool wasInRange = isInRange;
        isInRange = Vector3.Distance(transform.position, rightHand.position) <= grabRange;

        if (showHighlight && bottleRenderer != null && highlightMaterial != null)
        {
            if (isInRange && !wasInRange)
                bottleRenderer.material = highlightMaterial;
            else if (!isInRange && wasInRange)
                bottleRenderer.material = originalMaterial;
        }
    }

    void HandleGrab()
    {
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            PickUp();
    }

    void PickUp()
    {
        isHeld = true;
        isInRange = false;

        if (bottleRenderer != null && originalMaterial != null)
            bottleRenderer.material = originalMaterial;

        // Unparent from seat — bottle is now in hand
        transform.SetParent(null, worldPositionStays: true);
        rb.isKinematic = true;   // still kinematic, FollowHand() moves it

        prevHandPos = rightHand.position;
        Debug.Log("[Bottle] Grabbed.");
    }

    void HandleDrop()
    {
        if (OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            float speed = handVelocity.magnitude;

            if (speed >= throwThreshold)
            {
                // Fast release = throw. Let physics take over.
                rb.isKinematic = false;
                rb.linearVelocity = handVelocity;
                isHeld = false;
                isDrinking = false;
                drinkTimer = 0f;
                Debug.Log($"[Bottle] Thrown at {speed:F1} m/s.");
                // NOTE: bottle won't auto-return after a throw.
                // Add a trigger zone or timer here if you want it to respawn.
            }
            else
            {
                // Gentle drop = snap back to seat
                isHeld = false;
                isDrinking = false;
                drinkTimer = 0f;
                ReturnToSeat();
                Debug.Log("[Bottle] Dropped — returning to seat.");
            }
        }
    }

    // ------------------------------------------------------------------ //
    // Drinking
    // ------------------------------------------------------------------ //

    void HandleDrinking()
    {
        if (headAnchor == null) return;

        bool nearFace = Vector3.Distance(transform.position, headAnchor.position) <= drinkRange;

        if (nearFace)
        {
            if (!isDrinking)
            {
                isDrinking = true;
                drinkTimer = 0f;
                Debug.Log("[Bottle] Raising to drink...");
            }

            drinkTimer += Time.deltaTime;

            if (drinkTimer >= drinkDuration)
            {
                drinkTimer = 0f;
                TakeSip();
            }
        }
        else
        {
            if (isDrinking)
            {
                isDrinking = false;
                drinkTimer = 0f;
            }
        }
    }

    void TakeSip()
    {
        Debug.Log("[Bottle] Sip taken — impairment increasing.");
        if (drunkEffect != null)
            drunkEffect.AddSip();
        else
            Debug.LogWarning("[Bottle] DrunkEffect not assigned!");
    }

    // ------------------------------------------------------------------ //

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, grabRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, drinkRange);
    }
}
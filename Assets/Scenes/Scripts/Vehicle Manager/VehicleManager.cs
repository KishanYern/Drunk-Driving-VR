using UnityEngine;

/// <summary>
/// VehicleManager — seats the player in the car at game start.
///
/// ACTUAL SCENE STRUCTURE (do not change):
///   Cars  (root)
///   ├── OculusInteractionSampleRig   ← playerRig
///   └── Sedan1                       ← this script lives here
///       ├── Driver Anchor            ← seatAnchor
///       └── CarController2_VR        ← carController
///
/// Because OculusInteractionSampleRig is already a child of Cars, it already
/// moves with the car — no re-parenting needed. We only need to position it
/// correctly at the Driver Anchor once on Start.
///
/// Everything is auto-found at runtime so you do NOT need to manually assign
/// fields in the Inspector (though you still can to override auto-detection).
/// </summary>
public class VehicleManager : MonoBehaviour
{
    [Header("References  (all auto-found if left empty)")]
    [Tooltip("The OculusInteractionSampleRig inside Cars. Auto-found if blank.")]
    public GameObject playerRig;

    [Tooltip("The Driver Anchor Transform inside Sedan1. Auto-found if blank.")]
    public Transform seatAnchor;

    [Tooltip("CarController2_VR on Sedan1. Auto-found if blank.")]
    public CarController2_VR carController;

    // ── Runtime state ─────────────────────────────────────────────────────  //

    private bool inCar = false;

    /// <summary>Exposed so SteeringWheelInteraction_OVR can call SetSteering.</summary>
    public CarController2_VR CarController => carController;

    // ── Init ──────────────────────────────────────────────────────────────  //

    void Start()
    {
        AutoFindReferences();
        SeatPlayer();
    }

    private void AutoFindReferences()
    {
        // ── Player rig ────────────────────────────────────────────────── //
        // VehicleManager is on Sedan1. Cars is Sedan1's parent.
        // OculusInteractionSampleRig is a sibling of Sedan1 inside Cars.
        if (playerRig == null)
        {
            Transform carsRoot = transform.parent; // Cars
            if (carsRoot != null)
            {
                // Walk Cars' children to find the one with OVRCameraRig
                foreach (Transform child in carsRoot)
                {
                    if (child.GetComponentInChildren<OVRCameraRig>() != null)
                    {
                        playerRig = child.gameObject;
                        break;
                    }
                }
            }

            // Last resort — scene-wide search
            if (playerRig == null)
            {
                OVRCameraRig found = Object.FindFirstObjectByType<OVRCameraRig>();
                if (found != null) playerRig = found.transform.parent?.gameObject ?? found.gameObject;
            }

            if (playerRig != null)
                Debug.Log($"VehicleManager: Auto-found player rig '{playerRig.name}'.");
            else
                Debug.LogError("VehicleManager: Could not find OculusInteractionSampleRig!");
        }

        // ── Seat anchor ───────────────────────────────────────────────── //
        // Driver Anchor is a direct child of Sedan1 (this GameObject).
        if (seatAnchor == null)
        {
            seatAnchor = transform.Find("Driver Anchor");

            if (seatAnchor == null)
            {
                // Try a recursive search one level deeper
                foreach (Transform child in transform)
                    if (child.name.ToLower().Contains("driver") || child.name.ToLower().Contains("seat"))
                    { seatAnchor = child; break; }
            }

            if (seatAnchor != null)
                Debug.Log($"VehicleManager: Auto-found seat anchor '{seatAnchor.name}'.");
            else
                Debug.LogWarning("VehicleManager: 'Driver Anchor' not found — using Sedan1 position as fallback.");
        }

        // ── Car controller ────────────────────────────────────────────── //
        if (carController == null)
        {
            carController = GetComponent<CarController2_VR>();
            if (carController == null)
                carController = GetComponentInParent<CarController2_VR>();
            if (carController == null)
                carController = Object.FindFirstObjectByType<CarController2_VR>();

            if (carController != null)
                Debug.Log($"VehicleManager: Auto-found CarController2_VR on '{carController.gameObject.name}'.");
            else
                Debug.LogWarning("VehicleManager: CarController2_VR not found!");
        }
    }

    // ── Seat the player ───────────────────────────────────────────────────  //

    private void SeatPlayer()
    {
        if (inCar) return;

        if (playerRig == null)
        {
            Debug.LogError("VehicleManager: Cannot seat player — no player rig found.");
            return;
        }

        inCar = true;

        // Use Driver Anchor position, or fall back to Sedan1's own transform
        Transform target = seatAnchor != null ? seatAnchor : transform;

        // Rotate the rig to match the seat direction first
        playerRig.transform.rotation = target.rotation;

        // In VR the headset position offsets the camera from the rig root.
        // We subtract that offset so the player's EYES land at the anchor,
        // not the rig's pivot point (which would put eyes in the floor/ceiling).
        OVRCameraRig ovrRig = playerRig.GetComponentInChildren<OVRCameraRig>();
        if (ovrRig != null && ovrRig.centerEyeAnchor != null)
        {
            Vector3 eyeOffset = ovrRig.centerEyeAnchor.position - playerRig.transform.position;
            playerRig.transform.position = target.position - eyeOffset;
        }
        else
        {
            playerRig.transform.position = target.position;
        }

        // Disable CharacterController so it doesn't fight the car's physics
        CharacterController cc = playerRig.GetComponentInChildren<CharacterController>();
        if (cc != null) cc.enabled = false;

        // Disable OVRPlayerController so it can't move the rig away from the seat
        OVRPlayerController ovrPC = playerRig.GetComponent<OVRPlayerController>();
        if (ovrPC == null) ovrPC = playerRig.GetComponentInChildren<OVRPlayerController>();
        if (ovrPC != null) ovrPC.enabled = false;

        // ── STRAP THE RIG TO THE CAR ────────────────────────────────────── //
        // Sedan1 is the GameObject this script lives on AND the one with the
        // Rigidbody that the CarController2_VR drives. Parenting the rig to
        // Sedan1 (worldPositionStays = true) keeps the rig at its current
        // world pose but makes it follow the car every frame from now on.
        playerRig.transform.SetParent(transform, true);

        // Smooth out the headset against physics-driven motion
        Rigidbody carRb = GetComponent<Rigidbody>();
        if (carRb != null) carRb.interpolation = RigidbodyInterpolation.Interpolate;

        // Enable driving immediately — no control-schema gate
        if (carController != null)
        {
            carController.enabled = true;
            carController.inputLocked = false;
        }

        Debug.Log("VehicleManager: Player seated and parented to car. Driving unlocked.");
    }

    // ── Public API ────────────────────────────────────────────────────────  //

    public bool IsInCar() => inCar;

    /// <summary>Called by SteeringWheelInteraction_OVR every frame.</summary>
    public void SetSteering(float value)
    {
        if (inCar && carController != null)
            carController.SetExternalSteering(value);
    }
}

using UnityEngine;

/// <summary>
/// VehicleManager - Always-in-car version.
/// Player spawns directly in the driver's seat at game start.
/// No exit functionality — player is always driving.
/// Attach this to your Car's root GameObject (Sedan1).
/// </summary>
public class VehicleManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Your OculusInteractionSampleRig GameObject")]
    public GameObject playerRig;

    [Tooltip("Your CarController2_VR script on the car")]
    public CarController2_VR carController;

    [Tooltip("The 'Anchor Car' Transform — the driver's seat position/rotation")]
    public Transform seatAnchor;

    // Tracks whether the player has been seated (used as a safety guard)
    private bool inCar = false;

    void Start()
    {
        if (playerRig == null)
        {
            Debug.LogError("VehicleManager: PlayerRig not assigned!");
            return;
        }

        if (seatAnchor == null)
        {
            Debug.LogWarning("VehicleManager: SeatAnchor not assigned — creating a default one.");
            seatAnchor = new GameObject("SeatAnchor").transform;
            seatAnchor.SetParent(transform);
            seatAnchor.localPosition = new Vector3(-0.4f, 1.5f, 0f); // Approximate driver position
        }

        // Seat the player immediately on game start
        EnterCar();
    }

    /// <summary>
    /// Parents the player rig to the car and positions them at the seat anchor.
    /// Called automatically in Start().
    /// </summary>
    private void EnterCar()
    {
        if (inCar) return;
        inCar = true;

        playerRig.transform.SetParent(transform);
        playerRig.transform.rotation = seatAnchor.rotation;

        // Correct for VR head offset so eyes land at the Anchor, not the rig root
        OVRCameraRig ovrRig = playerRig.GetComponentInChildren<OVRCameraRig>();
        if (ovrRig != null && ovrRig.centerEyeAnchor != null)
        {
            Vector3 eyeOffset = ovrRig.centerEyeAnchor.position - playerRig.transform.position;
            playerRig.transform.position = seatAnchor.position - eyeOffset;
        }
        else
        {
            playerRig.transform.position = seatAnchor.position;
        }

        CharacterController cc = playerRig.GetComponentInChildren<CharacterController>();
        if (cc != null) cc.enabled = false;

        if (carController != null) carController.enabled = true;

        Debug.Log("VehicleManager: Player seated at Driver Anchor.");
    }

    /// <summary>
    /// Returns whether the player is currently seated.
    /// </summary>
    public bool IsInCar()
    {
        return inCar;
    }

    /// <summary>
    /// Called by SteeringWheelInteraction_OVR to pass steering input to the car.
    /// </summary>
    public void SetSteering(float value)
    {
        if (inCar && carController != null)
        {
            carController.SetExternalSteering(value);
        }
    }
}
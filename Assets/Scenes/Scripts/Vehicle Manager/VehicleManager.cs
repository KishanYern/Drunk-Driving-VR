using UnityEngine;

/// <summary>
/// Enhanced VehicleManager for VR car enter/exit functionality.
/// Attach this to your Car's root GameObject.
/// </summary>
public class VehicleManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Your OVRPlayerController or XR Rig")]
    public GameObject playerRig;

    [Tooltip("Your car controller script")]
    public CarController2_VR carController;

    [Tooltip("Empty GameObject at driver's head/seat position")]
    public Transform seatAnchor;

    [Header("Exit Settings")]
    [Tooltip("Distance to move player when exiting (to the side)")]
    public float exitDistance = 1.5f;

    [Tooltip("Exit on the right side of the car (uncheck for left)")]
    public bool exitOnRightSide = true;

    [Header("Optional - Disable Player Movement")]
    [Tooltip("Reference to OVRPlayerController if you want to disable movement while driving")]
    public MonoBehaviour ovrPlayerController;

    private bool inCar = false;
    private Vector3 originalPlayerPosition;
    private Quaternion originalPlayerRotation;
    private Transform originalPlayerParent;

    void Start()
    {
        if (carController != null)
            carController.enabled = false;

        if (playerRig == null)
            Debug.LogError("PlayerRig not assigned to VehicleManager!");

        if (seatAnchor == null)
        {
            Debug.LogWarning("SeatAnchor not assigned. Creating one at car's position.");
            seatAnchor = new GameObject("SeatAnchor").transform;
            seatAnchor.SetParent(transform);
            seatAnchor.localPosition = new Vector3(0, 1.5f, 0);
        }
    }

    void Update()
    {
        // Keyboard fallback to enter/exit the car without VR hands
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame)
        {
            ToggleVehicle();
        }
    }

    /// <summary>Toggle between entering and exiting the car.</summary>
    public void ToggleVehicle()
    {
        if (!inCar) EnterCar();
        else ExitCar();
    }

    public void EnterCar()
    {
        if (inCar || playerRig == null) return;

        Debug.Log("Entering car...");
        inCar = true;

        originalPlayerPosition = playerRig.transform.position;
        originalPlayerRotation = playerRig.transform.rotation;
        originalPlayerParent = playerRig.transform.parent;

        playerRig.transform.SetParent(transform);
        playerRig.transform.position = seatAnchor.position;
        playerRig.transform.rotation = seatAnchor.rotation;

        CharacterController cc = playerRig.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        if (ovrPlayerController != null) ovrPlayerController.enabled = false;
        if (carController != null) carController.enabled = true;
    }

    public void ExitCar()
    {
        if (!inCar || playerRig == null) return;

        Debug.Log("Exiting car...");
        inCar = false;

        playerRig.transform.SetParent(originalPlayerParent);

        Vector3 exitDir = exitOnRightSide ? transform.right : -transform.right;
        playerRig.transform.position = transform.position + exitDir * exitDistance;

        CharacterController cc = playerRig.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = true;

        if (ovrPlayerController != null) ovrPlayerController.enabled = true;
        if (carController != null) carController.enabled = false;
    }

    public bool IsInCar() => inCar;

    void OnDestroy()
    {
        if (inCar) ExitCar();
    }

    /// <summary>
    /// Called every frame by SteeringWheelInteraction_OVR.
    /// Passes the normalised steering value (-1..1) straight to the car.
    /// </summary>
    public void SetSteering(float value)
    {
        if (!inCar || carController == null) return;
        carController.SetExternalSteering(value);
    }
}
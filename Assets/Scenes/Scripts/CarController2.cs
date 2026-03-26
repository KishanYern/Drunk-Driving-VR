/*
VR ADAPTATION: Modified for Meta Quest 3 by [Your Name]
Original script by Mena - Modified for VR hand controller input
*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public class CarController2_VR : MonoBehaviour
{
    // Set every frame by SteeringWheelInteraction_OVR via VehicleManager.SetSteering()
    private float externalSteeringInput = 0f;
    public void SetExternalSteering(float value)
    {
        externalSteeringInput = value;
    }

    //CAR SETUP
    [Space(20)]
    [Header("CAR SETUP")]
    [Space(10)]
    [Range(20, 190)]
    public int maxSpeed = 90;
    [Range(10, 120)]
    public int maxReverseSpeed = 45;
    [Range(1, 10)]
    public int accelerationMultiplier = 2;
    [Space(10)]
    [Range(10, 45)]
    public int maxSteeringAngle = 27;
    [Range(0.1f, 1f)]
    public float steeringSpeed = 0.5f;
    [Space(10)]
    [Range(100, 600)]
    public int brakeForce = 350;
    [Range(1, 10)]
    public int decelerationMultiplier = 2;
    [Range(1, 10)]
    public int handbrakeDriftMultiplier = 5;
    [Space(10)]
    public Vector3 bodyMassCenter;

    //VR CONTROLS
    [Space(20)]
    [Header("VR CONTROL SETTINGS")]
    [Space(10)]
    [Tooltip("Minimum trigger value to register acceleration/brake (0-1)")]
    [Range(0.1f, 0.9f)]
    public float triggerThreshold = 0.2f;

    //WHEELS
    [Space(20)]
    [Header("WHEELS")]
    public GameObject frontLeftMesh;
    public WheelCollider frontLeftCollider;
    [Space(10)]
    public GameObject frontRightMesh;
    public WheelCollider frontRightCollider;
    [Space(10)]
    public GameObject rearLeftMesh;
    public WheelCollider rearLeftCollider;
    [Space(10)]
    public GameObject rearRightMesh;
    public WheelCollider rearRightCollider;

    //PARTICLE SYSTEMS
    [Space(20)]
    [Header("EFFECTS")]
    [Space(10)]
    public bool useEffects = false;
    public ParticleSystem RLWParticleSystem;
    public ParticleSystem RRWParticleSystem;
    [Space(10)]
    public TrailRenderer RLWTireSkid;
    public TrailRenderer RRWTireSkid;

    //SPEED TEXT (UI)
    [Space(20)]
    [Header("UI")]
    [Space(10)]
    public bool useUI = false;
    public Text carSpeedText;

    //SOUNDS
    [Space(20)]
    [Header("Sounds")]
    [Space(10)]
    public bool useSounds = false;
    public AudioSource carEngineSound;
    public AudioSource tireScreechSound;
    float initialCarEngineSoundPitch;

    //CAR DATA
    [HideInInspector] public float carSpeed;
    [HideInInspector] public bool isDrifting;
    [HideInInspector] public bool isTractionLocked;

    //PRIVATE VARIABLES
    Rigidbody carRigidbody;
    float steeringAxis;
    float throttleAxis;
    float localVelocityZ;
    float localVelocityX;
    bool deceleratingCar;

    WheelFrictionCurve FLwheelFriction; float FLWextremumSlip;
    WheelFrictionCurve FRwheelFriction; float FRWextremumSlip;
    WheelFrictionCurve RLwheelFriction; float RLWextremumSlip;
    WheelFrictionCurve RRwheelFriction; float RRWextremumSlip;

    //VR INPUT DEVICES
    private InputDevice rightController;
    private InputDevice leftController;
    private bool controllersInitialized = false;

    // Torque per wheel. Old value of 50 was far too weak for a 500 kg body.
    // 200 gives 400 Nm/wheel = 1600 Nm total — snappy but not spinny.
    // Raise if still sluggish, lower if wheels spin wildly on the spot.
    private const float TORQUE_SCALE = 200f;

    void Start()
    {
        carRigidbody = GetComponent<Rigidbody>();
        carRigidbody.centerOfMass = bodyMassCenter;

        InitializeWheelFriction();
        InitializeVRControllers();

        if (carEngineSound != null)
            initialCarEngineSoundPitch = carEngineSound.pitch;

        if (useUI)
            InvokeRepeating("CarSpeedUI", 0f, 0.1f);
        else if (carSpeedText != null)
            carSpeedText.text = "0";

        if (useSounds)
            InvokeRepeating("CarSounds", 0f, 0.1f);
        else
        {
            if (carEngineSound != null) carEngineSound.Stop();
            if (tireScreechSound != null) tireScreechSound.Stop();
        }

        if (!useEffects) DisableEffects();
    }

    void InitializeWheelFriction()
    {
        FLwheelFriction = CopyFriction(frontLeftCollider.sidewaysFriction, out FLWextremumSlip);
        FRwheelFriction = CopyFriction(frontRightCollider.sidewaysFriction, out FRWextremumSlip);
        RLwheelFriction = CopyFriction(rearLeftCollider.sidewaysFriction, out RLWextremumSlip);
        RRwheelFriction = CopyFriction(rearRightCollider.sidewaysFriction, out RRWextremumSlip);
    }

    WheelFrictionCurve CopyFriction(WheelFrictionCurve src, out float extremumSlip)
    {
        extremumSlip = src.extremumSlip;
        return new WheelFrictionCurve
        {
            extremumSlip = src.extremumSlip,
            extremumValue = src.extremumValue,
            asymptoteSlip = src.asymptoteSlip,
            asymptoteValue = src.asymptoteValue,
            stiffness = src.stiffness
        };
    }

    void InitializeVRControllers()
    {
        var right = new List<InputDevice>();
        var left = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, right);
        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, left);

        if (right.Count > 0) { rightController = right[0]; controllersInitialized = true; }
        if (left.Count > 0) leftController = left[0];

        if (!controllersInitialized)
            Debug.LogWarning("VR Controllers not found. Will retry in Update.");
    }

    void Update()
    {
        if (!controllersInitialized) InitializeVRControllers();

        carSpeed = (2 * Mathf.PI * frontLeftCollider.radius * frontLeftCollider.rpm * 60) / 1000;
        localVelocityX = transform.InverseTransformDirection(carRigidbody.linearVelocity).x;
        localVelocityZ = transform.InverseTransformDirection(carRigidbody.linearVelocity).z;

        HandleVRInput();
        AnimateWheelMeshes();
    }

    void HandleVRInput()
    {
        float rightTrigger = 0f;
        float leftTrigger = 0f;

        // --- ACCELERATION (right trigger) ---
        if (rightController.TryGetFeatureValue(CommonUsages.trigger, out rightTrigger)
            && rightTrigger > triggerThreshold)
        {
            CancelInvoke("DecelerateCar");
            deceleratingCar = false;
            GoForward();
        }

        // --- BRAKE / REVERSE (left trigger) ---
        if (leftController.TryGetFeatureValue(CommonUsages.trigger, out leftTrigger)
            && leftTrigger > triggerThreshold)
        {
            CancelInvoke("DecelerateCar");
            deceleratingCar = false;
            GoReverse();
        }

        // --- STEERING — always driven by the physical steering wheel ---
        // externalSteeringInput is updated every frame by SteeringWheelInteraction_OVR
        // (including while self-centring after release), so we just apply it directly.
        ApplyExternalSteering(externalSteeringInput);

        // --- DECELERATION when both triggers released ---
        if (rightTrigger <= triggerThreshold && leftTrigger <= triggerThreshold)
        {
            ThrottleOff();
            if (!deceleratingCar)
            {
                InvokeRepeating("DecelerateCar", 0f, 0.1f);
                deceleratingCar = true;
            }
        }
    }

    private void ApplyExternalSteering(float normalizedInput)
    {
        steeringAxis = Mathf.Clamp(normalizedInput, -1f, 1f);

        // Power curve: makes small inputs feel snappier without over-rotating at full lock.
        // Exponent 0.7 = more responsive near centre; raise toward 1.0 for linear, lower for more snap.
        float curved = Mathf.Sign(steeringAxis) * Mathf.Pow(Mathf.Abs(steeringAxis), 0.7f);

        float targetAngle = curved * maxSteeringAngle;

        // Snap directly — no lerp. The physical wheel IS the input device, so
        // smoothing here just adds lag. Remove steeringSpeed entirely for arcade feel.
        frontLeftCollider.steerAngle = targetAngle;
        frontRightCollider.steerAngle = targetAngle;
    }

    public void CarSpeedUI()
    {
        if (!useUI) return;
        try { carSpeedText.text = Mathf.RoundToInt(Mathf.Abs(carSpeed)).ToString(); }
        catch (Exception ex) { Debug.LogWarning(ex); }
    }

    public void CarSounds()
    {
        if (!useSounds) return;
        try
        {
            if (carEngineSound != null)
                carEngineSound.pitch = initialCarEngineSoundPitch
                                       + Mathf.Abs(carRigidbody.linearVelocity.magnitude) / 25f;

            bool screeching = isDrifting || (isTractionLocked && Mathf.Abs(carSpeed) > 12f);
            if (screeching && !tireScreechSound.isPlaying) tireScreechSound.Play();
            if (!screeching && tireScreechSound.isPlaying) tireScreechSound.Stop();
        }
        catch (Exception ex) { Debug.LogWarning(ex); }
    }

    void AnimateWheelMeshes()
    {
        try
        {
            Quaternion rot; Vector3 pos;
            frontLeftCollider.GetWorldPose(out pos, out rot);
            frontLeftMesh.transform.SetPositionAndRotation(pos, rot);
            frontRightCollider.GetWorldPose(out pos, out rot);
            frontRightMesh.transform.SetPositionAndRotation(pos, rot);
            rearLeftCollider.GetWorldPose(out pos, out rot);
            rearLeftMesh.transform.SetPositionAndRotation(pos, rot);
            rearRightCollider.GetWorldPose(out pos, out rot);
            rearRightMesh.transform.SetPositionAndRotation(pos, rot);
        }
        catch (Exception ex) { Debug.LogWarning(ex); }
    }

    //ENGINE AND BRAKING
    public void GoForward()
    {
        isDrifting = Mathf.Abs(localVelocityX) > 2.5f;
        DriftCarPS();

        throttleAxis = Mathf.Min(throttleAxis + Time.deltaTime * 3f, 1f);

        if (localVelocityZ < -1f)
        {
            Brakes();
        }
        else if (Mathf.RoundToInt(carSpeed) < maxSpeed)
        {
            float t = TORQUE_SCALE * accelerationMultiplier * throttleAxis;
            SetAllMotorTorque(t, 0);
        }
        else
        {
            SetAllMotorTorque(0, -1);
        }
    }

    public void GoReverse()
    {
        isDrifting = Mathf.Abs(localVelocityX) > 2.5f;
        DriftCarPS();

        throttleAxis = Mathf.Max(throttleAxis - Time.deltaTime * 3f, -1f);

        if (localVelocityZ > 1f)
        {
            Brakes();
        }
        else if (Mathf.Abs(Mathf.RoundToInt(carSpeed)) < maxReverseSpeed)
        {
            float t = TORQUE_SCALE * accelerationMultiplier * throttleAxis;
            SetAllMotorTorque(t, 0);
        }
        else
        {
            SetAllMotorTorque(0, -1);
        }
    }

    public void ThrottleOff() => SetAllMotorTorque(0, -1);

    public void DecelerateCar()
    {
        isDrifting = Mathf.Abs(localVelocityX) > 2.5f;
        DriftCarPS();

        if (throttleAxis != 0f)
        {
            throttleAxis += (throttleAxis > 0f ? -1f : 1f) * Time.deltaTime * 10f;
            if (Mathf.Abs(throttleAxis) < 0.15f) throttleAxis = 0f;
        }

        carRigidbody.linearVelocity *= 1f / (1f + 0.025f * decelerationMultiplier);
        SetAllMotorTorque(0, -1);

        if (carRigidbody.linearVelocity.magnitude < 0.25f)
        {
            carRigidbody.linearVelocity = Vector3.zero;
            CancelInvoke("DecelerateCar");
        }
    }

    public void Brakes()
    {
        frontLeftCollider.brakeTorque = brakeForce;
        frontRightCollider.brakeTorque = brakeForce;
        rearLeftCollider.brakeTorque = brakeForce;
        rearRightCollider.brakeTorque = brakeForce;
    }

    // motor = torque value to set; brake = -1 to leave untouched, else set that brake value
    private void SetAllMotorTorque(float motor, float brake)
    {
        frontLeftCollider.motorTorque = motor;
        frontRightCollider.motorTorque = motor;
        rearLeftCollider.motorTorque = motor;
        rearRightCollider.motorTorque = motor;
        if (brake >= 0)
        {
            frontLeftCollider.brakeTorque = brake;
            frontRightCollider.brakeTorque = brake;
            rearLeftCollider.brakeTorque = brake;
            rearRightCollider.brakeTorque = brake;
        }
    }

    public void DriftCarPS()
    {
        if (!useEffects) return;
        try
        {
            if (isDrifting) { RLWParticleSystem.Play(); RRWParticleSystem.Play(); }
            else { RLWParticleSystem.Stop(); RRWParticleSystem.Stop(); }
        }
        catch (Exception ex) { Debug.LogWarning(ex); }
        try
        {
            bool skid = (isTractionLocked || Mathf.Abs(localVelocityX) > 5f) && Mathf.Abs(carSpeed) > 12f;
            RLWTireSkid.emitting = skid;
            RRWTireSkid.emitting = skid;
        }
        catch (Exception ex) { Debug.LogWarning(ex); }
    }

    void DisableEffects()
    {
        if (RLWParticleSystem != null) RLWParticleSystem.Stop();
        if (RRWParticleSystem != null) RRWParticleSystem.Stop();
        if (RLWTireSkid != null) RLWTireSkid.emitting = false;
        if (RRWTireSkid != null) RRWTireSkid.emitting = false;
    }
}
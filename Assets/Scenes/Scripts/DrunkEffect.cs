using UnityEngine;

/// <summary>
/// Attach to the OVRCameraRig (or any parent of the camera) to apply drunk visual effects.
/// Call AddSip() from BottleGrabbable each time the player takes a sip.
/// Impairment level increases with each sip and slowly fades over time.
/// </summary>
public class DrunkEffect : MonoBehaviour
{
    [Header("Impairment Settings")]
    [Tooltip("How much each sip increases impairment (0-1 scale)")]
    public float impairmentPerSip = 0.15f;
    [Tooltip("Maximum impairment level")]
    [Range(0f, 1f)]
    public float maxImpairment = 1f;
    [Tooltip("How fast impairment fades over time (units per second)")]
    public float sobringUpRate = 0.01f;

    [Header("Wobble Settings")]
    [Tooltip("Maximum angle of camera sway in degrees")]
    public float maxWobbleAngle = 8f;
    [Tooltip("Speed of the wobble oscillation")]
    public float wobbleSpeed = 1.2f;

    [Header("Double Vision Settings")]
    [Tooltip("The URP Material used for the Double Vision effect")]
    public Material doubleVisionMaterial;
    [Tooltip("The maximum horizontal offset for the double vision when fully impaired")]
    public float maxDoubleVisionOffset = 0.02f;

    [Header("Input Settings")]
    [Tooltip("Keyboard key to press to take a sip and increase drunkenness")]
    public UnityEngine.InputSystem.Key drinkKey = UnityEngine.InputSystem.Key.B;

    private float impairmentLevel = 0f;
    private Transform cameraTransform;
    private Quaternion originalLocalRotation;
    private float wobbleTimeOffset;

    public float ImpairmentLevel => impairmentLevel;

    void Start()
    {
        OVRCameraRig rig = GetComponent<OVRCameraRig>();
        if (rig != null)
        {
            cameraTransform = rig.centerEyeAnchor;
        }
        else if (GetComponent<Camera>() != null)
        {
            // If attached directly to the camera
            cameraTransform = transform;
        }
        else
        {
            // Fallback: look for the center eye anchor in children
            Transform trackingSpace = transform.Find("TrackingSpace");
            if (trackingSpace != null)
                cameraTransform = trackingSpace.Find("CenterEyeAnchor");
        }

        if (cameraTransform == null)
            Debug.LogWarning("DrunkEffect: Could not find CenterEyeAnchor. Wobble effect will not work.");
        else
            originalLocalRotation = cameraTransform.localRotation;

        wobbleTimeOffset = Random.Range(0f, 100f);
        
        // Ensure double vision starts at 0
        if (doubleVisionMaterial != null)
        {
            doubleVisionMaterial.SetVector("_Offset", Vector4.zero);
        }
    }

    void Update()
    {
        // Simple keyboard button to take a sip (using the new Input System)
        if (UnityEngine.InputSystem.Keyboard.current != null && 
            UnityEngine.InputSystem.Keyboard.current[drinkKey].wasPressedThisFrame)
        {
            AddSip();
        }

        if (cameraTransform == null)
            return;

        if (impairmentLevel <= 0f)
        {
            // Ensure camera rotation is restored when completely sober
            cameraTransform.localRotation = originalLocalRotation;
            if (doubleVisionMaterial != null) doubleVisionMaterial.SetVector("_Offset", Vector4.zero);
            return;
        }

        // Slowly sober up over time
        impairmentLevel = Mathf.Max(0f, impairmentLevel - sobringUpRate * Time.deltaTime);

        if (doubleVisionMaterial != null)
        {
            // Update double vision based on impairment level
            float currentOffset = maxDoubleVisionOffset * impairmentLevel;
            doubleVisionMaterial.SetVector("_Offset", new Vector4(currentOffset, 0f, 0f, 0f));
        }

        if (impairmentLevel <= 0f)
        {
            // Impairment just reached zero; reset rotation and skip wobble
            cameraTransform.localRotation = originalLocalRotation;
            return;
        }
        ApplyWobble();
    }

    /// <summary>
    /// Called by BottleGrabbable each time the player takes a sip.
    /// </summary>
    public void AddSip()
    {
        impairmentLevel = Mathf.Min(maxImpairment, impairmentLevel + impairmentPerSip);
        Debug.Log("[DrunkEffect] Impairment level: " + impairmentLevel.ToString("F2"));
    }

    void ApplyWobble()
    {
        if (cameraTransform == null) return;

        float t = Time.time * wobbleSpeed + wobbleTimeOffset;

        // Use two sine waves at different frequencies for a natural drunk sway
        float rollAngle  = Mathf.Sin(t * 1.0f) * maxWobbleAngle * impairmentLevel;
        float pitchAngle = Mathf.Sin(t * 0.7f + 1.3f) * (maxWobbleAngle * 0.5f) * impairmentLevel;
        float yawAngle   = Mathf.Sin(t * 0.5f + 2.6f) * (maxWobbleAngle * 0.4f) * impairmentLevel;

        cameraTransform.localRotation = originalLocalRotation *
            Quaternion.Euler(pitchAngle, yawAngle, rollAngle);
    }

    void OnDisable()
    {
        // Restore camera rotation when script is disabled
        if (cameraTransform != null)
            cameraTransform.localRotation = originalLocalRotation;
            
        // Reset double vision
        if (doubleVisionMaterial != null)
            doubleVisionMaterial.SetVector("_Offset", Vector4.zero);
    }

    void OnGUI()
    {
        // Simple debug UI to show the drunk level on screen
        GUIStyle style = new GUIStyle();
        style.fontSize = 40;
        style.normal.textColor = Color.red;
        style.fontStyle = FontStyle.Bold;
        GUI.Label(new Rect(20, 20, 400, 100), "DRUNK LEVEL: " + (impairmentLevel * 100f).ToString("F0") + "%", style);
    }
}

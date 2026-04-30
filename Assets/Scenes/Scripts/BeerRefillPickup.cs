using UnityEngine;

/// <summary>
/// A floating grabbable pickup. When the player gets a hand close and presses
/// grip / index trigger, the pickup:
///   1. Plays its pickup SFX
///   2. Calls RefillAll() on the assigned BeerBottleSpawner (which refills all
///      bottles and plays each bottle's own refillClip)
///   3. Destroys itself after a short delay so the SFX can finish.
///
/// The pickup also gently bobs and spins so it's easy to spot.
///
/// Spawning is handled by BeerRefillPickupSpawner (which sets `spawner` and
/// schedules the next pickup). You can also drop one of these in the scene
/// manually for testing.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BeerRefillPickup : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    // Inspector
    // ------------------------------------------------------------------ //

    [Header("Refill Target")]
    [Tooltip("Spawner that owns the bottle slots. RefillAll() is called when this pickup is grabbed. " +
             "Auto-found if left blank.")]
    public BeerBottleSpawner spawner;

    [Tooltip("Optional: this spawner will be told the pickup was consumed (so it can schedule a new one).")]
    public BeerRefillPickupSpawner pickupSpawner;

    [Header("Grab")]
    [Tooltip("Hand must be within this distance to highlight + grab.")]
    public float grabRadius = 0.4f;
    [Tooltip("Allow either hand to grab the pickup.")]
    public bool allowEitherHand = true;
    [Tooltip("Accept the grip button (hand trigger) in addition to the index trigger.")]
    public bool acceptGripButton = true;
    [Tooltip("If true, the pickup is consumed on hand proximity alone (no button press required). " +
             "Useful for VR comfort � just touch it.")]
    public bool grabOnTouch = false;

    [Header("Audio")]
    [Tooltip("Sound played when the pickup is grabbed. Drag your .wav/.mp3 from Assets/Sounds.")]
    public AudioClip pickupClip;
    [Range(0f, 1f)] public float pickupVolume = 1f;
    [Tooltip("Auto-create an AudioSource on this object if none is present.")]
    public bool autoCreateAudioSource = true;
    [Tooltip("Optional explicit AudioSource. If left blank we use / auto-create one on this GameObject.")]
    public AudioSource audioSource;

    [Header("Visual Feedback")]
    [Tooltip("How fast the pickup bobs up and down (Hz).")]
    public float bobFrequency = 1.2f;
    [Tooltip("How far the pickup bobs up and down (metres).")]
    public float bobAmplitude = 0.04f;
    [Tooltip("Degrees per second the pickup spins on its local Y axis.")]
    public float spinSpeed = 60f;
    [Tooltip("Optional material to swap in while the player's hand is hovering close. " +
             "If empty, a yellow tint is applied as a fallback.")]
    public Material highlightMaterial;
    [Tooltip("Optional 'glow' light child. Auto-pulses while the pickup is active.")]
    public Light glowLight;
    [Tooltip("Min/max intensity for the auto-pulsing glow light (X = min, Y = max).")]
    public Vector2 glowIntensityRange = new Vector2(0.6f, 1.4f);

    [Header("Lifetime")]
    [Tooltip("After being grabbed, wait this many seconds before destroying the GameObject (lets SFX finish).")]
    public float destroyDelay = 1.0f;

    // ------------------------------------------------------------------ //
    // Private
    // ------------------------------------------------------------------ //

    private Transform rightHand;
    private Transform leftHand;
    private Transform headAnchor;
    private Renderer[] allRenderers;
    private Material[] originalMaterials;
    private Vector3 startLocalPos;
    private bool consumed;
    private float spawnTime;

    // ------------------------------------------------------------------ //

    void Start()
    {
        // Snapshot starting local position for bobbing.
        startLocalPos = transform.localPosition;
        spawnTime = Time.time;

        // Cache renderers + their original materials.
        allRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        originalMaterials = new Material[allRenderers.Length];
        for (int i = 0; i < allRenderers.Length; i++)
            originalMaterials[i] = allRenderers[i] != null ? allRenderers[i].material : null;

        // Audio source.
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null && autoCreateAudioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 0.3f;
            audioSource.maxDistance = 8f;
        }

        // Find OVR rig.
        OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();
        if (rig != null)
        {
            rightHand  = rig.rightHandAnchor;
            leftHand   = rig.leftHandAnchor;
            headAnchor = rig.centerEyeAnchor;
        }

        // Auto-find the bottle spawner if the pickup spawner didn't wire it.
        if (spawner == null) spawner = Object.FindFirstObjectByType<BeerBottleSpawner>();

        // Make sure the rigidbody won't fall on the floor.
        var rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.detectCollisions = false;
    }

    void Update()
    {
        if (consumed) return;

        // ----- bob + spin -----
        float bob = Mathf.Sin((Time.time - spawnTime) * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
        transform.localPosition = startLocalPos + new Vector3(0f, bob, 0f);
        if (Mathf.Abs(spinSpeed) > 0.01f)
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);

        // ----- glow pulse -----
        if (glowLight != null)
        {
            float t01 = (Mathf.Sin((Time.time - spawnTime) * bobFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
            glowLight.intensity = Mathf.Lerp(glowIntensityRange.x, glowIntensityRange.y, t01);
        }

        // ----- hand interaction -----
        OVRInput.Controller controller;
        Transform hand = GetClosestHandInRange(out controller);

        if (hand != null)
        {
            ApplyHighlight();

            bool triggered = grabOnTouch || GrabPressedThisFrame(controller, acceptGripButton);
            if (triggered) Consume();
        }
        else
        {
            RestoreMaterials();
        }
    }

    // ------------------------------------------------------------------ //

    /// <summary>The actual refill + SFX + cleanup. Public so a spawner can force-fire if needed.</summary>
    public void Consume()
    {
        if (consumed) return;
        consumed = true;

        // SFX first so it overlaps with the refill sounds nicely.
        if (audioSource != null && pickupClip != null)
            audioSource.PlayOneShot(pickupClip, pickupVolume);

        // Trigger the actual refill.
        if (spawner != null)
        {
            int n = spawner.RefillAll();
            Debug.Log($"[BeerRefillPickup] Consumed � refilled {n} bottle(s).");
        }
        else
        {
            Debug.LogWarning("[BeerRefillPickup] No BeerBottleSpawner reference � nothing to refill.");
        }

        // Notify the pickup spawner so it can schedule the next one.
        if (pickupSpawner != null) pickupSpawner.NotifyPickupConsumed();

        // Hide visuals immediately, but keep the GameObject alive long enough
        // for the AudioClip to finish playing.
        foreach (var r in allRenderers) if (r != null) r.enabled = false;
        if (glowLight != null) glowLight.enabled = false;

        Destroy(gameObject, Mathf.Max(0.05f, destroyDelay));
    }

    // ------------------------------------------------------------------ //
    // Hand detection (mirrors BottleGrabbable)
    // ------------------------------------------------------------------ //

    private Transform GetClosestHandInRange(out OVRInput.Controller controller)
    {
        controller = OVRInput.Controller.RTouch;
        Transform best = null;
        float bestDist = grabRadius;

        if (rightHand != null)
        {
            float d = Vector3.Distance(transform.position, rightHand.position);
            if (d < bestDist) { bestDist = d; best = rightHand; controller = OVRInput.Controller.RTouch; }
        }
        if (allowEitherHand && leftHand != null)
        {
            float d = Vector3.Distance(transform.position, leftHand.position);
            if (d < bestDist) { bestDist = d; best = leftHand; controller = OVRInput.Controller.LTouch; }
        }
        return best;
    }

    private static bool GrabPressedThisFrame(OVRInput.Controller c, bool acceptGrip)
    {
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, c)) return true;
        if (acceptGrip && OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, c)) return true;
        return false;
    }

    // ------------------------------------------------------------------ //
    // Highlighting
    // ------------------------------------------------------------------ //

    private void ApplyHighlight()
    {
        if (highlightMaterial == null)
        {
            foreach (var r in allRenderers)
                if (r != null) r.material.color = Color.yellow;
            return;
        }

        foreach (var r in allRenderers)
            if (r != null) r.material = highlightMaterial;
    }

    private void RestoreMaterials()
    {
        for (int i = 0; i < allRenderers.Length; i++)
            if (allRenderers[i] != null && originalMaterials[i] != null)
                allRenderers[i].material = originalMaterials[i];
    }

    // ------------------------------------------------------------------ //

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, grabRadius);
    }
}

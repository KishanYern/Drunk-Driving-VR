using System.Collections;
using UnityEngine;

/// <summary>
/// FINAL VERSION - fixes:
///   1. rb.enabled = false while resting/flying/held - completely removes bottle
///      from PhysX so a moving car CANNOT push it.
///   2. Highlight targets ALL renderers on child objects (jager/default mesh).
///   3. Gaze detection works even when looking from driver seat angle.
///   4. No collisions needed while resting - bottle is pure transform.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BottleGrabbable : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    // Inspector
    // ------------------------------------------------------------------ //

    [Header("Car Anchor")]
    [Tooltip("Child Transform of the car where bottle rests. MOVE THIS OBJECT to position the bottle.")]
    public Transform bottleAnchor;
    [Tooltip("Fine-tune local position from anchor. Usually 0,0,0 - just move the Anchor object.")]
    public Vector3 anchorPositionOffset = Vector3.zero;
    [Tooltip("Local rotation at rest.")]
    public Vector3 anchorRotationOffset = Vector3.zero;

    [Header("Gaze + Proximity")]
    [Tooltip("Hand must be within this distance to highlight + attract.")]
    public float grabRadius = 0.5f;
    [Tooltip("Require the player to also be looking at the bottle. Turn OFF for easier grabbing.")]
    public bool requireGaze = false;
    [Tooltip("Degrees of gaze tolerance - how directly you need to look at the bottle (only used if requireGaze is on).")]
    [Range(5f, 90f)]
    public float gazeAngleThreshold = 45f;
    [Tooltip("Accept the grip button (hand trigger) in addition to the index trigger when grabbing.")]
    public bool acceptGripButton = true;
    [Tooltip("Allow either hand to grab the bottle (otherwise right hand only).")]
    public bool allowEitherHand = true;
    [Tooltip("Seconds to fly to hand. 0 = instant.")]
    public float flyDuration = 0.35f;

    [Header("Drinking")]
    [Tooltip("Distance from the head anchor at which the bottle starts counting as drinking. Bumped up so bringing it to your mouth or tilting it over your head both work.")]
    public float drinkRange = 0.55f;
    [Tooltip("How long you have to hold the bottle in the drinking pose to register one sip.")]
    public float drinkDuration = 1.0f;
    [Tooltip("If the bottle's local up axis points DOWN by more than this many degrees (i.e. it's tipped up to drink), the drink range is loosened. Set to 180 to disable tilt detection.")]
    [Range(15f, 180f)]
    public float drinkTiltAngle = 60f;
    [Tooltip("Extra distance allowed when the bottle is tilted into the drinking pose - makes 'over the head' chugging work.")]
    public float drinkRangeWhenTilted = 0.85f;
    [Tooltip("Local axis of the bottle that points UP when it's resting upright. Default = +Y. If your model is sideways, change this.")]
    public Vector3 bottleUpLocal = Vector3.up;
    [Tooltip("Log progress while drinking so you can see it firing in the Console.")]
    public bool logDrinkProgress = true;
    public DrunkEffect drunkEffect;

    [Header("Throw")]
    public float throwThreshold = 1.5f;

    [Header("Highlight Materials")]
    [Tooltip("Material shown when gaze + hand proximity detected. If none, colour tint is used instead.")]
    public Material attractReadyMaterial;
    [Tooltip("Leave empty - original material stored automatically at Start.")]
    public Material originalMaterialOverride;
    [Tooltip("Material applied while the player is actively drinking. Falls back to attractReadyMaterial, then to a green tint.")]
    public Material drinkingMaterial;

    [Header("Audio")]
    [Tooltip("Looped sip/glug sound played while the bottle is in the drinking pose. Drag your own clip in here.")]
    public AudioClip drinkLoopClip;
    [Tooltip("One-shot sound played when a sip is registered (impairment goes up).")]
    public AudioClip sipCompleteClip;
    [Tooltip("One-shot sound played when this bottle is refilled / respawned. Drop your .wav/.mp3 from Assets/Sounds.")]
    public AudioClip refillClip;
    [Range(0f, 1f)] public float drinkVolume = 0.8f;
    [Range(0f, 1f)] public float refillVolume = 1f;
    [Tooltip("If true, an AudioSource will be auto-added to this bottle if one isn't present.")]
    public bool autoCreateAudioSource = true;
    [Tooltip("Optional: drag a specific AudioSource here. If left blank, one on this GameObject is used / auto-created.")]
    public AudioSource audioSource;

    [Header("Drinking UI")]
    [Tooltip("Show a screen-space progress bar while drinking.")]
    public bool showDrinkingHUD = true;
    [Tooltip("Label drawn above the bar.")]
    public string drinkingLabel = "DRINKING...";

    // ------------------------------------------------------------------ //
    // Private
    // ------------------------------------------------------------------ //

    private enum State { Resting, Attracting, Held, Thrown }
    private State current = State.Resting;

    private Rigidbody rb;
    private Renderer[] allRenderers;    // captures jager AND default child
    private Material[] originalMaterials;

    private Transform rightHand;
    private Transform leftHand;
    private Transform activeHand;            // hand currently holding / attracting the bottle
    private OVRInput.Controller activeController = OVRInput.Controller.RTouch;
    private Transform headAnchor;
    private Vector3 prevHandPos;
    private Vector3 handVelocity;

    private bool isDrinking;
    private float drinkTimer;
    private Coroutine flyCoroutine;
    private bool hasInitialized;     // prevents refill sfx from firing on first spawn

    // ------------------------------------------------------------------ //

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Grab ALL renderers in the hierarchy (jager + default child mesh)
        allRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        originalMaterials = new Material[allRenderers.Length];
        for (int i = 0; i < allRenderers.Length; i++)
            originalMaterials[i] = originalMaterialOverride != null
                ? originalMaterialOverride
                : allRenderers[i].material;

        // ----- Audio source setup -----
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null && autoCreateAudioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f; // 3D
            audioSource.minDistance = 0.2f;
            audioSource.maxDistance = 5f;
        }

        // Find OVR anchors
        OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();
        if (rig != null)
        {
            rightHand = rig.rightHandAnchor;
            leftHand  = rig.leftHandAnchor;
            headAnchor = rig.centerEyeAnchor;
        }

        if (rightHand == null) Debug.LogWarning("[Bottle] RightHandAnchor not found.");
        if (headAnchor == null) Debug.LogWarning("[Bottle] CenterEyeAnchor not found.");

        SnapToAnchor();
    }

    void Update()
    {
        if (activeHand != null) TrackHandVelocity();

        switch (current)
        {
            case State.Resting: UpdateResting(); break;
            case State.Held: UpdateHeld(); break;
            case State.Attracting: break; // coroutine
            case State.Thrown: break; // physics
        }
    }

    /// <summary>Returns the closest hand within grabRadius, or null if none in range.</summary>
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

    private static bool GrabReleasedThisFrame(OVRInput.Controller c, bool acceptGrip)
    {
        if (OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, c)) return true;
        if (acceptGrip && OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, c)) return true;
        return false;
    }

    // ------------------------------------------------------------------ //
    // RESTING
    // ------------------------------------------------------------------ //

    private void SnapToAnchor()
    {
        current = State.Resting;
        if (isDrinking) StopDrinkingFeedback();
        isDrinking = false;
        drinkTimer = 0f;
        activeHand = null;

        // *** DEFINITIVE FIX: isKinematic + detectCollisions = false ***
        // detectCollisions = false removes the bottle from PhysX broadphase entirely.
        // Combined with isKinematic, the car CANNOT push or launch it.
        rb.isKinematic = true;
        rb.detectCollisions = false;   // << THE KEY LINE
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (bottleAnchor == null)
        {
            Debug.LogWarning("[Bottle] bottleAnchor not assigned! Bottle will freeze in world space.");
            return;
        }

        transform.SetParent(bottleAnchor, worldPositionStays: false);
        transform.localPosition = anchorPositionOffset;
        transform.localRotation = Quaternion.Euler(anchorRotationOffset);

        RestoreMaterials();

        // Play refill SFX whenever the bottle returns to its slot (but not on the
        // very first SnapToAnchor at spawn time).
        if (hasInitialized && audioSource != null && refillClip != null)
            audioSource.PlayOneShot(refillClip, refillVolume);

        hasInitialized = true;

        Debug.Log("[Bottle] Resting in car.");
    }

    private void UpdateResting()
    {
        if (rightHand == null) return;

        OVRInput.Controller candidate;
        Transform hand = GetClosestHandInRange(out candidate);
        bool handNear = hand != null;
        bool lookingAt = !requireGaze || IsLookingAtBottle();

        if (handNear && lookingAt)
        {
            ApplyHighlight();

            if (GrabPressedThisFrame(candidate, acceptGripButton))
            {
                activeHand = hand;
                activeController = candidate;
                prevHandPos = activeHand.position;
                BeginAttract();
            }
        }
        else
        {
            RestoreMaterials();
        }
    }

    private bool IsLookingAtBottle()
    {
        if (headAnchor == null) return false;
        Vector3 toBottle = (transform.position - headAnchor.position).normalized;
        return Vector3.Angle(headAnchor.forward, toBottle) < gazeAngleThreshold;
    }

    // ------------------------------------------------------------------ //
    // HIGHLIGHT - applies to ALL child renderers
    // ------------------------------------------------------------------ //

    private void ApplyHighlight()
    {
        if (attractReadyMaterial == null)
        {
            // No material assigned - tint all renderers yellow as fallback
            foreach (var r in allRenderers)
            {
                // Use emission colour tint if no material provided
                r.material.color = Color.yellow;
            }
            return;
        }

        foreach (var r in allRenderers)
            r.material = attractReadyMaterial;
    }

    private void RestoreMaterials()
    {
        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] != null)
                allRenderers[i].material = originalMaterials[i];
        }
    }

    // ------------------------------------------------------------------ //
    // ATTRACTING
    // ------------------------------------------------------------------ //

    private void BeginAttract()
    {
        current = State.Attracting;
        RestoreMaterials();

        transform.SetParent(null, worldPositionStays: true);
        // detectCollisions stays false during flight - no physics needed

        if (flyCoroutine != null) StopCoroutine(flyCoroutine);
        flyCoroutine = StartCoroutine(FlyToHand());
    }

    private IEnumerator FlyToHand()
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        if (flyDuration <= 0f) { SnapToHand(); yield break; }

        Transform target = activeHand != null ? activeHand : rightHand;
        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / flyDuration);
            transform.position = Vector3.Lerp(startPos, target.position, t);
            transform.rotation = Quaternion.Slerp(startRot, target.rotation, t);
            yield return null;
        }

        SnapToHand();
    }

    private void SnapToHand()
    {
        current = State.Held;
        Transform target = activeHand != null ? activeHand : rightHand;
        transform.position = target.position;
        transform.rotation = target.rotation;
        prevHandPos = target.position;
        Debug.Log("[Bottle] In hand.");
    }

    // ------------------------------------------------------------------ //
    // HELD
    // ------------------------------------------------------------------ //

    private void UpdateHeld()
    {
        Transform target = activeHand != null ? activeHand : rightHand;
        transform.position = target.position;
        transform.rotation = target.rotation;

        HandleDrinking();

        if (GrabReleasedThisFrame(activeController, acceptGripButton))
        {
            if (handVelocity.magnitude >= throwThreshold)
                Throw();
            else
                SnapToAnchor();
        }
    }

    // ------------------------------------------------------------------ //
    // THROWN
    // ------------------------------------------------------------------ //

    private void Throw()
    {
        current = State.Thrown;
        if (isDrinking) StopDrinkingFeedback();
        isDrinking = false;
        drinkTimer = 0f;
        transform.SetParent(null, worldPositionStays: true);

        // Re-enable physics only for throwing
        rb.detectCollisions = true;
        rb.isKinematic = false;
        rb.linearVelocity = handVelocity;
        rb.angularVelocity = Random.insideUnitSphere * 3f;
        Debug.Log($"[Bottle] Thrown at {handVelocity.magnitude:F1} m/s.");
    }

    // ------------------------------------------------------------------ //
    // DRINKING
    // ------------------------------------------------------------------ //

    private void HandleDrinking()
    {
        if (headAnchor == null) return;

        // How tilted is the bottle? Compare its local "up" axis (in world space)
        // against world-down. If you tip the bottle to pour into your mouth, the
        // bottle's up axis points downward, giving a small angle vs. -world-up.
        Vector3 bottleUpWorld = transform.TransformDirection(bottleUpLocal.normalized);
        float tiltAngle = Vector3.Angle(bottleUpWorld, Vector3.down);
        bool isTilted = tiltAngle <= drinkTiltAngle;

        // Range loosens when the bottle is in the drinking pose so over-the-head
        // chugging counts. Otherwise we use the tighter default range.
        float effectiveRange = isTilted ? drinkRangeWhenTilted : drinkRange;

        float dist = Vector3.Distance(transform.position, headAnchor.position);
        bool nearFace = dist <= effectiveRange;

        if (nearFace)
        {
            if (!isDrinking)
            {
                isDrinking = true; drinkTimer = 0f;
                StartDrinkingFeedback();
                if (logDrinkProgress)
                    Debug.Log($"[Bottle] Drinking started (dist={dist:F2}m, tilt={tiltAngle:F0}deg).");
            }
            drinkTimer += Time.deltaTime;
            if (drinkTimer >= drinkDuration) { drinkTimer = 0f; TakeSip(); }
        }
        else if (isDrinking)
        {
            if (logDrinkProgress)
                Debug.Log($"[Bottle] Drinking cancelled (dist={dist:F2}m > {effectiveRange:F2}m).");
            isDrinking = false; drinkTimer = 0f;
            StopDrinkingFeedback();
        }
    }

    private void TakeSip()
    {
        Debug.Log("[Bottle] Sip!");
        SipsTaken++;
        if (drunkEffect != null) drunkEffect.AddSip();
        else Debug.LogWarning("[Bottle] DrunkEffect not assigned! Drink registered but no DrunkEffect to apply it to.");

        if (audioSource != null && sipCompleteClip != null)
            audioSource.PlayOneShot(sipCompleteClip, drinkVolume);
    }

    // ------------------------------------------------------------------ //
    // Drinking feedback (sound + visual)
    // ------------------------------------------------------------------ //

    private void StartDrinkingFeedback()
    {
        // Play looped drink sound
        if (audioSource != null && drinkLoopClip != null)
        {
            audioSource.clip = drinkLoopClip;
            audioSource.loop = true;
            audioSource.volume = drinkVolume;
            if (!audioSource.isPlaying) audioSource.Play();
        }

        // Visual: swap to drinking material (or fallback)
        ApplyDrinkingMaterial();
    }

    private void StopDrinkingFeedback()
    {
        // Stop the loop (let one-shots like sipComplete keep playing)
        if (audioSource != null && audioSource.loop)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.clip = null;
        }

        // Restore original look
        RestoreMaterials();
    }

    private void ApplyDrinkingMaterial()
    {
        Material m = drinkingMaterial != null ? drinkingMaterial : attractReadyMaterial;
        if (m != null)
        {
            foreach (var r in allRenderers)
                if (r != null) r.material = m;
        }
        else
        {
            // No material assigned anywhere - tint green so it's visibly different from the
            // yellow "ready to grab" tint.
            foreach (var r in allRenderers)
                if (r != null) r.material.color = new Color(0.4f, 1f, 0.4f);
        }
    }

    /// <summary>True while a drink is being held in the right pose. Useful for UI / SFX.</summary>
    public bool IsDrinking => isDrinking;
    /// <summary>0..1 progress toward the next sip.</summary>
    public float DrinkProgress => drinkDuration > 0f ? Mathf.Clamp01(drinkTimer / drinkDuration) : 0f;

    // ------------------------------------------------------------------ //
    // Refill (called by BeerRefillPickup / BeerBottleSpawner.RefillAll)
    // ------------------------------------------------------------------ //

    /// <summary>How many sips have been taken from this bottle since its last refill.</summary>
    public int SipsTaken { get; private set; }
    /// <summary>True if this bottle is currently sitting in its anchor (not held / flying / thrown).</summary>
    public bool IsResting => current == State.Resting;

    /// <summary>
    /// Reset the bottle to its anchor as a fresh, full bottle. Plays the refillClip
    /// (via SnapToAnchor's built-in refill SFX hook). Safe to call on a bottle that
    /// is being held or has been thrown - it will be pulled out of the player's
    /// hand / off the ground and snapped back to its slot.
    /// </summary>
    public void Refill()
    {
        // Stop any in-flight attract coroutine.
        if (flyCoroutine != null) { StopCoroutine(flyCoroutine); flyCoroutine = null; }

        // Drop hand state so SnapToAnchor doesn't think we're held.
        activeHand = null;

        // Reset sips first so this bottle is "full" again.
        SipsTaken = 0;

        // Re-park at the anchor (this also plays the refill SFX via the
        // hasInitialized branch in SnapToAnchor).
        SnapToAnchor();

        Debug.Log($"[Bottle] Refilled '{name}'.");
    }

    // ------------------------------------------------------------------ //
    // Helpers
    // ------------------------------------------------------------------ //

    private void TrackHandVelocity()
    {
        Transform h = activeHand != null ? activeHand : rightHand;
        if (h == null) return;
        handVelocity = (h.position - prevHandPos) / Time.deltaTime;
        prevHandPos = h.position;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, grabRadius);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, drinkRange);
        Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, drinkRangeWhenTilted);
    }

    void OnGUI()
    {
        if (!showDrinkingHUD || !isDrinking) return;

        // Draw a centred progress bar near the bottom of the screen.
        float w = 360f, h = 22f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - 120f;

        // Background
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x - 4, y - 4, w + 8, h + 8), Texture2D.whiteTexture);
        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        // Fill
        float p = DrinkProgress;
        GUI.color = Color.Lerp(new Color(0.9f, 0.7f, 0.1f), new Color(0.2f, 1f, 0.3f), p);
        GUI.DrawTexture(new Rect(x, y, w * p, h), Texture2D.whiteTexture);

        // Label
        GUI.color = Color.white;
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 18;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y - 28, w, 24), drinkingLabel, style);
    }
}

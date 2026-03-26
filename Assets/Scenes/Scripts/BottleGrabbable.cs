using System.Collections;
using UnityEngine;

/// <summary>
/// FINAL VERSION — fixes:
///   1. rb.enabled = false while resting/flying/held — completely removes bottle
///      from PhysX so a moving car CANNOT push it.
///   2. Highlight targets ALL renderers on child objects (jager/default mesh).
///   3. Gaze detection works even when looking from driver seat angle.
///   4. No collisions needed while resting — bottle is pure transform.
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
    [Tooltip("Fine-tune local position from anchor. Usually 0,0,0 — just move the Anchor object.")]
    public Vector3 anchorPositionOffset = Vector3.zero;
    [Tooltip("Local rotation at rest.")]
    public Vector3 anchorRotationOffset = Vector3.zero;

    [Header("Gaze + Proximity")]
    [Tooltip("Right hand must be within this distance to trigger attract.")]
    public float grabRadius = 0.4f;
    [Tooltip("Degrees of gaze tolerance — how directly you need to look at the bottle.")]
    [Range(5f, 60f)]
    public float gazeAngleThreshold = 30f;
    [Tooltip("Seconds to fly to hand. 0 = instant.")]
    public float flyDuration = 0.5f;

    [Header("Drinking")]
    public float drinkRange = 0.3f;
    public float drinkDuration = 1.5f;
    public DrunkEffect drunkEffect;

    [Header("Throw")]
    public float throwThreshold = 1.5f;

    [Header("Highlight Materials")]
    [Tooltip("Material shown when gaze + hand proximity detected. If none, colour tint is used instead.")]
    public Material attractReadyMaterial;
    [Tooltip("Leave empty — original material stored automatically at Start.")]
    public Material originalMaterialOverride;

    // ------------------------------------------------------------------ //
    // Private
    // ------------------------------------------------------------------ //

    private enum State { Resting, Attracting, Held, Thrown }
    private State current = State.Resting;

    private Rigidbody rb;
    private Renderer[] allRenderers;    // captures jager AND default child
    private Material[] originalMaterials;

    private Transform rightHand;
    private Transform headAnchor;
    private Vector3 prevHandPos;
    private Vector3 handVelocity;

    private bool isDrinking;
    private float drinkTimer;
    private Coroutine flyCoroutine;

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

        // Find OVR anchors
        OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();
        if (rig != null) { rightHand = rig.rightHandAnchor; headAnchor = rig.centerEyeAnchor; }

        if (rightHand == null) Debug.LogWarning("[Bottle] RightHandAnchor not found.");
        if (headAnchor == null) Debug.LogWarning("[Bottle] CenterEyeAnchor not found.");

        SnapToAnchor();
    }

    void Update()
    {
        if (rightHand != null) TrackHandVelocity();

        switch (current)
        {
            case State.Resting: UpdateResting(); break;
            case State.Held: UpdateHeld(); break;
            case State.Attracting: break; // coroutine
            case State.Thrown: break; // physics
        }
    }

    // ------------------------------------------------------------------ //
    // RESTING
    // ------------------------------------------------------------------ //

    private void SnapToAnchor()
    {
        current = State.Resting;
        isDrinking = false;
        drinkTimer = 0f;

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
        Debug.Log("[Bottle] Resting in car.");
    }

    private void UpdateResting()
    {
        if (rightHand == null || headAnchor == null) return;

        bool handNear = Vector3.Distance(transform.position, rightHand.position) < grabRadius;
        bool lookingAt = IsLookingAtBottle();

        if (handNear && lookingAt)
        {
            ApplyHighlight();

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
                BeginAttract();
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
    // HIGHLIGHT — applies to ALL child renderers
    // ------------------------------------------------------------------ //

    private void ApplyHighlight()
    {
        if (attractReadyMaterial == null)
        {
            // No material assigned — tint all renderers yellow as fallback
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
        // detectCollisions stays false during flight — no physics needed

        if (flyCoroutine != null) StopCoroutine(flyCoroutine);
        flyCoroutine = StartCoroutine(FlyToHand());
    }

    private IEnumerator FlyToHand()
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        if (flyDuration <= 0f) { SnapToHand(); yield break; }

        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / flyDuration);
            transform.position = Vector3.Lerp(startPos, rightHand.position, t);
            transform.rotation = Quaternion.Slerp(startRot, rightHand.rotation, t);
            yield return null;
        }

        SnapToHand();
    }

    private void SnapToHand()
    {
        current = State.Held;
        transform.position = rightHand.position;
        transform.rotation = rightHand.rotation;
        prevHandPos = rightHand.position;
        Debug.Log("[Bottle] In hand.");
    }

    // ------------------------------------------------------------------ //
    // HELD
    // ------------------------------------------------------------------ //

    private void UpdateHeld()
    {
        transform.position = rightHand.position;
        transform.rotation = rightHand.rotation;

        HandleDrinking();

        if (OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
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
        bool nearFace = Vector3.Distance(transform.position, headAnchor.position) <= drinkRange;

        if (nearFace)
        {
            if (!isDrinking) { isDrinking = true; drinkTimer = 0f; }
            drinkTimer += Time.deltaTime;
            if (drinkTimer >= drinkDuration) { drinkTimer = 0f; TakeSip(); }
        }
        else if (isDrinking) { isDrinking = false; drinkTimer = 0f; }
    }

    private void TakeSip()
    {
        Debug.Log("[Bottle] Sip!");
        if (drunkEffect != null) drunkEffect.AddSip();
        else Debug.LogWarning("[Bottle] DrunkEffect not assigned!");
    }

    // ------------------------------------------------------------------ //
    // Helpers
    // ------------------------------------------------------------------ //

    private void TrackHandVelocity()
    {
        handVelocity = (rightHand.position - prevHandPos) / Time.deltaTime;
        prevHandPos = rightHand.position;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, grabRadius);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, drinkRange);
    }
}
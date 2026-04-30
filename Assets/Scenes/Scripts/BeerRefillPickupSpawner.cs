using System.Collections;
using UnityEngine;

/// <summary>
/// Periodically spawns a single BeerRefillPickup at a random spawn point.
/// When the player grabs the pickup it:
///   - calls RefillAll() on the assigned BeerBottleSpawner
///   - plays its pickup sound
///   - destroys itself
///   - and pings this spawner so the next pickup is scheduled.
///
/// Setup
///   1. Drop this script on an empty GameObject parented to your car (or anywhere
///      that follows the player � the spawn points are in this transform's local space).
///   2. Assign `pickupPrefab` to a prefab that has a BeerRefillPickup component
///      attached. (See "Auto-pickup-prefab fallback" below if you don't have one.)
///   3. Assign `bottleSpawner` to the BeerBottleSpawner that manages the seat bottles.
///   4. Add child Transforms (empty GameObjects) for each spawn point and drop them
///      in the `spawnPoints` array. If you leave the array empty, the pickup is
///      spawned at this transform's position.
///   5. Drop a pickup AudioClip into `pickupClip` (Assets/Sounds/...).
///
/// Auto-pickup-prefab fallback
///   If `pickupPrefab` is left empty, a simple sphere is generated at runtime as a
///   placeholder pickup so you can see the system working without a custom prefab.
/// </summary>
public class BeerRefillPickupSpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Prefab spawned as a pickup. Should have a BeerRefillPickup component. " +
             "If left empty, a placeholder sphere is generated at runtime.")]
    public GameObject pickupPrefab;

    [Tooltip("Bottle spawner whose RefillAll() the pickup will call. Auto-found if blank.")]
    public BeerBottleSpawner bottleSpawner;

    [Header("Spawn Points")]
    [Tooltip("Possible spawn locations. The pickup appears at one of these picked at random. " +
             "If empty, this transform is used.")]
    public Transform[] spawnPoints;

    [Tooltip("Local random offset (relative to the chosen spawn point) added so the pickup " +
             "doesn't always land in the same exact spot. Set to 0 for deterministic spawns.")]
    public Vector3 randomOffset = new Vector3(0.05f, 0.05f, 0.05f);

    [Header("Timing")]
    [Tooltip("Wait this long after the previous pickup is consumed (or after Start) before spawning the next. " +
             "X = min seconds, Y = max seconds. Picked uniformly at random.")]
    public Vector2 spawnDelayRange = new Vector2(8f, 16f);

    [Tooltip("Spawn the first pickup automatically when this object starts.")]
    public bool spawnOnStart = true;

    [Tooltip("If true, only spawn a new pickup when at least one bottle has actually been " +
             "consumed (sips taken). Avoids spamming pickups when the player isn't drinking.")]
    public bool requireBottleUsed = false;

    [Header("Audio (forwarded to pickup)")]
    [Tooltip("Pickup AudioClip the spawned pickups will play when grabbed. Drop your .wav/.mp3 from Assets/Sounds.")]
    public AudioClip pickupClip;
    [Range(0f, 1f)] public float pickupVolume = 1f;

    [Header("Pickup Tuning (forwarded)")]
    [Tooltip("If true, the pickup is consumed on hand-touch (no button press). Comfort option for VR.")]
    public bool grabOnTouch = false;
    [Tooltip("Hand proximity radius for grabbing the pickup.")]
    public float grabRadius = 0.4f;

    [Header("Placeholder Pickup (used if pickupPrefab is empty)")]
    [Tooltip("Diameter of the auto-generated placeholder sphere.")]
    public float placeholderDiameter = 0.12f;
    [Tooltip("Tint of the auto-generated placeholder sphere.")]
    public Color placeholderColour = new Color(1f, 0.82f, 0.2f);

    [Header("Debug")]
    public bool drawGizmos = true;

    // ------------------------------------------------------------------ //
    // Runtime state
    // ------------------------------------------------------------------ //

    private GameObject currentPickup;
    private Coroutine spawnRoutine;

    void Start()
    {
        if (bottleSpawner == null)
            bottleSpawner = Object.FindFirstObjectByType<BeerBottleSpawner>();

        if (spawnOnStart) ScheduleNext();
    }

    /// <summary>Called by BeerRefillPickup.Consume() so we know to schedule the next one.</summary>
    public void NotifyPickupConsumed()
    {
        currentPickup = null;
        ScheduleNext();
    }

    /// <summary>Manually trigger a spawn after the configured random delay.</summary>
    public void ScheduleNext()
    {
        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(ScheduleNextRoutine());
    }

    private IEnumerator ScheduleNextRoutine()
    {
        // Wait the configured random delay.
        float wait = Random.Range(
            Mathf.Min(spawnDelayRange.x, spawnDelayRange.y),
            Mathf.Max(spawnDelayRange.x, spawnDelayRange.y));
        yield return new WaitForSeconds(wait);

        // Optionally only spawn once at least one bottle has been used.
        if (requireBottleUsed && bottleSpawner != null)
        {
            while (!AnyBottleUsed())
                yield return new WaitForSeconds(1f);
        }

        SpawnNow();
    }

    /// <summary>Spawn a pickup immediately (skipping the wait).</summary>
    public void SpawnNow()
    {
        if (currentPickup != null) return; // one at a time

        // Pick a spawn point.
        Transform anchor = (spawnPoints != null && spawnPoints.Length > 0)
            ? spawnPoints[Random.Range(0, spawnPoints.Length)]
            : transform;

        if (anchor == null) anchor = transform;

        Vector3 jitter = new Vector3(
            Random.Range(-randomOffset.x, randomOffset.x),
            Random.Range(-randomOffset.y, randomOffset.y),
            Random.Range(-randomOffset.z, randomOffset.z));
        Vector3 spawnPos = anchor.TransformPoint(jitter);

        GameObject go;
        if (pickupPrefab != null)
        {
            go = Instantiate(pickupPrefab, spawnPos, Quaternion.identity, anchor);
        }
        else
        {
            go = BuildPlaceholderPickup(spawnPos, anchor);
        }
        go.name = "BeerRefillPickup";

        // Wire up the BeerRefillPickup component.
        BeerRefillPickup pickup = go.GetComponent<BeerRefillPickup>();
        if (pickup == null) pickup = go.AddComponent<BeerRefillPickup>();

        pickup.spawner = bottleSpawner;
        pickup.pickupSpawner = this;
        if (pickupClip != null) pickup.pickupClip = pickupClip;
        pickup.pickupVolume = pickupVolume;
        pickup.grabOnTouch = grabOnTouch;
        pickup.grabRadius = grabRadius;

        currentPickup = go;
        Debug.Log($"[BeerRefillPickupSpawner] Spawned pickup at '{anchor.name}' (delay used).");
    }

    private bool AnyBottleUsed()
    {
        if (bottleSpawner == null) return true; // can't tell, just allow
        var bottles = bottleSpawner.SpawnedBottles;
        if (bottles == null) return true;
        foreach (var b in bottles)
        {
            if (b == null) return true;            // missing -> definitely needs refill
            var bg = b.GetComponent<BottleGrabbable>();
            if (bg == null) bg = b.GetComponentInChildren<BottleGrabbable>();
            if (bg != null && bg.SipsTaken > 0) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ //
    // Placeholder pickup (a glowing sphere) so the system works without an art prefab.
    // ------------------------------------------------------------------ //

    private GameObject BuildPlaceholderPickup(Vector3 worldPos, Transform parent)
    {
        GameObject root = new GameObject("BeerRefillPickup");
        root.transform.SetParent(parent, worldPositionStays: false);
        root.transform.position = worldPos;

        // Visual sphere child (so we can disable its collider on the parent rigidbody).
        GameObject vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        vis.name = "Mesh";
        vis.transform.SetParent(root.transform, worldPositionStays: false);
        vis.transform.localScale = Vector3.one * placeholderDiameter;
        // Drop the auto-added collider � we don't need physics for the visual.
        var col = vis.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var rend = vis.GetComponent<Renderer>();
        if (rend != null)
        {
            // Try Standard first (Built-in RP), fall back to URP/Lit, then default.
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.color = placeholderColour;
            // Best-effort emission so the placeholder glows even without a Light.
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", placeholderColour * 1.5f);
            }
            rend.material = mat;
        }

        // Glow point light child.
        GameObject lightGO = new GameObject("Glow");
        lightGO.transform.SetParent(root.transform, worldPositionStays: false);
        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 1.5f;
        light.intensity = 1f;
        light.color = placeholderColour;

        // Rigidbody (BeerRefillPickup [RequireComponent] expects one).
        var rb = root.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.detectCollisions = false;

        // Add the pickup behaviour and wire its glow light.
        var pickup = root.AddComponent<BeerRefillPickup>();
        pickup.glowLight = light;

        return root;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        Gizmos.color = new Color(1f, 0.82f, 0.2f, 0.9f);

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            foreach (var p in spawnPoints)
            {
                if (p == null) continue;
                Gizmos.DrawWireSphere(p.position, 0.08f);
                Gizmos.DrawLine(p.position, p.position + Vector3.up * 0.2f);
            }
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, 0.08f);
        }
    }
}

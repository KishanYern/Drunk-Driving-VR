using UnityEngine;

/// <summary>
/// Spawns N beer bottles next to the driver seat. Each bottle gets its own
/// anchor (so it returns to its own slot after release) and is wired up to
/// the shared DrunkEffect.
///
/// Place this script anywhere on the car (e.g. on Sedan1 or on a child
/// "Beer Tray" transform). Assign:
///   - bottlePrefab : the jager.prefab in Prefabs/BeerBottles
///   - drunkEffect  : the DrunkEffect on the OVRCameraRig
///   - layoutOrigin : (optional) transform that defines where the row of
///                    bottles starts. If left blank, this transform is used.
///
/// The spawner lays the bottles out in a row (configurable) so the driver can
/// reach for them without leaving the seat. Each bottle uses the existing
/// BottleGrabbable behaviour: hover near it, press grip/trigger, the bottle
/// flies to your hand. Bring it to your face to drink and increase impairment.
/// </summary>
public class BeerBottleSpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Prefab to spawn. Should already have a BottleGrabbable component (e.g. jager.prefab).")]
    public GameObject bottlePrefab;

    [Tooltip("DrunkEffect each bottle reports sips to. If left blank, the spawner will try to find one in the scene.")]
    public DrunkEffect drunkEffect;

    [Tooltip("Where the row of bottles is centred. Leave blank to use this transform's position.")]
    public Transform layoutOrigin;

    [Tooltip("Optional parent for the spawned bottles + anchors (keeps the hierarchy tidy). Defaults to layoutOrigin.")]
    public Transform spawnParent;

    [Header("Layout")]
    [Tooltip("How many bottles to spawn.")]
    [Range(1, 12)]
    public int bottleCount = 6;

    [Tooltip("Spacing between bottles along the row (metres).")]
    public float spacing = 0.12f;

    [Tooltip("Local offset applied to the whole row (relative to layoutOrigin). " +
             "Use this to nudge the tray so it sits next to the driver seat.")]
    public Vector3 rowOffset = new Vector3(0.35f, 0f, 0.05f);

    [Tooltip("Direction the row extends along, in layoutOrigin's local space. " +
             "Default = +Z (forward). Use Vector3.right for a side-to-side row.")]
    public Vector3 rowDirection = Vector3.forward;

    [Tooltip("Local rotation applied to each spawned bottle (Euler angles).")]
    public Vector3 bottleRotation = Vector3.zero;

    [Tooltip("Scale multiplier for each spawned bottle (applied on top of the prefab's own scale).")]
    public float bottleScale = 1f;

    [Header("Per-Bottle Tuning")]
    [Tooltip("Override the BottleGrabbable.grabRadius on every spawned bottle. <=0 keeps the prefab value.")]
    public float overrideGrabRadius = 0.5f;

    [Tooltip("Override BottleGrabbable.flyDuration on every spawned bottle. <0 keeps the prefab value.")]
    public float overrideFlyDuration = 0.35f;

    [Tooltip("Override BottleGrabbable.drinkRange on every spawned bottle. <=0 keeps the prefab value.")]
    public float overrideDrinkRange = 0.55f;

    [Tooltip("Override BottleGrabbable.drinkRangeWhenTilted on every spawned bottle. <=0 keeps the prefab value.")]
    public float overrideDrinkRangeWhenTilted = 0.85f;

    [Tooltip("Override BottleGrabbable.drinkDuration on every spawned bottle. <=0 keeps the prefab value.")]
    public float overrideDrinkDuration = 1.0f;

    [Tooltip("Force gaze requirement off so bottles are easy to grab.")]
    public bool forceGazeOff = true;

    [Tooltip("Force grip-button-also-grabs on.")]
    public bool forceAcceptGrip = true;

    [Tooltip("Force either-hand grabbing on.")]
    public bool forceEitherHand = true;

    [Header("Audio (applied to every bottle)")]
    [Tooltip("Looped sip/glug sound played while drinking. Drag your own clip in here.")]
    public AudioClip drinkLoopClip;
    [Tooltip("One-shot sound played when each sip lands.")]
    public AudioClip sipCompleteClip;
    [Tooltip("One-shot sound played when a bottle is refilled / returned to its slot.")]
    public AudioClip refillClip;
    [Range(0f, 1f)] public float drinkVolume = 0.8f;
    [Range(0f, 1f)] public float refillVolume = 1f;

    [Header("Visual (applied to every bottle)")]
    [Tooltip("Material used while the player is actively drinking. Optional � falls back to the prefab's attractReadyMaterial then to a green tint.")]
    public Material drinkingMaterial;
    [Tooltip("Show the on-screen drinking progress bar.")]
    public bool showDrinkingHUD = true;

    [Header("Debug")]
    public bool spawnOnStart = true;
    public bool drawGizmos = true;

    private GameObject[] spawned;

    void Start()
    {
        if (spawnOnStart) Spawn();
    }

    /// <summary>
    /// Spawn the bottles. Safe to call again � it will clear and respawn.
    /// </summary>
    public void Spawn()
    {
        if (bottlePrefab == null)
        {
            Debug.LogError("[BeerBottleSpawner] bottlePrefab is not assigned.");
            return;
        }

        // Resolve references
        Transform origin = layoutOrigin != null ? layoutOrigin : transform;
        Transform parent = spawnParent != null ? spawnParent : origin;

        if (drunkEffect == null)
        {
            drunkEffect = Object.FindFirstObjectByType<DrunkEffect>();
            if (drunkEffect == null)
                Debug.LogWarning("[BeerBottleSpawner] No DrunkEffect found in the scene � sips won't register until one is assigned.");
        }

        ClearSpawned();
        spawned = new GameObject[bottleCount];

        Vector3 dirLocal = rowDirection.sqrMagnitude < 0.0001f ? Vector3.forward : rowDirection.normalized;
        // Centre the row around rowOffset
        float halfRow = (bottleCount - 1) * 0.5f;

        for (int i = 0; i < bottleCount; i++)
        {
            // ---- Per-bottle anchor (so each bottle has its own resting slot) ----
            GameObject anchorGO = new GameObject($"BeerAnchor_{i}");
            Transform anchor = anchorGO.transform;
            anchor.SetParent(parent, worldPositionStays: false);

            Vector3 localPos = rowOffset + dirLocal * (spacing * (i - halfRow));
            anchor.localPosition = localPos;
            anchor.localRotation = Quaternion.Euler(bottleRotation);

            // ---- Spawn the bottle as a child of the anchor ----
            GameObject bottle = Instantiate(bottlePrefab, anchor);
            bottle.name = $"Beer_{i}";
            bottle.transform.localPosition = Vector3.zero;
            bottle.transform.localRotation = Quaternion.identity;
            bottle.transform.localScale = bottle.transform.localScale * bottleScale;

            // ---- Wire the BottleGrabbable ----
            BottleGrabbable bg = bottle.GetComponent<BottleGrabbable>();
            if (bg == null) bg = bottle.GetComponentInChildren<BottleGrabbable>();
            if (bg == null)
            {
                Debug.LogWarning($"[BeerBottleSpawner] Spawned bottle '{bottle.name}' has no BottleGrabbable component.");
            }
            else
            {
                bg.bottleAnchor = anchor;
                bg.anchorPositionOffset = Vector3.zero;
                bg.anchorRotationOffset = Vector3.zero;
                bg.drunkEffect = drunkEffect;

                if (overrideGrabRadius > 0f) bg.grabRadius = overrideGrabRadius;
                if (overrideFlyDuration >= 0f) bg.flyDuration = overrideFlyDuration;
                if (overrideDrinkRange > 0f) bg.drinkRange = overrideDrinkRange;
                if (overrideDrinkRangeWhenTilted > 0f) bg.drinkRangeWhenTilted = overrideDrinkRangeWhenTilted;
                if (overrideDrinkDuration > 0f) bg.drinkDuration = overrideDrinkDuration;
                if (forceGazeOff)    bg.requireGaze = false;
                if (forceAcceptGrip) bg.acceptGripButton = true;
                if (forceEitherHand) bg.allowEitherHand = true;

                // Audio + visual (only assigned if the spawner has a value, so prefab
                // defaults aren't blown away when a slot is left empty).
                if (drinkLoopClip != null)    bg.drinkLoopClip = drinkLoopClip;
                if (sipCompleteClip != null)  bg.sipCompleteClip = sipCompleteClip;
                if (refillClip != null)       bg.refillClip = refillClip;
                bg.drinkVolume = drinkVolume;
                bg.refillVolume = refillVolume;
                if (drinkingMaterial != null) bg.drinkingMaterial = drinkingMaterial;
                bg.showDrinkingHUD = showDrinkingHUD;
            }

            spawned[i] = bottle;
        }

        Debug.Log($"[BeerBottleSpawner] Spawned {bottleCount} beer bottles next to the driver seat.");
    }

    /// <summary>Read-only accessor � the bottle GameObjects produced by the last Spawn().
    /// Slots may be null if a bottle has been destroyed elsewhere.</summary>
    public GameObject[] SpawnedBottles => spawned;

    /// <summary>How many bottle slots this spawner manages.</summary>
    public int SpawnedCount => spawned != null ? spawned.Length : 0;

    /// <summary>
    /// Refill / respawn every bottle managed by this spawner. Bottles that
    /// still exist are reset back to their anchor (with sip count cleared and
    /// the refill SFX played). Bottles that were destroyed (e.g. a thrown
    /// bottle that hit a deletion trigger) are re-instantiated into their slot.
    /// </summary>
    /// <returns>The number of bottles that were refilled or respawned.</returns>
    public int RefillAll()
    {
        if (spawned == null || spawned.Length == 0)
        {
            Debug.LogWarning("[BeerBottleSpawner] RefillAll called before Spawn � nothing to refill.");
            return 0;
        }

        int touched = 0;

        for (int i = 0; i < spawned.Length; i++)
        {
            GameObject bottle = spawned[i];

            if (bottle == null)
            {
                // Bottle was destroyed � rebuild it in its anchor (the anchor is
                // its old parent, which may also have been destroyed if the whole
                // hierarchy was nuked; we just skip in that case).
                bottle = RespawnBottleAt(i);
                if (bottle != null) touched++;
                continue;
            }

            BottleGrabbable bg = bottle.GetComponent<BottleGrabbable>();
            if (bg == null) bg = bottle.GetComponentInChildren<BottleGrabbable>();
            if (bg != null)
            {
                bg.Refill();
                touched++;
            }
        }

        Debug.Log($"[BeerBottleSpawner] RefillAll refilled/respawned {touched} bottle(s).");
        return touched;
    }

    /// <summary>
    /// Re-instantiate the bottle at slot <paramref name="index"/> if it has been
    /// destroyed. Returns the new GameObject (or null if the anchor is gone too).
    /// </summary>
    private GameObject RespawnBottleAt(int index)
    {
        if (bottlePrefab == null) return null;
        if (index < 0 || index >= spawned.Length) return null;

        // The anchor for this slot is named "BeerAnchor_{index}" and lives under
        // the spawn parent. Find it again.
        Transform parent = spawnParent != null ? spawnParent : (layoutOrigin != null ? layoutOrigin : transform);
        Transform anchor = null;
        foreach (Transform child in parent)
        {
            if (child != null && child.name == $"BeerAnchor_{index}") { anchor = child; break; }
        }
        if (anchor == null)
        {
            Debug.LogWarning($"[BeerBottleSpawner] Anchor for slot {index} is missing � cannot respawn.");
            return null;
        }

        GameObject bottle = Instantiate(bottlePrefab, anchor);
        bottle.name = $"Beer_{index}";
        bottle.transform.localPosition = Vector3.zero;
        bottle.transform.localRotation = Quaternion.identity;
        bottle.transform.localScale = bottle.transform.localScale * bottleScale;

        BottleGrabbable bg = bottle.GetComponent<BottleGrabbable>();
        if (bg == null) bg = bottle.GetComponentInChildren<BottleGrabbable>();
        if (bg != null)
        {
            bg.bottleAnchor = anchor;
            bg.anchorPositionOffset = Vector3.zero;
            bg.anchorRotationOffset = Vector3.zero;
            bg.drunkEffect = drunkEffect;

            if (overrideGrabRadius > 0f) bg.grabRadius = overrideGrabRadius;
            if (overrideFlyDuration >= 0f) bg.flyDuration = overrideFlyDuration;
            if (overrideDrinkRange > 0f) bg.drinkRange = overrideDrinkRange;
            if (overrideDrinkRangeWhenTilted > 0f) bg.drinkRangeWhenTilted = overrideDrinkRangeWhenTilted;
            if (overrideDrinkDuration > 0f) bg.drinkDuration = overrideDrinkDuration;
            if (forceGazeOff)    bg.requireGaze = false;
            if (forceAcceptGrip) bg.acceptGripButton = true;
            if (forceEitherHand) bg.allowEitherHand = true;

            if (drinkLoopClip != null)    bg.drinkLoopClip = drinkLoopClip;
            if (sipCompleteClip != null)  bg.sipCompleteClip = sipCompleteClip;
            if (refillClip != null)       bg.refillClip = refillClip;
            bg.drinkVolume = drinkVolume;
            bg.refillVolume = refillVolume;
            if (drinkingMaterial != null) bg.drinkingMaterial = drinkingMaterial;
            bg.showDrinkingHUD = showDrinkingHUD;

            // Play the refill SFX on the freshly spawned bottle too.
            if (bg.audioSource == null) bg.audioSource = bg.GetComponent<AudioSource>();
            if (bg.audioSource != null && refillClip != null)
                bg.audioSource.PlayOneShot(refillClip, refillVolume);
        }

        spawned[index] = bottle;
        return bottle;
    }

    /// <summary>Destroy any previously spawned bottles + anchors.</summary>
    public void ClearSpawned()
    {
        if (spawned == null) return;
        foreach (var go in spawned)
        {
            if (go == null) continue;
            // Destroy the anchor (parent), which cleans up the bottle too.
            Transform p = go.transform.parent;
            if (p != null && p.name.StartsWith("BeerAnchor_"))
                Destroy(p.gameObject);
            else
                Destroy(go);
        }
        spawned = null;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Transform origin = layoutOrigin != null ? layoutOrigin : transform;
        Vector3 dirLocal = rowDirection.sqrMagnitude < 0.0001f ? Vector3.forward : rowDirection.normalized;
        float halfRow = (bottleCount - 1) * 0.5f;

        Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.9f);
        for (int i = 0; i < bottleCount; i++)
        {
            Vector3 localPos = rowOffset + dirLocal * (spacing * (i - halfRow));
            Vector3 worldPos = origin.TransformPoint(localPos);
            Gizmos.DrawWireSphere(worldPos, 0.05f);
            Gizmos.DrawLine(worldPos, worldPos + Vector3.up * 0.18f);
        }
    }
}

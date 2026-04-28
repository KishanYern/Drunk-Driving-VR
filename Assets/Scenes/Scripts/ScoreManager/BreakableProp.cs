using UnityEngine;

/// <summary>
/// Attach to any single-mesh prop (lamp post, traffic light, etc.)
/// On car impact:
///   1. Hides the original mesh
///   2. Spawns debris prefabs and blasts them outward
///   3. Calls PropHitDetector to register the score
///
/// HOW TO SET UP DEBRIS PREFABS — see bottom of this file.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BreakableProp : MonoBehaviour
{
    [Header("Car Detection")]
    public string carTag = "PlayerCar";
    [Tooltip("Minimum car speed (m/s) to trigger break. Slow nudges won't break it.")]
    public float minImpactSpeed = 3f;

    [Header("Debris")]
    [Tooltip("Drag in 3-6 debris prefabs (chunks of the prop). See setup guide below.")]
    public GameObject[] debrisPrefabs;

    [Tooltip("How many debris pieces to spawn total.")]
    [Range(3, 12)]
    public int debrisCount = 5;

    [Tooltip("How violently pieces fly outward.")]
    public float explosionForce = 400f;

    [Tooltip("Upward bias on the explosion so pieces fly up and out, not just sideways.")]
    public float explosionUpward = 1.5f;

    [Tooltip("Radius of the explosion force sphere.")]
    public float explosionRadius = 2f;

    [Tooltip("Seconds before debris pieces are destroyed (keeps scene clean).")]
    public float debrisLifetime = 4f;

    [Header("Effects")]
    [Tooltip("Optional particle effect prefab to spawn at break point (sparks, dust, etc.)")]
    public GameObject breakParticlePrefab;

    [Tooltip("Optional audio clip to play on break.")]
    public AudioClip breakSound;

    // ------------------------------------------------------------------ //

    private bool hasBroken = false;
    private Renderer propRenderer;
    private Collider propCollider;

    void Start()
    {
        propRenderer = GetComponent<Renderer>();
        propCollider = GetComponent<Collider>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasBroken) return;
        if (!collision.gameObject.CompareTag(carTag)) return;
        if (collision.relativeVelocity.magnitude < minImpactSpeed) return;

        hasBroken = true;
        Break(collision);
    }

    private void Break(Collision collision)
    {
        // --- Score ---
        // PropHitDetector handles scoring if it's on this GameObject.
        // If not, call directly:
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.RegisterPropHit();

        Vector3 impactPoint = collision.contacts[0].point;

        if (debrisPrefabs != null && debrisPrefabs.Length > 0)
        {
            // --- Hide original mesh + disable collider ---
            if (propRenderer != null) propRenderer.enabled = false;
            if (propCollider != null) propCollider.enabled = false;

            // --- Spawn debris ---
            SpawnDebris(impactPoint, collision.relativeVelocity);
        }
        else
        {
            // --- Make the pole ITSELF fly away (Fallback) ---
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

            MeshCollider[] meshColliders = GetComponentsInChildren<MeshCollider>();
            foreach (MeshCollider mc in meshColliders) mc.convex = true;

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;
            
            // Give the full pole a realistic mass so it doesn't fly like a bullet
            rb.mass = 150f; 
            rb.linearDamping = 2f; // Heavy air resistance so it drops quickly
            rb.angularDamping = 2f; // Stops it from spinning endlessly

            // Clamp the impact speed so super-fast crashes don't launch it miles away
            Vector3 clampedVelocity = Vector3.ClampMagnitude(collision.relativeVelocity, 15f);

            // Transfer only 10% of the clamped speed, plus a small upward pop
            Vector3 pushForce = clampedVelocity * rb.mass * 0.10f;
            pushForce.y += 300f; // Upward pop scaled for the heavier 150kg mass

            rb.AddForce(pushForce, ForceMode.Impulse);
            
            // Add a smaller tumble since it's much heavier
            rb.AddTorque(Random.insideUnitSphere * 50f, ForceMode.Impulse);
        }

        // --- Effects ---
        if (breakParticlePrefab != null)
        {
            GameObject fx = Instantiate(breakParticlePrefab, impactPoint, Quaternion.identity);
            Destroy(fx, 3f);
        }

        if (breakSound != null)
            AudioSource.PlayClipAtPoint(breakSound, impactPoint);

        // --- Destroy the object after some time to keep the scene clean ---
        Destroy(gameObject, debrisLifetime + 0.5f);
    }

    private void SpawnDebris(Vector3 impactPoint, Vector3 carVelocity)
    {
        if (debrisPrefabs == null || debrisPrefabs.Length == 0)
        {
            Debug.LogWarning($"[BreakableProp] No debris prefabs assigned on {gameObject.name}!");
            return;
        }

        for (int i = 0; i < debrisCount; i++)
        {
            // Pick a random debris prefab
            GameObject prefab = debrisPrefabs[Random.Range(0, debrisPrefabs.Length)];
            if (prefab == null) continue;

            // Scatter spawn position slightly around the impact point
            Vector3 randomOffset = Random.insideUnitSphere * 0.3f;
            Vector3 spawnPos     = impactPoint + randomOffset;

            // Random rotation so pieces tumble differently
            Quaternion randomRot = Random.rotation;

            GameObject piece = Instantiate(prefab, spawnPos, randomRot);

            // Ensure any MeshColliders on the debris are convex (required for dynamic rigidbodies)
            MeshCollider[] meshColliders = piece.GetComponentsInChildren<MeshCollider>();
            foreach (MeshCollider mc in meshColliders)
            {
                mc.convex = true;
            }

            // Make sure the piece has a Rigidbody for physics
            Rigidbody rb = piece.GetComponent<Rigidbody>();
            if (rb == null) rb = piece.AddComponent<Rigidbody>();

            rb.mass = Random.Range(0.3f, 1.2f);   // varied mass = varied flight

            // Explosion force from the impact point
            rb.AddExplosionForce(
                explosionForce,
                impactPoint,
                explosionRadius,
                explosionUpward,
                ForceMode.Impulse
            );

            // Add a bit of the car's own momentum so pieces fly in the direction you hit
            rb.AddForce(carVelocity * 0.5f, ForceMode.Impulse);

            // Random spin
            rb.angularVelocity = Random.insideUnitSphere * 8f;

            // Auto-destroy after lifetime
            Destroy(piece, debrisLifetime);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}


// ==========================================================================
// HOW TO CREATE DEBRIS PREFABS (no 3D modelling needed)
// ==========================================================================
//
// OPTION A — Quick primitives (fastest, looks decent at speed):
//   1. Right-click Hierarchy > 3D Object > Cube  (rename "Debris_Chunk")
//   2. Scale it to roughly 1/4 the size of your lamp post, e.g. (0.1, 0.4, 0.1)
//   3. Add Component > Rigidbody
//   4. Add Component > Box Collider (auto-added with cube)
//   5. Apply the same material as your lamp post so it looks like a piece of it
//   6. Drag into Project > Prefabs folder  -->  delete from scene
//   7. Make 2-3 variations with different scales/shapes (thin sliver, chunk, top piece)
//   8. Assign all of them to the Debris Prefabs array on BreakableProp
//
// OPTION B — Mesh slices (best look, needs free tool):
//   1. Install "Destructible 2D" or "Fracture" from Asset Store (many are free)
//   OR
//   1. In Blender: import your mesh, use "Cell Fracture" add-on to split it
//   2. Export each piece as its own FBX, import into Unity
//   3. Add Rigidbody + MeshCollider (Convex = true) to each
//   4. Save as prefabs, assign to array
//
// TIP: Use 3-4 different debris prefabs so no two explosions look identical.
// ==========================================================================

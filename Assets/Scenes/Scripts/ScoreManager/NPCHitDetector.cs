using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Attach this to every NPC prefab alongside SimpleNPCMovement.
/// Detects a collision with the player's car, reports the hit to ScoreManager,
/// then disables the NPC (ragdoll-ready: swap Disable() for ragdoll logic later).
/// </summary>
[RequireComponent(typeof(Collider))]
public class NPCHitDetector : MonoBehaviour
{
    [Header("Hit Settings")]
    [Tooltip("Tag on the player's car GameObject.")]
    public string carTag = "PlayerCar";

    [Tooltip("Minimum impact speed (m/s) required to count as a hit. Prevents slow nudges scoring.")]
    public float minImpactSpeed = 3f;

    [Tooltip("Seconds before this NPC is destroyed after being hit (time for effects/ragdoll).")]
    public float destroyDelay = 2f;

    private bool hasBeenHit = false;    // prevent double-scoring if collider fires twice

    // ------------------------------------------------------------------ //

    private void OnCollisionEnter(Collision collision)
    {
        if (hasBeenHit) return;
        if (!collision.gameObject.CompareTag(carTag)) return;

        // Check impact speed so slowly rolling into an NPC doesn't trigger
        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < minImpactSpeed) return;

        hasBeenHit = true;

        // Tell the score manager
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.RegisterHit();
        else
            Debug.LogWarning("NPCHitDetector: No ScoreManager found in scene.");

        // Stop the NPC wandering
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        // Optional: swap SetActive(false) for ragdoll/death animation here
        Disable();
    }

    private void Disable()
    {
        // Disable movement and this script
        SimpleNPCMovement movement = GetComponent<SimpleNPCMovement>();
        if (movement != null) movement.enabled = false;

        // Destroy after a short delay (gives time for particle effects etc.)
        Destroy(gameObject, destroyDelay);
    }
}

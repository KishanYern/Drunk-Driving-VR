using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Attach to every NPC prefab.
/// Calls ScoreManager.RegisterNPCHit() — counts toward combo multiplier.
/// </summary>
[RequireComponent(typeof(Collider))]
public class NPCHitDetector : MonoBehaviour
{
    [Header("Hit Settings")]
    public string carTag = "PlayerCar";
    public float minImpactSpeed = 3f;
    public float destroyDelay = 2f;

    private bool hasBeenHit = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (hasBeenHit) return;
        if (!collision.gameObject.CompareTag(carTag)) return;
        if (collision.relativeVelocity.magnitude < minImpactSpeed) return;

        hasBeenHit = true;

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.RegisterNPCHit();

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        SimpleNPCMovement movement = GetComponent<SimpleNPCMovement>();
        if (movement != null) movement.enabled = false;

        Destroy(gameObject, destroyDelay);
    }
}
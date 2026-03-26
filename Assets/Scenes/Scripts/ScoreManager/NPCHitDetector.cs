using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Collider))]
public class NPCHitDetector : MonoBehaviour
{
    [Header("Hit Settings")]
    public string carTag = "PlayerCar";
    public float minImpactSpeed = 3f;
    public float destroyDelay = 2f;

    [Header("Sounds")]
    [Tooltip("Drag in 2-3 different scream clips for variety")]
    public AudioClip[] screamClips;
    [Tooltip("Body/thud impact sound")]
    public AudioClip impactClip;

    private bool hasBeenHit = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (hasBeenHit) return;
        if (!collision.gameObject.CompareTag(carTag)) return;
        if (collision.relativeVelocity.magnitude < minImpactSpeed) return;

        hasBeenHit = true;

        // Play a random scream at the NPC's world position
        if (screamClips != null && screamClips.Length > 0)
        {
            AudioClip scream = screamClips[Random.Range(0, screamClips.Length)];
            AudioSource.PlayClipAtPoint(scream, transform.position, 2f);
        }

        if (impactClip != null)
            AudioSource.PlayClipAtPoint(impactClip, collision.contacts[0].point, 0.8f);

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.RegisterNPCHit();

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        SimpleNPCMovement movement = GetComponent<SimpleNPCMovement>();
        if (movement != null) movement.enabled = false;

        Destroy(gameObject, destroyDelay);
    }
}
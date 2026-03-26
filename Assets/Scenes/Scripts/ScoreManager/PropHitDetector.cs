using UnityEngine;

/// <summary>
/// Attach to props like lamp posts, traffic lights, bins, cones etc.
/// Gives +10 flat points. Does NOT affect the combo multiplier.
/// Tag the prop's GameObject with "Prop" (optional — the script uses layer/tag set below).
/// </summary>
[RequireComponent(typeof(Collider))]
public class PropHitDetector : MonoBehaviour
{
    [Header("Hit Settings")]
    public string carTag        = "PlayerCar";
    public float minImpactSpeed = 1f;   // lower than NPC — even a slow nudge should score

    [Tooltip("Can this prop be hit multiple times (e.g. a cone that rolls), or only once (e.g. a fixed post)?")]
    public bool oneHitOnly = true;

    private bool hasBeenHit = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (oneHitOnly && hasBeenHit) return;
        if (!collision.gameObject.CompareTag(carTag)) return;
        if (collision.relativeVelocity.magnitude < minImpactSpeed) return;

        hasBeenHit = true;

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.RegisterPropHit();
        else
            Debug.LogWarning("PropHitDetector: No ScoreManager in scene.");
    }
}

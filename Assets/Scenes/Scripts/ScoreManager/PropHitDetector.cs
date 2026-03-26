using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PropHitDetector : MonoBehaviour
{
    [Header("Hit Settings")]
    public string carTag = "PlayerCar";
    public float minImpactSpeed = 1f;
    public bool oneHitOnly = true;

    [Header("Sounds")]
    [Tooltip("Metal crunch / snap sound for prop hits")]
    public AudioClip[] propImpactClips;

    private bool hasBeenHit = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (oneHitOnly && hasBeenHit) return;
        if (!collision.gameObject.CompareTag(carTag)) return;
        if (collision.relativeVelocity.magnitude < minImpactSpeed) return;

        hasBeenHit = true;

        if (propImpactClips != null && propImpactClips.Length > 0)
        {
            AudioClip clip = propImpactClips[Random.Range(0, propImpactClips.Length)];
            float vol = Mathf.Clamp01(collision.relativeVelocity.magnitude / 15f); // louder = faster hit
            AudioSource.PlayClipAtPoint(clip, collision.contacts[0].point, vol);
        }

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.RegisterPropHit();
        else
            Debug.LogWarning("PropHitDetector: No ScoreManager in scene.");
    }
}
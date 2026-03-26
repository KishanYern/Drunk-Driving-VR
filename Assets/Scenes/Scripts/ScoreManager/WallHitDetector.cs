using UnityEngine;

/// <summary>
/// Attach to walls, buildings, kerbs, barriers — anything that should
/// penalise the player and break their combo.
///
/// Uses a cooldown so a sustained grind against a wall doesn't spam -50 every frame.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WallHitDetector : MonoBehaviour
{
    [Header("Hit Settings")]
    public string carTag        = "PlayerCar";

    [Tooltip("Minimum speed (m/s) for the collision to count. Prevents penalty from gently touching a wall.")]
    public float minImpactSpeed = 2f;

    [Tooltip("Seconds before this wall can penalise the player again. Prevents repeated -50 while scraping.")]
    public float hitCooldown    = 2f;

    private float cooldownTimer = 0f;

    void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (cooldownTimer > 0f) return;
        if (!collision.gameObject.CompareTag(carTag)) return;
        if (collision.relativeVelocity.magnitude < minImpactSpeed) return;

        cooldownTimer = hitCooldown;

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.RegisterWallHit();
        else
            Debug.LogWarning("WallHitDetector: No ScoreManager in scene.");
    }
}

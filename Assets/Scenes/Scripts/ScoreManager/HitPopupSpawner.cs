using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Spawns a floating "+100 x3" text popup at the hit position.
/// Place one instance anywhere in the scene and assign a TMP prefab.
///
/// HOW TO SET UP THE POPUP PREFAB:
///   1. Create a Canvas (World Space, small scale ~0.01)
///   2. Add a TMP_Text child — centre-aligned, bold, large font
///   3. Save as a prefab and assign to popupPrefab below
/// </summary>
public class HitPopupSpawner : MonoBehaviour
{
    public static HitPopupSpawner Instance { get; private set; }

    [Header("Popup Settings")]
    public GameObject popupPrefab;              // World-space Canvas with TMP_Text
    public float floatSpeed = 1.5f;             // Units per second the text rises
    public float popupLifetime = 1.2f;          // Seconds before it fades out
    public Color singleHitColor = Color.white;
    public Color comboColor = Color.yellow;
    public Color maxComboColor = Color.red;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Call from NPCHitDetector or anywhere to show a popup at worldPos.
    /// </summary>
    public void ShowPopup(Vector3 worldPos, int points, int multiplier)
    {
        if (popupPrefab == null) return;
        StartCoroutine(AnimatePopup(worldPos, points, multiplier));
    }

    private IEnumerator AnimatePopup(Vector3 worldPos, int points, int multiplier)
    {
        GameObject popup = Instantiate(popupPrefab, worldPos + Vector3.up * 1.5f, Quaternion.identity);
        TMP_Text label = popup.GetComponentInChildren<TMP_Text>();

        if (label != null)
        {
            label.text = multiplier > 1
                ? $"+{points}\n<size=60%>x{multiplier} COMBO!</size>"
                : $"+{points}";

            // Colour based on multiplier tier
            label.color = multiplier >= ScoreManager.Instance?.maxMultiplier
                ? maxComboColor
                : multiplier > 1 ? comboColor : singleHitColor;
        }

        float elapsed = 0f;
        Vector3 startPos = popup.transform.position;

        while (elapsed < popupLifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popupLifetime;

            // Float upward
            popup.transform.position = startPos + Vector3.up * (floatSpeed * elapsed);

            // Face camera
            if (Camera.main != null)
                popup.transform.LookAt(Camera.main.transform);

            // Fade out in last 40% of lifetime
            if (label != null && t > 0.6f)
            {
                Color c = label.color;
                c.a = Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
                label.color = c;
            }

            yield return null;
        }

        Destroy(popup);
    }
}

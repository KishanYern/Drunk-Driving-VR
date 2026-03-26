using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Singleton score manager.
/// - Base points per hit scale with the current multiplier.
/// - Multiplier increases every consecutive hit within comboTimeWindow seconds.
/// - If the window expires with no new hit, multiplier resets to 1.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    // ------------------------------------------------------------------ //
    // Inspector
    // ------------------------------------------------------------------ //
    [Header("Scoring")]
    [Tooltip("Base points awarded per NPC hit (multiplied by current multiplier).")]
    public int basePointsPerHit = 100;

    [Tooltip("Seconds you have to hit another NPC before the combo resets.")]
    public float comboTimeWindow = 3f;

    [Tooltip("How much the multiplier increases with each consecutive hit.")]
    public int multiplierStep = 1;

    [Tooltip("Maximum multiplier cap.")]
    public int maxMultiplier = 10;

    [Header("UI")]
    [Tooltip("Text element showing the total score.")]
    public TMP_Text scoreText;

    [Tooltip("Text element showing the current multiplier.")]
    public TMP_Text multiplierText;

    [Tooltip("Text element showing the combo timer countdown (optional).")]
    public TMP_Text timerText;

    [Tooltip("Panel/object to flash or animate on combo increase (optional).")]
    public GameObject comboFeedbackObject;

    // ------------------------------------------------------------------ //
    // State
    // ------------------------------------------------------------------ //
    private int totalScore = 0;
    private int currentMultiplier = 1;
    private float comboTimer = 0f;          // counts DOWN from comboTimeWindow
    private bool comboActive = false;       // true after first hit in a chain
    private int consecutiveHits = 0;       // hits without the timer expiring

    // ------------------------------------------------------------------ //

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        if (!comboActive) return;

        comboTimer -= Time.deltaTime;

        // Update optional countdown UI
        if (timerText != null)
            timerText.text = Mathf.Max(comboTimer, 0f).ToString("F1") + "s";

        // Timer expired — reset combo
        if (comboTimer <= 0f)
            ResetCombo();
    }

    /// <summary>
    /// Call this whenever the player's car hits an NPC.
    /// </summary>
    public void RegisterHit()
    {
        consecutiveHits++;

        // Increase multiplier every hit, capped at maxMultiplier
        currentMultiplier = Mathf.Min(1 + (consecutiveHits - 1) * multiplierStep, maxMultiplier);

        // Award points
        int pointsAwarded = basePointsPerHit * currentMultiplier;
        totalScore += pointsAwarded;

        // Restart the combo window
        comboTimer = comboTimeWindow;
        comboActive = true;

        Debug.Log($"[Score] Hit! +{pointsAwarded} pts | x{currentMultiplier} multiplier | Total: {totalScore}");

        UpdateUI();
        TriggerComboFeedback();
    }

    private void ResetCombo()
    {
        consecutiveHits = 0;
        currentMultiplier = 1;
        comboActive = false;
        comboTimer = 0f;

        Debug.Log("[Score] Combo expired — multiplier reset.");
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + totalScore.ToString("N0");

        if (multiplierText != null)
        {
            multiplierText.text = currentMultiplier > 1
                ? "x" + currentMultiplier
                : "";           // Hide "x1" — looks cleaner
        }

        if (timerText != null && !comboActive)
            timerText.text = "";
    }

    private void TriggerComboFeedback()
    {
        if (comboFeedbackObject == null) return;
        // Simple pulse: disable and re-enable so you can drive an Animator trigger
        comboFeedbackObject.SetActive(false);
        comboFeedbackObject.SetActive(true);
    }

    // Public read-only accessors for other scripts if needed
    public int Score        => totalScore;
    public int Multiplier   => currentMultiplier;
    public float ComboTimer => comboTimer;
}

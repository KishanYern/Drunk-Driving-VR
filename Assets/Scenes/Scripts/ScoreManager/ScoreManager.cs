using UnityEngine;
using TMPro;

/// <summary>
/// Three hit types:
///   RegisterNPCHit()   — scores base points x multiplier, grows the multiplier chain
///   RegisterPropHit()  — flat +10 pts, no multiplier change
///   RegisterWallHit()  — -50 pts, immediately kills the multiplier chain
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    // ------------------------------------------------------------------ //
    // Inspector
    // ------------------------------------------------------------------ //
    [Header("Scoring — NPC")]
    public int basePointsPerHit = 100;
    public float comboTimeWindow = 3f;
    public int multiplierStep = 1;
    public int maxMultiplier = 10;

    [Header("Scoring — Props and Walls")]
    public int propHitPoints = 10;
    public int wallHitPenalty = 50;   // subtracted from score

    [Header("UI")]
    public TMP_Text scoreText;
    public TMP_Text multiplierText;
    public TMP_Text timerText;
    public TMP_Text feedbackText;         // shows "+10", "-50 WALL!" etc. briefly
    public GameObject comboFeedbackObject;

    // ------------------------------------------------------------------ //
    // State
    // ------------------------------------------------------------------ //
    private int totalScore = 0;
    private int currentMultiplier = 1;
    private float comboTimer = 0f;
    private bool comboActive = false;
    private int consecutiveHits = 0;

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

        if (timerText != null)
            timerText.text = Mathf.Max(comboTimer, 0f).ToString("F1") + "s";

        if (comboTimer <= 0f)
            ResetCombo(silent: true);
    }

    // ------------------------------------------------------------------ //
    // Public hit entry points
    // ------------------------------------------------------------------ //

    /// <summary>Hit an NPC — grows combo and scores multiplied points.</summary>
    public void RegisterNPCHit()
    {
        consecutiveHits++;
        currentMultiplier = Mathf.Min(1 + (consecutiveHits - 1) * multiplierStep, maxMultiplier);

        int pts = basePointsPerHit * currentMultiplier;
        AddScore(pts);

        comboTimer = comboTimeWindow;
        comboActive = true;

        ShowFeedback($"+{pts}" + (currentMultiplier > 1 ? $"  x{currentMultiplier}!" : ""), Color.yellow);
        TriggerComboFeedback();
        Debug.Log($"[Score] NPC Hit! +{pts} | x{currentMultiplier} | Total: {totalScore}");

        UpdateUI();
    }

    /// <summary>Hit a prop (light post, traffic light, etc.) — flat points, no multiplier effect.</summary>
    public void RegisterPropHit()
    {
        AddScore(propHitPoints);
        ShowFeedback($"+{propHitPoints}", Color.white);
        Debug.Log($"[Score] Prop Hit! +{propHitPoints} | Total: {totalScore}");
        UpdateUI();
    }

    /// <summary>Hit a wall or building — penalty and combo reset.</summary>
    public void RegisterWallHit()
    {
        AddScore(-wallHitPenalty);
        ResetCombo(silent: false);
        ShowFeedback($"-{wallHitPenalty}  WALL!", Color.red);
        Debug.Log($"[Score] Wall Hit! -{wallHitPenalty} | Total: {totalScore}");
        UpdateUI();
    }

    // ------------------------------------------------------------------ //
    // Internal helpers
    // ------------------------------------------------------------------ //

    private void AddScore(int amount)
    {
        totalScore = Mathf.Max(0, totalScore + amount);   // clamp at 0, no negative totals
    }

    private void ResetCombo(bool silent)
    {
        consecutiveHits = 0;
        currentMultiplier = 1;
        comboActive = false;
        comboTimer = 0f;

        if (!silent)
            Debug.Log("[Score] Combo broken!");

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + totalScore.ToString("N0");

        if (multiplierText != null)
            multiplierText.text = currentMultiplier > 1 ? $"x{currentMultiplier}" : "";

        if (timerText != null && !comboActive)
            timerText.text = "";
    }

    private void ShowFeedback(string message, Color color)
    {
        if (feedbackText == null) return;
        feedbackText.text = message;
        feedbackText.color = color;
        CancelInvoke(nameof(ClearFeedback));
        Invoke(nameof(ClearFeedback), 1.2f);
    }

    private void ClearFeedback()
    {
        if (feedbackText != null) feedbackText.text = "";
    }

    private void TriggerComboFeedback()
    {
        if (comboFeedbackObject == null) return;
        comboFeedbackObject.SetActive(false);
        comboFeedbackObject.SetActive(true);
    }

    public int Score => totalScore;
    public int Multiplier => currentMultiplier;
    public float ComboTimer => comboTimer;
}
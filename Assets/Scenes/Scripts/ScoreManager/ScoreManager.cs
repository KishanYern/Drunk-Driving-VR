using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Three hit types:
///   RegisterNPCHit()   — scores base points x multiplier, grows the multiplier chain
///   RegisterPropHit()  — flat +10 pts, no multiplier change
///   RegisterWallHit()  — -50 pts, immediately kills the multiplier chain
///
/// 60-second countdown. Every 1,000-point threshold awards +15s.
/// On timeout: shows leaderboard panel with a Restart button.
/// Leaderboard persists for the session (top 5 runs by score).
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
    public int wallHitPenalty = 50;

    [Header("UI")]
    public TMP_Text scoreText;
    public TMP_Text highScoreText;
    public TMP_Text multiplierText;
    public TMP_Text timerText;
    public TMP_Text feedbackText;
    public GameObject comboFeedbackObject;

    [Header("Global Timer")]
    public float startingTime = 60f;
    public float timeAddedPerHit = 0.1f;
    public TMP_Text globalTimerText;

    [Header("Score Milestones")]
    public int milestoneInterval = 1000;
    public float milestoneTimeBonus = 15f;

    [Header("Leaderboard Panel")]
    [Tooltip("The parent GameObject containing the leaderboard UI. Hidden during play, shown on game over.")]
    public GameObject leaderboardPanel;
    [Tooltip("TMP Text that shows the top 5 runs.")]
    public TMP_Text leaderboardText;
    [Tooltip("The restart button inside the leaderboard panel.")]
    public Button restartButton;
    public int maxLeaderboardEntries = 5;

    // ------------------------------------------------------------------ //
    // Session Leaderboard (survives scene reloads via static)
    // ------------------------------------------------------------------ //
    private struct RunEntry
    {
        public int runNumber;
        public int score;
    }

    private static List<RunEntry> sessionLeaderboard = new List<RunEntry>();
    private static int totalRunCount = 0;

    // ------------------------------------------------------------------ //
    // State
    // ------------------------------------------------------------------ //
    private int totalScore = 0;
    private int currentMultiplier = 1;
    private float comboTimer = 0f;
    private bool comboActive = false;
    private int consecutiveHits = 0;
    private float currentGlobalTime;
    private bool isGameOver = false;
    private int lastMilestoneReached = 0;

    private const string HighScoreKey = "DrivingHighScore";

    // ------------------------------------------------------------------ //

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        currentGlobalTime = startingTime;
        totalRunCount++;
    }

    void Start()
    {
        // Hide leaderboard panel at the start of each run
        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(false);

        // Wire up restart button
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartScene);

        UpdateUI();
        RefreshHighScoreUI();
        RefreshLeaderboardUI();
    }

    void Update()
    {
        if (!isGameOver)
        {
            currentGlobalTime -= Time.deltaTime;

            if (currentGlobalTime <= 0f)
            {
                currentGlobalTime = 0f;
                isGameOver = true;
                EndRun();
            }

            if (globalTimerText != null)
            {
                globalTimerText.text = "Timer: " + currentGlobalTime.ToString("F1") + "s";
                globalTimerText.color = currentGlobalTime <= 10f ? Color.red : Color.white;
            }
        }

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

    public void RegisterNPCHit()
    {
        AddTime();
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

    public void RegisterPropHit()
    {
        AddTime();
        AddScore(propHitPoints);
        ShowFeedback($"+{propHitPoints}", Color.white);
        Debug.Log($"[Score] Prop Hit! +{propHitPoints} | Total: {totalScore}");
        UpdateUI();
    }

    public void RegisterWallHit()
    {
        AddTime();
        AddScore(-wallHitPenalty);
        ResetCombo(silent: false);
        ShowFeedback($"-{wallHitPenalty}  WALL!", Color.red);
        Debug.Log($"[Score] Wall Hit! -{wallHitPenalty} | Total: {totalScore}");
        UpdateUI();
    }

    // ------------------------------------------------------------------ //
    // End of run
    // ------------------------------------------------------------------ //

    private void EndRun()
    {
        Debug.Log($"[ScoreManager] Time's up! Final score: {totalScore}");

        // Add to session leaderboard
        sessionLeaderboard.Add(new RunEntry { runNumber = totalRunCount, score = totalScore });
        sessionLeaderboard.Sort((a, b) => b.score.CompareTo(a.score));
        if (sessionLeaderboard.Count > maxLeaderboardEntries)
            sessionLeaderboard.RemoveAt(sessionLeaderboard.Count - 1);

        // Save all-time high score
        int best = PlayerPrefs.GetInt(HighScoreKey, 0);
        if (totalScore > best)
        {
            PlayerPrefs.SetInt(HighScoreKey, totalScore);
            PlayerPrefs.Save();
            Debug.Log($"[ScoreManager] New high score: {totalScore}!");
        }

        RefreshHighScoreUI();
        RefreshLeaderboardUI();

        // Show the leaderboard panel with restart button
        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(true);
    }

    private void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ------------------------------------------------------------------ //
    // Internal helpers
    // ------------------------------------------------------------------ //

    private void AddTime()
    {
        if (!isGameOver)
            currentGlobalTime += timeAddedPerHit;
    }

    private void AddScore(int amount)
    {
        totalScore = Mathf.Max(0, totalScore + amount);
        CheckMilestone();
    }

    private void CheckMilestone()
    {
        int milestoneCount = totalScore / milestoneInterval;
        if (milestoneCount > lastMilestoneReached)
        {
            int newMilestones = milestoneCount - lastMilestoneReached;
            lastMilestoneReached = milestoneCount;
            float bonus = newMilestones * milestoneTimeBonus;
            currentGlobalTime += bonus;
            Debug.Log($"[ScoreManager] Milestone! +{bonus}s (score: {totalScore})");
            ShowFeedback($"+{bonus}s BONUS!", Color.cyan);
        }
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

    private void RefreshHighScoreUI()
    {
        if (highScoreText == null) return;
        int best = PlayerPrefs.GetInt(HighScoreKey, 0);
        highScoreText.text = best == 0 ? "High Score: --" : $"High Score: {best:N0}";
    }

    private void RefreshLeaderboardUI()
    {
        if (leaderboardText == null) return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        // Title
        sb.AppendLine("LEADERBOARD");
        
        if (sessionLeaderboard.Count == 0)
        {
            sb.AppendLine("No runs yet.");
        }
        else
        {
            // Column headers
            sb.AppendLine("  #    RUN       SCORE");

            
            for (int i = 0; i < sessionLeaderboard.Count; i++)
            {
                RunEntry entry = sessionLeaderboard[i];
                sb.AppendLine($"  {i + 1}.   Run {entry.runNumber}    {entry.score:N0} pts");
            }
        }

        leaderboardText.text = sb.ToString().TrimEnd();
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

    public void ResetHighScore()
    {
        PlayerPrefs.DeleteKey(HighScoreKey);
        PlayerPrefs.Save();
        RefreshHighScoreUI();
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Car Selection panel in the Main Menu scene.
/// The panel is a World Space Canvas with prev/next arrows, a car preview image,
/// name/description/stats display, and a Confirm button.
///
/// Call ShowPanel() from MainMenuManager when the player presses "Select Car".
///
/// PANEL SETUP (World Space Canvas, same approach as MenuPanel):
///
///  CarSelectionPanel (Canvas — World Space, 700×500, scale 0.002)
///  ├── Background (Image, dark, semi-transparent)
///  ├── TitleText          (TMP)  "SELECT YOUR CAR"
///  ├── CarPreviewImage    (Image)  — assign carPreviewImage
///  ├── CarNameText        (TMP)   — assign carNameText
///  ├── CarDescText        (TMP)   — assign carDescText
///  ├── StatsPanel
///  │   ├── SpeedLabel + SpeedBar (Slider, non-interactable)
///  │   ├── HandlingLabel + HandlingBar
///  │   └── WeightLabel   + WeightBar
///  ├── PrevButton         (Button) ◀  — assign prevButton
///  ├── NextButton         (Button) ▶  — assign nextButton
///  ├── ConfirmButton      (Button) "CONFIRM"  — assign confirmButton
///  ├── CloseButton        (Button) "✕"        — assign closeButton
///  └── PageText           (TMP)  "1 / 3"     — assign pageText
/// </summary>
public class CarSelectionManager : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    // Inspector references
    // ------------------------------------------------------------------ //

    [Header("Panel")]
    public GameObject selectionPanel;
    public float panelDistance = 2f;
    public float panelHeightOffset = 0f;

    [Header("Car Display")]
    public Image          carPreviewImage;
    public TMP_Text       carNameText;
    public TMP_Text       carDescText;
    public TMP_Text       pageText;

    [Header("Stat Bars  (Slider, Max=5, non-interactable)")]
    public Slider speedBar;
    public Slider handlingBar;
    public Slider weightBar;

    [Header("Buttons")]
    public Button prevButton;
    public Button nextButton;
    public Button confirmButton;
    public Button closeButton;

    // ------------------------------------------------------------------ //
    // Private
    // ------------------------------------------------------------------ //

    private Transform playerHead;
    private int previewIndex = 0;       // local preview — not committed until Confirm

    // ------------------------------------------------------------------ //

    void Start()
    {
        OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();
        if (rig != null) playerHead = rig.centerEyeAnchor;
        if (playerHead == null && Camera.main != null) playerHead = Camera.main.transform;

        // Wire buttons
        if (prevButton    != null) prevButton.onClick.AddListener(OnPrev);
        if (nextButton    != null) nextButton.onClick.AddListener(OnNext);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (closeButton   != null) closeButton.onClick.AddListener(HidePanel);

        // Start with the panel hidden
        if (selectionPanel != null) selectionPanel.SetActive(false);
    }

    // ------------------------------------------------------------------ //
    // Public API — called from MainMenuManager
    // ------------------------------------------------------------------ //

    public void ShowPanel()
    {
        if (selectionPanel == null) return;

        // Sync preview to whatever is currently selected
        previewIndex = GameStateManager.Instance != null
            ? GameStateManager.Instance.SelectedCarIndex
            : 0;

        PositionPanel();
        selectionPanel.SetActive(true);
        RefreshDisplay();
    }

    public void HidePanel()
    {
        if (selectionPanel != null) selectionPanel.SetActive(false);
    }

    // ------------------------------------------------------------------ //
    // Button handlers
    // ------------------------------------------------------------------ //

    private void OnPrev()
    {
        int count = CarCount();
        if (count == 0) return;
        previewIndex = (previewIndex - 1 + count) % count;
        RefreshDisplay();
    }

    private void OnNext()
    {
        int count = CarCount();
        if (count == 0) return;
        previewIndex = (previewIndex + 1) % count;
        RefreshDisplay();
    }

    private void OnConfirm()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.SelectCar(previewIndex);

        HidePanel();
        Debug.Log($"[CarSelection] Confirmed car index {previewIndex}");
    }

    // ------------------------------------------------------------------ //
    // Display refresh
    // ------------------------------------------------------------------ //

    private void RefreshDisplay()
    {
        CarData[] cars = GameStateManager.Instance?.availableCars;
        if (cars == null || cars.Length == 0)
        {
            SetEmptyDisplay();
            return;
        }

        CarData car = cars[previewIndex];

        if (carNameText    != null) carNameText.text = car.carName;
        if (carDescText    != null) carDescText.text = car.description;
        if (pageText       != null) pageText.text    = $"{previewIndex + 1} / {cars.Length}";

        if (carPreviewImage != null)
        {
            carPreviewImage.sprite  = car.previewSprite;
            carPreviewImage.enabled = car.previewSprite != null;
        }

        // Stat bars — sliders should have Min=0, Max=5 set in the Inspector
        if (speedBar    != null) speedBar.value    = car.speedRating;
        if (handlingBar != null) handlingBar.value = car.handlingRating;
        if (weightBar   != null) weightBar.value   = car.weightRating;

        // Tint the stat bar fills with the car's accent colour
        SetSliderColor(speedBar,    car.accentColor);
        SetSliderColor(handlingBar, car.accentColor);
        SetSliderColor(weightBar,   car.accentColor);

        // Disable arrows when only one car exists
        bool moreThanOne = cars.Length > 1;
        if (prevButton != null) prevButton.interactable = moreThanOne;
        if (nextButton != null) nextButton.interactable = moreThanOne;
    }

    private void SetEmptyDisplay()
    {
        if (carNameText  != null) carNameText.text = "No Cars Available";
        if (carDescText  != null) carDescText.text = "";
        if (pageText     != null) pageText.text    = "0 / 0";
        if (confirmButton != null) confirmButton.interactable = false;
        if (prevButton   != null) prevButton.interactable = false;
        if (nextButton   != null) nextButton.interactable = false;
    }

    private void SetSliderColor(Slider slider, Color color)
    {
        if (slider == null) return;
        var fill = slider.fillRect?.GetComponent<Image>();
        if (fill != null) fill.color = color;
    }

    private int CarCount()
    {
        return GameStateManager.Instance?.availableCars?.Length ?? 0;
    }

    // ------------------------------------------------------------------ //
    // Panel positioning (same pattern as MainMenuManager)
    // ------------------------------------------------------------------ //

    private void PositionPanel()
    {
        if (playerHead == null || selectionPanel == null) return;

        Vector3 forward = playerHead.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 pos = playerHead.position
                    + forward * panelDistance
                    + Vector3.up * panelHeightOffset;

        selectionPanel.transform.position = pos;
        selectionPanel.transform.rotation = Quaternion.LookRotation(
            selectionPanel.transform.position - playerHead.position
        );
    }
}

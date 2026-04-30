using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Shows a "How to Play" control guide panel when the game scene loads.
/// Driving input is LOCKED until the player dismisses this panel.
///
/// Dismissal: press X (left controller) or tap the on-screen button.
///
/// PANEL SETUP (World Space Canvas in Final_Driving_Sim scene):
///
///  ControlGuidePanel (Canvas — World Space, 700×500, scale 0.002)
///  ├── Background        (Image, dark semi-transparent)
///  ├── TitleText         (TMP)  "HOW TO DRIVE"
///  ├── ControlsGrid      (Grid Layout Group — 2 columns)
///  │   ├── Row: [🎮 icon]  "RIGHT TRIGGER"     "Accelerate"
///  │   ├── Row: [🎮 icon]  "LEFT TRIGGER"      "Brake / Reverse"
///  │   ├── Row: [🎮 icon]  "STEERING WHEEL"    "Grab rim + twist to steer"
///  │   ├── Row: [🎮 icon]  "LEFT STICK"        "Look around (comfort turn)"
///  │   └── Row: [🎮 icon]  "X / Y BUTTON"      "Dismiss this guide"
///  ├── DismissHintText   (TMP)  — assign dismissHintText
///  └── DismissButton     (Button) "GOT IT — START DRIVING"  — assign dismissButton
///
/// Attach this script to a GameObject in Final_Driving_Sim.
/// Assign the ControlGuidePanel canvas and the CarController2_VR in the Inspector.
/// </summary>
public class ControlSchemaUI : MonoBehaviour
{
    [Header("Panel Reference")]
    [Tooltip("The World Space Canvas showing the control guide.")]
    public GameObject controlPanel;

    [Tooltip("How far in front of the player the guide appears (metres).")]
    public float panelDistance = 1.8f;

    [Tooltip("Height offset from eye level (negative = lower).")]
    public float panelHeightOffset = 0f;

    [Header("Car Controller")]
    [Tooltip("Assign the CarController2_VR on the player's car. " +
             "Driving input will be locked until this panel is dismissed.")]
    public CarController2_VR carController;

    [Header("Dismiss")]
    [Tooltip("The on-screen 'Got It' button.")]
    public Button dismissButton;

    [Tooltip("Hint text at the bottom of the panel (auto-populated).")]
    public TMP_Text dismissHintText;

    [Header("Fade")]
    public float fadeOutDuration = 0.5f;

    // ------------------------------------------------------------------ //

    private Transform playerHead;
    private CanvasGroup canvasGroup;
    private bool isDismissed = false;

    void Start()
    {
        // Find VR camera head
        OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();
        if (rig != null) playerHead = rig.centerEyeAnchor;
        if (playerHead == null && Camera.main != null) playerHead = Camera.main.transform;

        // Auto-find car controller if not assigned
        if (carController == null)
            carController = Object.FindFirstObjectByType<CarController2_VR>();

        // Lock driving input immediately
        if (carController != null)
        {
            carController.inputLocked = true;
            Debug.Log("[ControlSchema] Car input LOCKED — waiting for player to dismiss guide.");
        }

        if (controlPanel != null)
        {
            canvasGroup = controlPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = controlPanel.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 1f;
            controlPanel.SetActive(false); // hidden until ShowPanel() fires

            // Small delay so the player fully loads in before the guide pops up
            Invoke(nameof(ShowPanel), 1.5f);
        }

        if (dismissButton != null)
            dismissButton.onClick.AddListener(DismissPanel);

        if (dismissHintText != null)
            dismissHintText.text = "Press  X  on the left controller, or tap below to start driving";
    }

    void Update()
    {
        if (isDismissed) return;

        // X button (left controller) or Y button dismisses the panel
        if (OVRInput.GetDown(OVRInput.Button.Three, OVRInput.Controller.LTouch) ||   // X
            OVRInput.GetDown(OVRInput.Button.Four,  OVRInput.Controller.LTouch))      // Y
        {
            DismissPanel();
        }
    }

    // ------------------------------------------------------------------ //

    private void ShowPanel()
    {
        if (controlPanel == null || playerHead == null) return;

        controlPanel.SetActive(true);

        // Position in front of player
        Vector3 forward = playerHead.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 pos = playerHead.position
                    + forward * panelDistance
                    + Vector3.up * panelHeightOffset;

        controlPanel.transform.position = pos;
        controlPanel.transform.rotation = Quaternion.LookRotation(
            controlPanel.transform.position - playerHead.position
        );
    }

    public void DismissPanel()
    {
        if (isDismissed) return;
        isDismissed = true;

        // Unlock driving as soon as the player confirms they've read the guide
        if (carController != null)
        {
            carController.inputLocked = false;
            Debug.Log("[ControlSchema] Car input UNLOCKED — player is ready to drive.");
        }

        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            if (canvasGroup != null)
                canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOutDuration);
            yield return null;
        }

        if (controlPanel != null)
            controlPanel.SetActive(false);
    }
}

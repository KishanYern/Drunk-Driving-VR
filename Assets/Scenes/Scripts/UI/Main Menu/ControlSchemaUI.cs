using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Shows a "How to Play" control guide panel when the game scene loads.
/// The panel floats in front of the player and is dismissed by pressing
/// the left controller A/X button or the on-screen Dismiss button.
///
/// Attach this to a GameObject in your Final_Driving_Sim scene.
/// </summary>
public class ControlSchemaUI : MonoBehaviour
{
    [Header("Panel Reference")]
    [Tooltip("The World Space Canvas showing the control guide.")]
    public GameObject controlPanel;

    [Tooltip("How far in front of the player the guide appears.")]
    public float panelDistance = 1.8f;

    [Tooltip("Height offset from eye level (negative = lower).")]
    public float panelHeightOffset = 0f;

    [Header("Dismiss Settings")]
    [Tooltip("The on-screen dismiss button.")]
    public Button dismissButton;

    [Tooltip("Show a 'press X to dismiss' hint on the panel.")]
    public TMP_Text dismissHintText;

    [Header("Fade")]
    public float fadeOutDuration = 0.5f;

    // ------------------------------------------------------------------ //

    private Transform playerHead;
    private CanvasGroup canvasGroup;
    private bool isDismissed = false;

    void Start()
    {
        OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();
        if (rig != null) playerHead = rig.centerEyeAnchor;
        if (playerHead == null && Camera.main != null) playerHead = Camera.main.transform;

        if (controlPanel != null)
        {
            canvasGroup = controlPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = controlPanel.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 1f;

            // Small delay so the player fully loads in before showing guide
            Invoke(nameof(ShowPanel), 1.5f);
        }

        if (dismissButton != null)
            dismissButton.onClick.AddListener(DismissPanel);

        if (dismissHintText != null)
            dismissHintText.text = "Press  X  or tap below to dismiss";
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

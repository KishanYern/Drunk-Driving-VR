using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Manages the main menu floating panel.
/// The panel re-anchors itself in front of the player's head EVERY FRAME,
/// so it always shows up no matter how late OVR tracking initialises and
/// no matter where the player has been teleported in the world.
///
/// SCENE SETUP: See bottom of file.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("The World Space Canvas panel GameObject.")]
    public GameObject menuPanel;

    [Tooltip("How far in front of the player the menu floats (metres).")]
    public float menuDistance = 2f;

    [Tooltip("Height offset from the player's eye level.")]
    public float menuHeightOffset = 0f;

    [Tooltip("World-space scale to force on the panel each frame. Set X/Y/Z to 0 to disable.")]
    public Vector3 panelScale = new Vector3(0.003f, 0.003f, 0.003f);

    [Header("Buttons")]
    public Button playButton;
    public Button selectCarButton;
    public Button quitButton;

    [Header("Car Selection")]
    [Tooltip("Assign the CarSelectionManager in the scene.")]
    public CarSelectionManager carSelectionManager;

    [Header("Scene Names")]
    [Tooltip("Exact name of your game scene in Build Settings.")]
    public string gameSceneName = "Final_Driving_Sim";

    // ------------------------------------------------------------------ //

    private Transform playerHead;
    private bool buttonsWired = false;
    private float retryHeadSearchAt = 0f;

    void Start()
    {
        Debug.Log("[Menu] MainMenuManager Start() running.");

        // Make the panel WORLD-SPACE — never parent it to the camera.
        // (Parenting can drag along stray Camera/AudioListener components on
        // the canvas and cause black/blank views in VR.)
        if (menuPanel != null)
        {
            menuPanel.transform.SetParent(null, worldPositionStays: true);
            menuPanel.SetActive(true);

            // Cleanup: a stray Camera and/or AudioListener can end up on this
            // GameObject if it was created via the wrong UI menu. They cause
            // the view to go black/strange in VR. Disable them just in case.
            Camera strayCam = menuPanel.GetComponent<Camera>();
            if (strayCam != null)
            {
                strayCam.enabled = false;
                Debug.Log("[Menu] Disabled stray Camera component on MenuPanel.");
            }

            AudioListener strayListener = menuPanel.GetComponent<AudioListener>();
            if (strayListener != null)
            {
                strayListener.enabled = false;
                Debug.Log("[Menu] Disabled stray AudioListener component on MenuPanel.");
            }
        }
        else
        {
            Debug.LogError("[Menu] menuPanel is not assigned in the inspector!");
        }

        FindPlayerHead();
        WireButtons();
        EnsureButtonsAreClickable();
    }

    /// <summary>
    /// Each button needs:
    ///   - to live on the UI layer (5) so VRRayPointer's layer mask hits it
    ///   - a BoxCollider sized to its RectTransform so Physics.Raycast can hit it
    /// We set both at runtime so the inspector setup can't be wrong.
    /// </summary>
    private void EnsureButtonsAreClickable()
    {
        const int uiLayerIndex = 5; // built-in UI layer
        Button[] buttons = { playButton, selectCarButton, quitButton };

        foreach (Button b in buttons)
        {
            if (b == null) continue;

            b.gameObject.layer = uiLayerIndex;

            RectTransform rt = b.transform as RectTransform;
            BoxCollider box = b.GetComponent<BoxCollider>();
            if (box == null) box = b.gameObject.AddComponent<BoxCollider>();

            if (rt != null)
            {
                Vector2 size = rt.rect.size;
                box.size   = new Vector3(size.x, size.y, 1f);
                box.center = Vector3.zero;
            }
            box.isTrigger = false; // Physics.Raycast ignores triggers when Queries Hit Triggers is off; safer to keep it solid
        }
    }

    void LateUpdate()
    {
        // Keep retrying to find the head until OVR has fully initialised.
        if (playerHead == null && Time.time >= retryHeadSearchAt)
        {
            retryHeadSearchAt = Time.time + 0.25f;
            FindPlayerHead();
        }

        if (playerHead == null || menuPanel == null) return;

        // ---- Position the panel in front of the user, yaw-only ----
        // Strip pitch/roll so the menu sits upright even if the user looks up/down.
        Vector3 forward = playerHead.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 targetPos = playerHead.position
                          + forward * menuDistance
                          + Vector3.up * menuHeightOffset;

        menuPanel.transform.position = targetPos;
        menuPanel.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        if (panelScale.sqrMagnitude > 0f)
            menuPanel.transform.localScale = panelScale;
    }

    private void FindPlayerHead()
    {
        // 1) Standard OVR rig
        OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();
        if (rig != null && rig.centerEyeAnchor != null)
        {
            playerHead = rig.centerEyeAnchor;
            Debug.Log($"[Menu] playerHead = {playerHead.name} (from OVRCameraRig)");
            return;
        }

        // 2) Search by name — scene has a customised rig with extra "*Detached" children
        GameObject byName = GameObject.Find("CenterEyeAnchor");
        if (byName != null)
        {
            playerHead = byName.transform;
            Debug.Log("[Menu] playerHead = CenterEyeAnchor (found by name)");
            return;
        }

        // 3) Fallback to the main camera (covers OpenXR / non-OVR rigs)
        if (Camera.main != null)
        {
            playerHead = Camera.main.transform;
            Debug.Log($"[Menu] playerHead = {playerHead.name} (Camera.main fallback)");
            return;
        }

        Debug.LogWarning("[Menu] No player head found yet — will retry next frame.");
    }

    private void WireButtons()
    {
        if (buttonsWired) return;

        if (playButton != null)
            playButton.onClick.AddListener(OnPlayPressed);

        if (selectCarButton != null)
        {
            selectCarButton.interactable = true;
            selectCarButton.onClick.AddListener(OnSelectCarPressed);

            TMP_Text label = selectCarButton.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = "Select Car";
        }

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitPressed);

        buttonsWired = true;
    }

    private void OnSelectCarPressed()
    {
        Debug.Log("[Menu] Select Car pressed.");
        if (carSelectionManager != null)
            carSelectionManager.ShowPanel();
        else
            Debug.LogWarning("[Menu] CarSelectionManager not assigned on MainMenuManager.");
    }

    private void OnPlayPressed()
    {
        Debug.Log("[Menu] Play pressed — loading game scene.");
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(gameSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
    }

    private void OnQuitPressed()
    {
        Debug.Log("[Menu] Quit pressed.");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}


// ==========================================================================
// MENU SCENE SETUP GUIDE
// ==========================================================================
//
// 1. CREATE A NEW SCENE
//    File > New Scene > Basic (Built-in)
//    Save as "MainMenu" in Assets/Scenes/
//
// 2. ADD TO BUILD SETTINGS
//    File > Build Settings > Add Open Scenes
//    Make sure MainMenu = index 0, Final_Driving_Sim = index 1
//
// 3. ADD OVR RIG
//    Drag your OculusInteractionSampleRig prefab into the scene
//    Position at 0,0,0
//
// 4. ADD SCENELOADER
//    Create Empty > rename "SceneLoader" > Add Component > SceneLoader
//
// 5. CREATE THE MENU PANEL (World Space Canvas)
//    a. Right-click Hierarchy > UI > Canvas
//       - Rename "MenuPanel"
//       - Render Mode: World Space
//       - Width: 600, Height: 400
//       - REMOVE any extra Camera component if Unity added one
//
//    b-f. (See previous setup notes for buttons / title / etc.)
//
// 6-8. Standard managers (GameStateManager, CarSelectionManager, MainMenuManager)
//
// 9. ADD RAY POINTER
//    See VRRayPointer.cs setup guide
//
// 10. PHYSICS RAYCASTER on each Canvas
//    Select MenuPanel (and CarSelectionPanel) > Add Component > Physics Raycaster
//    Also: each Button needs a Box Collider matching its rect size.
// ==========================================================================

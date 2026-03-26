using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Manages the main menu floating panel.
/// The panel floats in front of the player on Start.
///
/// SCENE SETUP: See bottom of file.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("The World Space Canvas panel GameObject.")]
    public GameObject menuPanel;

    [Tooltip("How far in front of the player the menu floates (metres).")]
    public float menuDistance = 2f;

    [Tooltip("Height offset from the player's eye level.")]
    public float menuHeightOffset = 0f;

    [Header("Buttons")]
    public Button playButton;
    public Button selectCarButton;      // greyed out for now — coming soon
    public Button quitButton;

    [Header("Scene Names")]
    [Tooltip("Exact name of your game scene in Build Settings.")]
    public string gameSceneName = "Final_Driving_Sim";

    // ------------------------------------------------------------------ //

    private Transform playerHead;

    void Start()
    {
        // Find the player's head (CenterEyeAnchor)
        OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();
        if (rig != null) playerHead = rig.centerEyeAnchor;
        if (playerHead == null && Camera.main != null) playerHead = Camera.main.transform;

        PositionMenuInFrontOfPlayer();
        SetupButtons();
    }

    private void PositionMenuInFrontOfPlayer()
    {
        if (menuPanel == null || playerHead == null) return;

        // Place panel directly in front of eyes, at eye height
        Vector3 forward    = playerHead.forward;
        forward.y          = 0f;                        // keep it upright
        forward.Normalize();

        Vector3 position   = playerHead.position
                           + forward * menuDistance
                           + Vector3.up * menuHeightOffset;

        menuPanel.transform.position = position;

        // Face the player
        menuPanel.transform.rotation = Quaternion.LookRotation(
            menuPanel.transform.position - playerHead.position
        );
    }

    private void SetupButtons()
    {
        if (playButton != null)
            playButton.onClick.AddListener(OnPlayPressed);

        if (selectCarButton != null)
        {
            // Disable for now — grey it out visually
            selectCarButton.interactable = false;
            TMP_Text label = selectCarButton.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = "Select Car\n<size=60%>(Coming Soon)</size>";
        }

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitPressed);
    }

    private void OnPlayPressed()
    {
        Debug.Log("[Menu] Play pressed — loading game scene.");
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(gameSceneName);
        else
        {
            // Fallback if SceneLoader not in scene
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
        }
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
//       - Width: 600, Height: 400, Scale: 0.002, 0.002, 0.002
//
//    b. Add a background Image child:
//       - Right-click MenuPanel > UI > Image
//       - Stretch to fill, dark colour, alpha ~200
//
//    c. Add Title text:
//       - Right-click MenuPanel > UI > Text - TextMeshPro
//       - Text: "DRUNK DRIVING VR"
//       - Font Size: 72, Bold, White, Centre-top anchor
//
//    d. Add PLAY button:
//       - Right-click MenuPanel > UI > Button - TextMeshPro
//       - Rename "PlayButton"
//       - Label text: "PLAY"
//       - Position: centre, slightly above middle
//
//    e. Add SELECT CAR button (same steps, rename "SelectCarButton")
//       Position: below play button
//
//    f. Add QUIT button (rename "QuitButton")
//       Position: bottom
//
// 6. ADD MAINMENUMANAGER
//    Create Empty > rename "MainMenuManager" > Add Component > MainMenuManager
//    - Menu Panel: drag MenuPanel canvas
//    - Play/SelectCar/Quit Buttons: drag respective buttons
//    - Game Scene Name: "Final_Driving_Sim"
//
// 7. ADD RAY POINTER
//    See VRRayPointer.cs setup guide
//
// 8. PHYSICS RAYCASTER on the Canvas
//    Select MenuPanel > Add Component > Physics Raycaster
//    This lets the ray hit UI elements in world space.
//    Also: each Button needs a Box Collider (same size as the button rect).
// ==========================================================================

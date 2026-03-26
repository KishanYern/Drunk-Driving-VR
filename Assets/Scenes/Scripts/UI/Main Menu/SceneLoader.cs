using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton scene loader with a fade-to-black transition.
/// Call SceneLoader.Instance.LoadScene("SceneName") from anywhere.
/// Attach to a GameObject in the Menu scene with DontDestroyOnLoad.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Fade Settings")]
    [Tooltip("How long the fade to black takes in seconds.")]
    public float fadeDuration = 1f;

    // The fade overlay — a full-screen quad in front of the camera
    private GameObject fadeQuad;
    private Material fadeMaterial;
    private bool isFading = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        CreateFadeQuad();
        // Start fully black then fade in
        StartCoroutine(FadeIn());
    }

    // ------------------------------------------------------------------ //
    // Public API
    // ------------------------------------------------------------------ //

    public void LoadScene(string sceneName)
    {
        if (!isFading)
            StartCoroutine(FadeAndLoad(sceneName));
    }

    // ------------------------------------------------------------------ //
    // Fade quad — a black quad parented to the camera
    // ------------------------------------------------------------------ //

    private void CreateFadeQuad()
    {
        fadeQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fadeQuad.name = "_FadeOverlay";
        DontDestroyOnLoad(fadeQuad);

        // Destroy the collider — we don't want it blocking anything
        Destroy(fadeQuad.GetComponent<Collider>());

        fadeMaterial = new Material(Shader.Find("Unlit/Color"));
        fadeMaterial.color = Color.black;
        fadeQuad.GetComponent<Renderer>().material = fadeMaterial;

        // Place it just in front of the camera
        AttachFadeQuadToCamera();

        SetFadeAlpha(1f); // start black
    }

    private void AttachFadeQuadToCamera()
    {
        // Find CenterEyeAnchor (VR) or fallback to main camera
        Transform cam = null;

        OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();
        if (rig != null) cam = rig.centerEyeAnchor;
        if (cam == null && Camera.main != null) cam = Camera.main.transform;

        if (cam != null)
        {
            fadeQuad.transform.SetParent(cam, false);
            fadeQuad.transform.localPosition = new Vector3(0f, 0f, 0.31f);
            fadeQuad.transform.localRotation = Quaternion.identity;
            fadeQuad.transform.localScale    = new Vector3(2f, 2f, 1f);
        }
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeMaterial != null)
        {
            Color c = fadeMaterial.color;
            c.a = alpha;
            fadeMaterial.color = c;
        }

        // Show/hide the quad
        if (fadeQuad != null)
            fadeQuad.SetActive(alpha > 0.01f);
    }

    // ------------------------------------------------------------------ //
    // Coroutines
    // ------------------------------------------------------------------ //

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            SetFadeAlpha(1f - Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        SetFadeAlpha(0f);
    }

    private IEnumerator FadeAndLoad(string sceneName)
    {
        isFading = true;
        SetFadeAlpha(0f);
        fadeQuad.SetActive(true);

        // Fade out
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            SetFadeAlpha(Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        SetFadeAlpha(1f);

        // Load scene
        yield return SceneManager.LoadSceneAsync(sceneName);

        // Re-attach fade quad to new scene's camera
        AttachFadeQuadToCamera();

        // Fade in
        yield return StartCoroutine(FadeIn());

        isFading = false;
    }
}

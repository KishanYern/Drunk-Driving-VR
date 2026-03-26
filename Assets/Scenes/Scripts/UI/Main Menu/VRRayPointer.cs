using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Casts a ray from the right controller and interacts with World Space UI buttons.
/// Attach to a child of the right hand anchor (or anywhere — it finds the hand itself).
///
/// SETUP:
///   1. Create an empty child under OVRCameraRig > TrackingSpace > RightHandAnchor
///   2. Rename it "RayPointer" and attach this script
///   3. Assign the lineRenderer field (add a LineRenderer component to RayPointer)
/// </summary>
public class VRRayPointer : MonoBehaviour
{
    [Header("Ray Settings")]
    public float rayLength = 10f;
    public LayerMask uiLayer;                   // set to "UI" in Inspector

    [Header("Visual")]
    public LineRenderer lineRenderer;
    public Color defaultColor  = new Color(1f, 1f, 1f, 0.5f);
    public Color hoverColor    = Color.cyan;

    [Header("Dot at hit point (optional)")]
    public GameObject hitDotPrefab;             // small sphere to show where ray lands

    // ------------------------------------------------------------------ //

    private Transform rightHand;
    private GameObject currentHover;
    private GameObject hitDotInstance;

    void Start()
    {
        // Find right hand anchor
        OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();
        if (rig != null) rightHand = rig.rightHandAnchor;

        SetupLineRenderer();

        if (hitDotPrefab != null)
        {
            hitDotInstance = Instantiate(hitDotPrefab);
            hitDotInstance.SetActive(false);
        }
    }

    void Update()
    {
        if (rightHand == null) return;

        Vector3 origin    = rightHand.position;
        Vector3 direction = rightHand.forward;

        // Draw ray
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, origin + direction * rayLength);
        }

        // Raycast against UI
        if (Physics.Raycast(origin, direction, out RaycastHit hit, rayLength, uiLayer))
        {
            // Hit something on UI layer
            Button btn = hit.collider.GetComponentInParent<Button>();

            if (btn != null && btn.gameObject != currentHover)
            {
                // Unhover previous
                UnhoverCurrent();
                currentHover = btn.gameObject;
                SetLineColor(hoverColor);
            }

            // Show dot
            if (hitDotInstance != null)
            {
                hitDotInstance.SetActive(true);
                hitDotInstance.transform.position = hit.point;
            }

            // Trigger click on index trigger press
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                if (btn != null)
                {
                    btn.onClick.Invoke();
                    Debug.Log($"[RayPointer] Clicked: {btn.gameObject.name}");
                }
            }
        }
        else
        {
            UnhoverCurrent();
            SetLineColor(defaultColor);

            if (hitDotInstance != null)
                hitDotInstance.SetActive(false);
        }
    }

    private void UnhoverCurrent()
    {
        currentHover = null;
    }

    private void SetLineColor(Color color)
    {
        if (lineRenderer == null) return;
        lineRenderer.startColor = color;
        lineRenderer.endColor   = new Color(color.r, color.g, color.b, 0f); // fade out at tip
    }

    private void SetupLineRenderer()
    {
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        lineRenderer.positionCount    = 2;
        lineRenderer.startWidth       = 0.005f;
        lineRenderer.endWidth         = 0.002f;
        lineRenderer.useWorldSpace    = true;
        lineRenderer.material         = new Material(Shader.Find("Unlit/Color"));
        lineRenderer.material.color   = defaultColor;
        SetLineColor(defaultColor);
    }
}

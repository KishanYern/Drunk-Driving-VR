using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Casts a ray from the right Touch controller and interacts with World Space UI buttons.
///
/// Robust against customised OVR rigs (e.g. ones that have extra
/// "*Detached" anchors). On Start we:
///   1. Find the live RightHandAnchor (preferring the property on OVRCameraRig,
///      falling back to a name search inside the rig).
///   2. Reparent THIS GameObject under that anchor and zero its local pose.
///   3. Use OUR transform to draw the line — guaranteed to follow the controller.
///
/// If no anchor is found (e.g. OpenXR rig), we read controller pose from
/// OVRInput each frame as a fallback.
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

    [Header("Controller")]
    [Tooltip("Which Touch controller drives the ray.")]
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;

    // ------------------------------------------------------------------ //

    private Transform handAnchor;        // the live tracked anchor we parented to
    private Transform trackingSpace;     // used by the OVRInput fallback path
    private bool useOvrInputFallback;    // true when no anchor was found
    private GameObject currentHover;
    private GameObject hitDotInstance;

    void Start()
    {
        AttachToHand();

        SetupLineRenderer();

        if (hitDotPrefab != null)
        {
            hitDotInstance = Instantiate(hitDotPrefab);
            hitDotInstance.SetActive(false);
        }
    }

    /// <summary>
    /// Find the live right-hand anchor and parent ourselves to it so our
    /// transform.position / transform.forward exactly match the controller.
    /// </summary>
    private void AttachToHand()
    {
        OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();

        // Preferred: the property on the rig
        if (rig != null && rig.rightHandAnchor != null)
        {
            handAnchor = rig.rightHandAnchor;
            trackingSpace = rig.trackingSpace;
        }

        // Fallback: search by exact name inside the rig (skip *Detached etc.)
        if (handAnchor == null && rig != null)
        {
            foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "RightHandAnchor")
                {
                    handAnchor = t;
                    break;
                }
                if (trackingSpace == null && t.name == "TrackingSpace")
                    trackingSpace = t;
            }
        }

        // Last fallback: any GameObject called "RightHandAnchor" anywhere in scene
        if (handAnchor == null)
        {
            GameObject byName = GameObject.Find("RightHandAnchor");
            if (byName != null) handAnchor = byName.transform;
        }

        if (handAnchor != null)
        {
            transform.SetParent(handAnchor, worldPositionStays: false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            useOvrInputFallback = false;
            Debug.Log($"[RayPointer] Attached under {handAnchor.name}.");
        }
        else
        {
            // No anchor — drive ourselves from OVRInput each frame.
            useOvrInputFallback = true;
            Debug.LogWarning("[RayPointer] No RightHandAnchor found — using OVRInput fallback.");
        }
    }

    void Update()
    {
        // If we're using the fallback, drive OUR transform from OVRInput so the
        // rest of the code below is identical to the parented case.
        if (useOvrInputFallback)
        {
            Vector3 localPos = OVRInput.GetLocalControllerPosition(controller);
            Quaternion localRot = OVRInput.GetLocalControllerRotation(controller);

            if (trackingSpace != null)
            {
                transform.position = trackingSpace.TransformPoint(localPos);
                transform.rotation = trackingSpace.rotation * localRot;
            }
            else
            {
                transform.localPosition = localPos;
                transform.localRotation = localRot;
            }
        }

        Vector3 origin    = transform.position;
        Vector3 direction = transform.forward;

        // Draw ray
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, origin + direction * rayLength);
        }

        // Raycast against UI
        if (Physics.Raycast(origin, direction, out RaycastHit hit, rayLength, uiLayer))
        {
            Button btn = hit.collider.GetComponentInParent<Button>();

            if (btn != null && btn.gameObject != currentHover)
            {
                UnhoverCurrent();
                currentHover = btn.gameObject;
                SetLineColor(hoverColor);
            }

            if (hitDotInstance != null)
            {
                hitDotInstance.SetActive(true);
                hitDotInstance.transform.position = hit.point;
            }

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controller))
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

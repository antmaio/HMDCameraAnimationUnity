using System.Collections.Generic;
using Unity.InferenceEngine;
using UnityEngine;

/// <summary>
/// Receives 17 YOLO pose keypoints from Python via OSC and renders them as spheres.
/// Coordinates are already in Unity space — no conversion needed.
/// Supports expressing positions in a reference space, a spatial anchor space,
/// or a named scene anchor space (via SceneReferenceFrame).
/// </summary>
[RequireComponent(typeof(OscReceiver))]
public class YoloPoseVisualizer : MonoBehaviour
{
    public static YoloPoseVisualizer Instance { get; private set; }

    [Header("Visualization")]
    public float sphereRadius = 0.04f;
    public Color keypointColor = Color.green;

    [Tooltip("If checked, hides all pose visualizations (spheres and bones) immediately.")]
    public bool hideAll = false;

    [Tooltip("If a keypoint arrives as (0,0,0) it is considered invalid and the sphere is hidden.")]
    public bool hideInvalidKeypoints = true;
    private bool _positionsDirty = false;

    [Header("Skeleton")]
    [Tooltip("Draw lines between connected keypoints (COCO skeleton).")]
    public bool drawSkeleton = true;
    public Color boneColor = new Color(1f, 1f, 1f, 0.6f);
    public float boneWidth = 0.012f;

    // ── Coordinate Space Settings ──────────────────────────────────

    [Header("Coordinate Space Settings")]

    [Tooltip("Express keypoints relative to a transform from the list below.")]
    public bool expressInReferenceSpace = false;

    [Tooltip("Express keypoints in a single OVRSpatialAnchor's local space.")]
    public bool expressInAnchorSpace = false;

    [Tooltip("Express keypoints in a named scene anchor space via SceneReferenceFrame.")]
    public bool expressInSceneSpace = false;

    [Tooltip("Name of the anchor inside SceneReferenceFrame to use as the active scene space.")]
    public string activeSceneAnchorName = "";

    [Tooltip("SceneReferenceFrame that holds named scene anchors (same as PassthroughSnapshot).")]
    public SceneReferenceFrame sceneReferenceFrame;

    [Tooltip("Assign the OVRSpatialAnchor that defines the anchor space.")]
    public OVRSpatialAnchor anchorSource;

    [Tooltip("Available reference transforms (manual list).")]
    public List<Transform> availableReferenceSpaces = new List<Transform>();

    [Tooltip("Which index from the list above is the active reference space?")]
    public int activeSpaceIndex = 0;

    /// <summary>Currently active reference space transform (from the list), or null.</summary>
    public Transform referenceSpace
    {
        get
        {
            if (availableReferenceSpaces != null &&
                activeSpaceIndex >= 0 &&
                activeSpaceIndex < availableReferenceSpaces.Count)
                return availableReferenceSpaces[activeSpaceIndex];
            return null;
        }
    }

    // ── COCO Skeleton ──────────────────────────────────────────────

    private static readonly int[,] Bones =
    {
        {0,1},{0,2},{1,3},{2,4},
        {5,6},{5,7},{7,9},{6,8},{8,10},
        {5,11},{6,12},{11,12},
        {11,13},{13,15},{12,14},{14,16}
    };

    private static readonly string[] KeypointNames =
    {
        "Nose","L_Eye","R_Eye","L_Ear","R_Ear",
        "L_Shoulder","R_Shoulder","L_Elbow","R_Elbow","L_Wrist","R_Wrist",
        "L_Hip","R_Hip","L_Knee","R_Knee","L_Ankle","R_Ankle"
    };

    // ── Private State ──────────────────────────────────────────────

    private GameObject[] _spheres;
    private LineRenderer[] _bones;
    private Material _sphereMat;
    private Material _boneMat;
    private Vector3[] _positions;
    public Vector3[] KeypointPositions => _positions; // Expose positions read-only;

    private bool _anchorReady = false;

    // ── Unity Lifecycle ────────────────────────────────────────────

    private void OnValidate()
    {
        if (availableReferenceSpaces != null && availableReferenceSpaces.Count > 0)
            activeSpaceIndex = Mathf.Clamp(activeSpaceIndex, 0, availableReferenceSpaces.Count - 1);
        else
            activeSpaceIndex = 0;

        _positionsDirty = true; // Force refresh if toggled in editor while playing
    }

    void Awake()
    {
        Instance = this;
        _positions = new Vector3[Constants.HMR_JOINTS_SIZE];
        BuildMaterials();
        BuildSpheres();
        if (drawSkeleton) BuildBones();
        GetComponent<OscReceiver>().OnFloatsReceived += OnFloatsReceived;
    }

    private void Update()
    {
        // Poll until the single spatial anchor is localized
        if (!expressInAnchorSpace || _anchorReady) return;

        if (anchorSource == null)
            anchorSource = FindObjectOfType<OVRSpatialAnchor>();

        if (anchorSource != null && anchorSource.Localized)
        {
            _anchorReady = true;
            Debug.Log($"[YoloPoseVisualizer] Anchor localized (UUID: {anchorSource.Uuid}).");
        }
    }

    private void FixedUpdate()
    {
        if (!_positionsDirty) return;
        _positionsDirty = false;
        ApplyPositions(Constants.HMR_JOINTS_SIZE);
    }

    void OnDestroy()
    {
        if (GetComponent<OscReceiver>() != null)
            GetComponent<OscReceiver>().OnFloatsReceived -= OnFloatsReceived;

        Destroy(_sphereMat);
        Destroy(_boneMat);
    }

    // ── Coordinate Resolution ──────────────────────────────────────

    /// <summary>
    /// Priority: Scene space > Anchor space > Manual reference space > World space.
    /// </summary>
    private Transform ResolveReferenceFrame()
    {
        // 1. Scene space — named anchor inside SceneReferenceFrame
        if (expressInSceneSpace && sceneReferenceFrame != null && sceneReferenceFrame.IsReady)
        {
            if (!string.IsNullOrEmpty(activeSceneAnchorName) &&
                sceneReferenceFrame.ActiveFrames.TryGetValue(activeSceneAnchorName, out Transform sceneAnchor))
            {
                return sceneAnchor;
            }

            Debug.LogWarning($"[YoloPoseVisualizer] Scene anchor '{activeSceneAnchorName}' not found in SceneReferenceFrame. " +
                             $"Available: [{string.Join(", ", sceneReferenceFrame.ActiveFrames.Keys)}]");
        }

        // 2. Single OVRSpatialAnchor space
        if (expressInAnchorSpace && _anchorReady && anchorSource != null)
            return anchorSource.transform;

        // 3. Manual reference space from list
        if (expressInReferenceSpace && referenceSpace != null)
            return referenceSpace;

        // 4. World space — no transform needed
        return null;
    }

    /// <summary>
    /// Converts a local-space position into Unity world space using the resolved frame.
    /// </summary>
    private Vector3 ResolveWorldPosition(Vector3 localPos)
    {
        Transform frame = ResolveReferenceFrame();
        return frame != null ? frame.TransformPoint(localPos) : localPos;
    }

    // ── OSC Callback ───────────────────────────────────────────────

    private void OnFloatsReceived(float[] floats)
    {
        // Block incoming data if anchor space is selected but not yet ready
        if (expressInAnchorSpace && !_anchorReady) return;

        // Block if scene space is selected but SceneReferenceFrame not ready
        if (expressInSceneSpace && (sceneReferenceFrame == null || !sceneReferenceFrame.IsReady)) return;

        int available = Mathf.Min(Constants.HMR_JOINTS_SIZE, floats.Length / 3);
        for (int i = 0; i < available; i++)
        {
            Vector3 incoming = new Vector3(floats[i * 3],
                                           floats[i * 3 + 1],
                                           floats[i * 3 + 2]);
            _positions[i] = ResolveWorldPosition(incoming);
        }
        _positionsDirty = true;
    }

    // ── Position Update ────────────────────────────────────────────

    private void ApplyPositions(int count)
    {
        // 1. Update spheres and apply hideAll flag
        for (int i = 0; i < Constants.HMR_JOINTS_SIZE; i++)
        {
            bool valid = !hideAll && i < count && !(hideInvalidKeypoints && _positions[i] == Vector3.zero);
            _spheres[i].SetActive(valid);
            if (valid) _spheres[i].transform.position = _positions[i];
        }

        // 2. Hide bones if skeleton is disabled or master hidden
        if (!drawSkeleton || hideAll)
        {
            if (hideAll && _bones != null)
            {
                foreach (var bone in _bones)
                    if (bone != null) bone.gameObject.SetActive(false);
            }
            return;
        }

        // 3. Update active bones
        int boneCount = Bones.GetLength(0);
        for (int b = 0; b < boneCount; b++)
        {
            int a1 = Bones[b, 0];
            int a2 = Bones[b, 1];
            bool visible = _spheres[a1].activeSelf && _spheres[a2].activeSelf;
            _bones[b].gameObject.SetActive(visible);
            if (visible)
            {
                _bones[b].SetPosition(0, _positions[a1]);
                _bones[b].SetPosition(1, _positions[a2]);
            }
        }
    }

    // ── Scene Construction ─────────────────────────────────────────

    private void BuildSpheres()
    {
        _spheres = new GameObject[Constants.HMR_JOINTS_SIZE];
        for (int i = 0; i < Constants.HMR_JOINTS_SIZE; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = KeypointNames[i];
            go.transform.SetParent(transform);
            go.transform.localScale = Vector3.one * (sphereRadius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = _sphereMat;
            Destroy(go.GetComponent<SphereCollider>());
            go.SetActive(false);
            _spheres[i] = go;
        }
    }

    private void BuildBones()
    {
        int boneCount = Bones.GetLength(0);
        _bones = new LineRenderer[boneCount];

        for (int b = 0; b < boneCount; b++)
        {
            var go = new GameObject($"Bone_{Bones[b, 0]}_{Bones[b, 1]}");
            go.transform.SetParent(transform);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = _boneMat;
            lr.startWidth = boneWidth;
            lr.endWidth = boneWidth;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            go.SetActive(false);
            _bones[b] = lr;
        }
    }

    private void BuildMaterials()
    {
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color");

        _sphereMat = new Material(unlit) { color = keypointColor };
        _boneMat = new Material(unlit) { color = boneColor };
    }

    // ── Public Helpers ─────────────────────────────────────────────

    /// <summary>
    /// Helper method to easily toggle visualization from a UI Button or external script.
    /// Example usage: toggle.onValueChanged.AddListener(YoloPoseVisualizer.Instance.ToggleVisualization)
    /// </summary>
    public void ToggleVisualization(bool isVisible)
    {
        hideAll = !isVisible;
        _positionsDirty = true; // Force ApplyPositions on next FixedUpdate
    }
}
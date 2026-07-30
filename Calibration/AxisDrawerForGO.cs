using System.Collections.Generic;
using UnityEngine;

public class AxisDrawerForGO : MonoBehaviour
{
    [Header("Axis Settings")]
    public float axisLength = 1f;

    [Header("Sphere Settings")]
    public bool drawSphere = false;
    public float sphereRadius = 0.03f;
    public Color sphereColor = Color.white;

    [SerializeField] private Shader lineShader;

    [Header("Link to Guardian Script (Optional)")]
    [Tooltip("If left empty, axes will be drawn at the GO's current transform. " +
             "If assigned, the GO will be moved to the Guardian center.")]
    public InitGuardianCOM guardianSource;

    private bool _hasDrawn = false;
    private List<GameObject> _drawnObjects = new List<GameObject>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (guardianSource == null)
        {
            // No external source: draw using whatever transform the GO already has.
            DrawVisualization();
        }
    }

    void OnEnable()
    {
        if (guardianSource != null)
        {
            guardianSource.OnBarycenterCalculated += OnPositionReceived;
            guardianSource.OnOrientationCalculated += OnOrientationReceived;
        }
    }

    void OnDisable()
    {
        if (guardianSource != null)
        {
            guardianSource.OnBarycenterCalculated -= OnPositionReceived;
            guardianSource.OnOrientationCalculated -= OnOrientationReceived;
        }
    }

    // ── Guardian callbacks ────────────────────────────────────────────────────

    private void OnPositionReceived(Vector3 worldPosition)
    {
        // Move the GO itself; visuals follow because they are children in local space.
        transform.position = worldPosition;
        TryDraw();
    }

    private void OnOrientationReceived(Quaternion worldRotation)
    {
        // Rotate the GO itself; local-space children rotate with it automatically.
        transform.rotation = worldRotation;
        TryDraw();
    }

    private void TryDraw()
    {
        // Wait until both position and rotation have been received at least once.
        // Once drawn, any subsequent callback just moves/rotates the GO and the
        // children follow — no redraw needed.
        if (!_hasDrawn)
            DrawVisualization();
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    private void DrawVisualization()
    {
        if (_hasDrawn) CleanupExisting();

        // Sphere at the GO's own origin (local zero).
        if (drawSphere)
            CreateSphere("Origin_Sphere", sphereColor, sphereRadius);

        // Lines along local axes. useWorldSpace = false so they move and rotate
        // with the GO's transform automatically — no manual update ever needed.
        CreateLine("X_Axis", Vector3.right * axisLength, Color.red);
        CreateLine("Y_Axis", Vector3.up * axisLength, Color.green);
        CreateLine("Z_Axis", Vector3.forward * axisLength, Color.blue);

        _hasDrawn = true;

        //Debug.Log($"[AxisDrawerFromGO] Drawn at GO transform — " +
        //          $"pos: {transform.position:F3}, euler: {transform.eulerAngles:F1}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void CleanupExisting()
    {
        foreach (GameObject obj in _drawnObjects)
            if (obj != null) Destroy(obj);
        _drawnObjects.Clear();
        _hasDrawn = false;
    }

    private void CreateSphere(string objName, Color color, float radius)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = objName;
        sphere.transform.SetParent(transform);
        sphere.transform.localPosition = Vector3.zero;   // sits at the GO's origin
        sphere.transform.localRotation = Quaternion.identity;
        sphere.transform.localScale = Vector3.one * (radius * 2f);

        Destroy(sphere.GetComponent<SphereCollider>());

        Shader shader = lineShader != null ? lineShader : Shader.Find("Unlit/Color");
        Material mat = new Material(shader) { color = color };
        sphere.GetComponent<MeshRenderer>().sharedMaterial = mat;

        _drawnObjects.Add(sphere);
    }

    private void CreateLine(string objName, Vector3 localEnd, Color color)
    {
        GameObject obj = new GameObject(objName);
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;

        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;                        // local space — follows GO
        lr.positionCount = 2;
        lr.SetPositions(new Vector3[] { Vector3.zero, localEnd });
        lr.startWidth = 0.01f;
        lr.endWidth = 0.01f;

        Shader shader = lineShader != null ? lineShader : Shader.Find("Unlit/Color");
        lr.material = new Material(shader) { color = color };

        _drawnObjects.Add(obj);
    }
}
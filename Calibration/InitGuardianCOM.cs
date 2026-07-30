using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class InitGuardianCOM : MonoBehaviour
{
    public System.Action<Vector3> OnBarycenterCalculated;
    public System.Action<Quaternion> OnOrientationCalculated;

    [Header("Settings")]
    public Transform trackingSpaceOrigin;

    [Tooltip("If true, the COM is computed once at startup. If false, it is recomputed every Update().")]
    public bool computeCOMOnce = true;

    [Tooltip(
        "Appends the Guardian COM transform to a JSON file every time it is (re)computed. " +
        "If computeCOMOnce is checked, this produces a single entry. " +
        "If unchecked, a new entry is written on every Update() recomputation so you can " +
        "inspect drift over time.")]
    public bool saveGuardianCOMOverTime = false;

    [Header("Boundary Markers")]
    public float markerRadius = 0.05f;

    [Tooltip("The color spectrum for the markers. Sphere 0 will be the left color, and the last sphere will be the right color.")]
    public Gradient markerGradient;

    // ── Serialisation types ───────────────────────────────────────────────────

    [System.Serializable]
    public class COMSample
    {
        public string timestamp;           // wall-clock instant of the sample
        public float elapsed_seconds;     // seconds since session start
        public int sample_index;

        public Vector3 position;
        public Quaternion rotation;
        public Vector3 eulerAngles;
    }

    [System.Serializable]
    private class COMLog
    {
        public string session_start;
        public string compute_mode;   // "once" | "continuous"
        public List<COMSample> samples = new List<COMSample>();
    }

    // ── Private state ─────────────────────────────────────────────────────────

    private List<GameObject> _spawnedMarkers = new List<GameObject>();
    private bool _initialized = false;

    private COMLog _log;
    private string _logPath;
    private float _sessionStartTime;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (trackingSpaceOrigin == null)
        {
            Debug.LogError("[GuardianDebug] Tracking Space Origin is not assigned!");
            return;
        }

        if (markerGradient == null || markerGradient.colorKeys.Length <= 1)
            markerGradient = CreateDefaultGradient();

        // Initialise the log even if saveGuardianCOMOverTime is false right now,
        // so toggling it at runtime mid-session still produces a coherent file.
        _sessionStartTime = Time.realtimeSinceStartup;
        _log = new COMLog
        {
            session_start = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            compute_mode = computeCOMOnce ? "once" : "continuous"
        };

        string fileName = $"GuardianCOM_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
        _logPath = Path.Combine(Application.persistentDataPath, fileName);

        StartCoroutine(InitializeAfterFrame());
    }

    void Update()
    {
        if (computeCOMOnce || !_initialized) return;

        Vector3[] localPoints = FetchBoundaryPoints();
        if (localPoints != null && localPoints.Length > 0)
            InitializeGuardian(localPoints);
    }

    // ── Initialisation coroutine ──────────────────────────────────────────────

    private IEnumerator InitializeAfterFrame()
    {
        if (OVRManager.instance == null)
        {
            Debug.LogError("[GuardianDebug] OVRManager instance not found.");
            yield break;
        }

        float timeout = 10f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;

            if (!OVRManager.boundary.GetConfigured()) continue;

            Vector3[] localPoints = FetchBoundaryPoints();
            if (localPoints != null && localPoints.Length > 0)
            {
                InitializeGuardian(localPoints);
                _initialized = true;
                yield break;
            }
        }

        Debug.LogWarning("[GuardianDebug] Timed out waiting for boundary.");
    }

    // ── Core helpers ──────────────────────────────────────────────────────────

    private Vector3[] FetchBoundaryPoints()
    {
        Vector3[] points = OVRManager.boundary.GetGeometry(OVRBoundary.BoundaryType.OuterBoundary);
        if (points == null || points.Length == 0)
            points = OVRManager.boundary.GetGeometry(OVRBoundary.BoundaryType.PlayArea);
        return points;
    }

    private void InitializeGuardian(Vector3[] localBoundaryPoints)
    {
        var worldBoundaryPoints = new List<Vector3>();
        Vector3 sum = Vector3.zero;

        foreach (Vector3 localPt in localBoundaryPoints)
        {
            Vector3 worldPt = trackingSpaceOrigin.TransformPoint(localPt);
            worldBoundaryPoints.Add(worldPt);
            sum += worldPt;
        }

        Vector3 worldBarycenter = sum / worldBoundaryPoints.Count;
        Quaternion orientation = ComputeGuardianRotation(worldBoundaryPoints);

        transform.position = worldBarycenter;
        transform.rotation = orientation;

        SpawnBoundaryMarkers(worldBoundaryPoints);
        OnBarycenterCalculated?.Invoke(worldBarycenter);
        OnOrientationCalculated?.Invoke(orientation);

        // Save after the transform has been applied so we log the definitive values.
        if (saveGuardianCOMOverTime)
            AppendAndFlushSample();
    }

    // ── Logging ───────────────────────────────────────────────────────────────

    private void AppendAndFlushSample()
    {
        var sample = new COMSample
        {
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            elapsed_seconds = Time.realtimeSinceStartup - _sessionStartTime,
            sample_index = _log.samples.Count,

            position = transform.position,
            rotation = transform.rotation,
            eulerAngles = transform.eulerAngles
        };

        _log.samples.Add(sample);

        // Keep compute_mode in sync in case the inspector value was changed at runtime.
        _log.compute_mode = computeCOMOnce ? "once" : "continuous";

        try
        {
            File.WriteAllText(_logPath, JsonUtility.ToJson(_log, true));
            Debug.Log($"[GuardianCOM] Sample #{sample.sample_index} saved → {_logPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GuardianCOM] Failed to write log: {e.Message}");
        }
    }

    // ── Rotation helpers ──────────────────────────────────────────────────────

    private Quaternion ComputeGuardianRotation(List<Vector3> worldPoints)
    {
        int count = worldPoints.Count;
        Vector3 bestDir = Vector3.forward;
        float longestSq = -1f;

        for (int i = 0; i < count; i++)
        {
            Vector3 a = worldPoints[i];
            Vector3 b = worldPoints[(i + 1) % count];
            Vector3 edge = new Vector3(b.x - a.x, 0f, b.z - a.z);
            float lenSq = edge.sqrMagnitude;

            if (lenSq > longestSq)
            {
                longestSq = lenSq;
                bestDir = edge.normalized;
            }
        }

        return Quaternion.LookRotation(bestDir, Vector3.up);
    }

    // ── Marker helpers ────────────────────────────────────────────────────────

    private void SpawnBoundaryMarkers(List<Vector3> worldBoundaryPoints)
    {
        foreach (var marker in _spawnedMarkers) Destroy(marker);
        _spawnedMarkers.Clear();

        int count = worldBoundaryPoints.Count;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color");

        for (int i = 0; i < count; i++)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"Marker_{i}";
            marker.transform.position = worldBoundaryPoints[i];
            marker.transform.localScale = Vector3.one * (markerRadius * 2f);
            marker.transform.SetParent(transform);

            Destroy(marker.GetComponent<SphereCollider>());

            float t = (count > 1) ? (float)i / (count - 1) : 0f;
            Material mat = new Material(shader) { color = markerGradient.Evaluate(t) };
            marker.GetComponent<MeshRenderer>().sharedMaterial = mat;

            _spawnedMarkers.Add(marker);
        }
    }

    private Gradient CreateDefaultGradient()
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.blue, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return g;
    }
}
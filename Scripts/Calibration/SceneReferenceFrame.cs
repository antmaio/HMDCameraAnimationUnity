using System.Collections;
using System.Collections.Generic;
//using System.Diagnostics;
using Meta.XR.MRUtilityKit;
using UnityEngine;

/// <summary>
/// For every checked AnchorTarget, spawns a child GameObject that is:
///   • positioned at that anchor's world position
///   • oriented with a deterministic, room-grounded rotation (rules below)
///   • equipped with an AxisDrawerForGO so the frame is visible in the scene
///
/// ┌─────────────────┬────────────────────────────────────────────────────────┐
/// │ Target          │ Z-axis rule (Y = Vector3.up, X = Cross(Z,Y) always)   │
/// ├─────────────────┼────────────────────────────────────────────────────────┤
/// │ Floor           │ Longest wall inward normal, projected horizontal        │
/// │ LongestWall     │ Longest wall inward normal, projected horizontal        │
/// │ NearestWall     │ Longest wall inward normal, projected horizontal        │
/// ├─────────────────┼────────────────────────────────────────────────────────┤
/// │ Table           │ Longest PlaneRect edge of that table, projected horiz. │
/// │ Couch           │ Longest PlaneRect edge of that couch, projected horiz. │
/// ├─────────────────┼────────────────────────────────────────────────────────┤
/// │ Ceiling         │ Longest PlaneRect edge of the ceiling, proj. horiz.    │
/// └─────────────────┴────────────────────────────────────────────────────────┘
///
/// The spawned transforms are exposed via ActiveFrames for other scripts
/// (e.g. PassthroughSnapshot) to use as reference spaces.
/// </summary>
public class SceneReferenceFrame : MonoBehaviour
{
    // ── Target identifiers (used internally to route orientation logic) ───────

    private enum AnchorType { Floor, Ceiling, LongestWall, NearestWall, Table, Couch }

    // ── Inspector checkboxes ──────────────────────────────────────────────────

    [System.Serializable]
    public struct AnchorSelection
    {
        public bool floor;
        public bool ceiling;
        public bool longestWall;
        public bool nearestWall;
        public bool table;
        public bool couch;
    }

    [Header("Select anchor targets (one GO per checked item)")]
    public AnchorSelection targets = new AnchorSelection { floor = true };

    // ── Axis visualisation ────────────────────────────────────────────────────

    [Header("Axis Visualisation")]
    public float axisLength = 0.3f;
    public bool drawOriginSphere = true;
    public float sphereRadius = 0.02f;

    // ── Timing ────────────────────────────────────────────────────────────────

    [Header("Timing")]
    public float pollInterval = 0.2f;

    // ── Public API ────────────────────────────────────────────────────────────

    public bool IsReady { get; private set; }
    public System.Action OnFramesReady;

    /// <summary>Anchor-name → spawned Transform. Populated after IsReady.</summary>
    public IReadOnlyDictionary<string, Transform> ActiveFrames => _activeFrames;

    // ── Private ───────────────────────────────────────────────────────────────

    private readonly Dictionary<string, Transform> _activeFrames
        = new Dictionary<string, Transform>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start() => StartCoroutine(WaitForMRUKThenRegister());

    private void OnDestroy()
    {
        if (MRUK.Instance != null)
            MRUK.Instance.SceneLoadedEvent.RemoveListener(OnSceneLoaded);
    }

    // ── MRUK wait ─────────────────────────────────────────────────────────────

    private IEnumerator WaitForMRUKThenRegister()
    {
        while (MRUK.Instance == null)
        {
            Debug.Log("[SceneReferenceFrame] Waiting for MRUK.Instance…");
            yield return new WaitForSeconds(pollInterval);
        }
        Debug.Log("[SceneReferenceFrame] MRUK ready — registering callback.");
        MRUK.Instance.RegisterSceneLoadedCallback(OnSceneLoaded);
    }

    // ── MRUK callback ─────────────────────────────────────────────────────────

    private void OnSceneLoaded()
    {
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        if (room == null)
        {
            Debug.LogError("[SceneReferenceFrame] GetCurrentRoom() returned null.");
            return;
        }

        // Cache the longest wall once — needed by Floor / LongestWall / NearestWall.
        MRUKAnchor longestWall = GetLongestWall(room);

        var selected = CollectSelectedAnchors(room);
        if (selected.Count == 0)
        {
            Debug.LogWarning("[SceneReferenceFrame] No anchor targets are checked.");
            return;
        }

        foreach (var (anchorName, anchor, anchorType) in selected)
        {
            Quaternion rot = BuildOrientation(anchor, anchorType, longestWall);
            SpawnFrame(anchorName, anchor.transform.position, rot);
        }

        IsReady = true;
        OnFramesReady?.Invoke();

        Debug.Log($"<b><color=cyan>[SceneReferenceFrame]</color></b> " +
                  $"Spawned {selected.Count} frame(s): " +
                  $"{string.Join(", ", _activeFrames.Keys)}");
    }

    // ── Orientation dispatcher ────────────────────────────────────────────────

    /// <summary>
    /// Routes to the correct orientation rule for each anchor type.
    /// In all cases: Y = Vector3.up, X = Cross(Z, Y).
    /// Only Z changes per rule.
    /// </summary>
    private static Quaternion BuildOrientation(
        MRUKAnchor anchor,
        AnchorType type,
        MRUKAnchor longestWall)
    {
        Vector3 zAxis;

        switch (type)
        {
            // ── Floor / wall targets: inward normal of the longest wall ────────
            case AnchorType.Floor:
            case AnchorType.LongestWall:
            case AnchorType.NearestWall:
                if (longestWall == null)
                {
                    Debug.LogWarning("[SceneReferenceFrame] No wall found for wall-based " +
                                     $"orientation ({type}). Falling back to identity.");
                    return Quaternion.identity;
                }
                zAxis = Vector3.ProjectOnPlane(
                    longestWall.transform.forward, Vector3.up).normalized;
                break;

            // ── Table / Couch: longest PlaneRect edge of that specific anchor ──
            case AnchorType.Table:
            case AnchorType.Couch:
                zAxis = LongestEdgeDirection(anchor);
                break;

            // ── Ceiling: longest PlaneRect edge of the ceiling anchor ─────────
            case AnchorType.Ceiling:
                zAxis = LongestEdgeDirection(anchor);
                break;

            default:
                return Quaternion.identity;
        }

        return SafeLookRotation(zAxis, type.ToString());
    }

    // ── Orientation helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the horizontal projection of the longer PlaneRect edge direction.
    ///
    ///   PlaneRect.width  maps to  anchor.transform.right
    ///   PlaneRect.height maps to  anchor.transform.up   (in-plane, may be tilted)
    ///
    /// Projecting onto the horizontal plane removes any scan tilt from both.
    /// </summary>
    private static Vector3 LongestEdgeDirection(MRUKAnchor anchor)
    {
        if (!anchor.PlaneRect.HasValue)
        {
            Debug.LogWarning($"[SceneReferenceFrame] '{anchor.name}' has no PlaneRect — " +
                              "falling back to anchor forward.");
            return Vector3.ProjectOnPlane(anchor.transform.forward, Vector3.up).normalized;
        }

        Rect rect = anchor.PlaneRect.Value;
        // Choose the local axis that corresponds to the longer dimension.
        Vector3 edgeDir = rect.width >= rect.height
            ? anchor.transform.right    // width  → local X
            : anchor.transform.up;      // height → local Y (in-plane)

        return Vector3.ProjectOnPlane(edgeDir, Vector3.up).normalized;
    }

    /// <summary>
    /// Builds LookRotation(zAxis, Vector3.up) with a degenerate-axis guard.
    /// </summary>
    private static Quaternion SafeLookRotation(Vector3 zAxis, string label)
    {
        if (zAxis.sqrMagnitude < 1e-6f)
        {
            Debug.LogWarning($"[SceneReferenceFrame] Z axis for '{label}' collapsed to zero " +
                              "(surface may be perfectly vertical or horizontal). " +
                              "Falling back to identity.");
            return Quaternion.identity;
        }
        return Quaternion.LookRotation(zAxis, Vector3.up);
    }

    // ── Frame spawning ────────────────────────────────────────────────────────

    private void SpawnFrame(string frameName, Vector3 worldPos, Quaternion worldRot)
    {
        GameObject go = new GameObject($"Frame_{frameName}");
        go.transform.SetParent(transform);
        go.transform.position = worldPos;
        go.transform.rotation = worldRot;

        AxisDrawerForGO drawer = go.AddComponent<AxisDrawerForGO>();
        drawer.axisLength = axisLength;
        drawer.drawSphere = drawOriginSphere;
        drawer.sphereRadius = sphereRadius;

        _activeFrames[frameName] = go.transform;

        Debug.Log($"[SceneReferenceFrame] Spawn frame {frameName} at {worldPos}");

    }

    // ── Anchor collection ─────────────────────────────────────────────────────

    private List<(string, MRUKAnchor, AnchorType)> CollectSelectedAnchors(MRUKRoom room)
    {
        var list = new List<(string, MRUKAnchor, AnchorType)>();

        void TryAdd(bool enabled, string name, AnchorType type, MRUKAnchor anchor)
        {
            if (!enabled) return;
            if (anchor == null)
            {
                Debug.LogWarning($"[SceneReferenceFrame] '{name}' checked but anchor not found.");
                return;
            }
            list.Add((name, anchor, type));
        }

        TryAdd(targets.floor, "Floor", AnchorType.Floor, room.FloorAnchor);
        TryAdd(targets.ceiling, "Ceiling", AnchorType.Ceiling, room.CeilingAnchor);
        TryAdd(targets.longestWall, "LongestWall", AnchorType.LongestWall, GetLongestWall(room));
        TryAdd(targets.nearestWall, "NearestWall", AnchorType.NearestWall, GetNearestWall(room));
        TryAdd(targets.table, "Table", AnchorType.Table, GetFirstByLabel(room, MRUKAnchor.SceneLabels.TABLE));
        TryAdd(targets.couch, "Couch", AnchorType.Couch, GetFirstByLabel(room, MRUKAnchor.SceneLabels.COUCH));

        return list;
    }

    // ── MRUK helpers ──────────────────────────────────────────────────────────

    private static MRUKAnchor GetLongestWall(MRUKRoom room)
    {
        MRUKAnchor best = null;
        float longest = -1f;
        foreach (MRUKAnchor wall in room.WallAnchors)
        {
            float w = wall.PlaneRect.HasValue ? wall.PlaneRect.Value.width : 0f;
            if (w > longest + 1e-4f) { longest = w; best = wall; }
        }
        return best;
    }

    private static MRUKAnchor GetNearestWall(MRUKRoom room)
    {
        Vector3 reference = room.FloorAnchor != null
            ? room.FloorAnchor.transform.position : Vector3.zero;
        MRUKAnchor best = null;
        float nearest = float.MaxValue;
        foreach (MRUKAnchor wall in room.WallAnchors)
        {
            float d = Vector3.Distance(wall.transform.position, reference);
            if (d < nearest) { nearest = d; best = wall; }
        }
        return best;
    }

    private static MRUKAnchor GetFirstByLabel(MRUKRoom room, MRUKAnchor.SceneLabels label)
    {
        foreach (MRUKAnchor a in room.Anchors)
            if (a.HasAnyLabel(label)) return a;
        return null;
    }
}
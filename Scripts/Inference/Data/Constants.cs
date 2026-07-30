using UnityEngine;

/// <summary>
/// Centralizes size/shape constants and derived values used to build the neural
/// network's input feature vector. Compile-time constants describe fixed model
/// dimensions (joint counts, hidden state size, etc.), while a small set of
/// scene-configurable values (via the singleton instance) let derived sizes like
/// <see cref="INPUT_SIZE"/> adapt to inspector settings without hardcoding them.
/// </summary>
public class Constants : MonoBehaviour
{
    // True compile-time constants unaffected by any of this
    public const int HMR_JOINTS_SIZE = 17;
    public const int SMPLX_JOINTS_SIZE = 21;
    public const int HIDDEN_STATE_SIZE = 256;
    public const int INPUT_MOTION_FEATURES_SIZE = 360;
    public const int NBLOCK = 2;
    public const int ROT_FEAT_BY_JOINT = 6;

    [SerializeField]
    private bool enableCartesianPosition = false;

    private static Constants _instance;

    private static Constants Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<Constants>();
            if (_instance == null)
                Debug.LogError("No Constants component found in the scene.");
            return _instance;
        }
    }

    private void Awake() => _instance = this;

    public static bool ENABLE_CARTESIAN_POSITION => Instance.enableCartesianPosition;
    public static int N_ADDITIONAL_JOINTS => ENABLE_CARTESIAN_POSITION ? 12 : 0;
    public static int NCHANNEL => 5 + N_ADDITIONAL_JOINTS;
    public static int INPUT_SIZE => NBLOCK * NCHANNEL * 2 * HIDDEN_STATE_SIZE + INPUT_MOTION_FEATURES_SIZE;
}
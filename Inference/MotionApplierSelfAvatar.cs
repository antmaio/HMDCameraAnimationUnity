using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Applies predicted motion data (root positions and body joint rotations) from a <see cref="NeuralNetwork"/>
/// to a child avatar skeleton hierarchy in Unity.
/// </summary>
public class MotionApplierSelfAvatar : MonoBehaviour
{

    private NeuralNetwork neuralNetwork;
    private int nLocalJoints; 
    private Transform[] joints;
    private string ignoreTag = "IgnoreMe";
    private int[] SmplxToUnityJointMapping;
    private UtilsData utils;

    /// <summary>
    /// Toggles verbose console logging for joint indexing and mapping validation.
    /// </summary>
    public bool verbose = true;

    void Start(){

        if (neuralNetwork == null){
            neuralNetwork = GetComponentInParent<NeuralNetwork>();
        }

        //Check if the components are loaded
        if (neuralNetwork == null){
            Debug.LogError("No NeuralNetwork found! Please attach a NeuralNetwork component to this GameObject.");
            return;
        }

        InitializeSkeleton();
        utils = new UtilsData();
        SmplxToUnityJointMapping = utils.CreateSmplxToUnityJointMapping();
    }


    /// <summary>
    /// Traverses all child transforms, caching valid skeleton joints while excluding the root transform 
    /// and any child tagged with the specified <c>ignoreTag</c>.
    /// </summary>
    void InitializeSkeleton()
    {

        List<Transform> jointList = new List<Transform>();
        Transform[] allChildren = GetComponentsInChildren<Transform>(true); // Include inactive

        foreach (Transform child in allChildren)
        {
            // Skip self and check GameObject (not Transform) tag
            if (child != transform && !child.gameObject.CompareTag(ignoreTag))
            {
                jointList.Add(child);
            }

        }

        joints = jointList.ToArray();
        nLocalJoints = joints.Length;

    }

    /// <summary>
    /// Reads inferred root position, global orientation, and joint rotations from the parent <see cref="NeuralNetwork"/> 
    /// and updates the avatar's transform hierarchy frame-by-frame.
    /// </summary>
    public void ApplyMotion()
    {

        // 1. SAFETY CHECK: If the neural network hasn't finished its first inference yet,
        // bodyPoseRotations will be null. Exit early to avoid NullReferenceExceptions.
        Matrix3x3[] bodyPoseRotations = neuralNetwork.GetBodyPoseRotations();
        if (bodyPoseRotations == null || bodyPoseRotations.Length == 0)
        {
            return;
        }

        Vector3 rootPos = neuralNetwork.GetRootPosition();
        Quaternion globalOrient = neuralNetwork.GetGlobalOrientation().ToQuaternion();
        
        transform.position = rootPos;
        transform.rotation = globalOrient;

        //nJoints - 1 because of local rotations, pelvis is excluded
        for (int i = 0; i < nLocalJoints && i < SmplxToUnityJointMapping.Length; i++){
            int smplxIndex = SmplxToUnityJointMapping[i+1];

            if (verbose){
                string smplxJointName = ((UtilsData.SmplxJoints)(smplxIndex)).ToString();
                Debug.Log($"Joints name: {bodyPoseRotations.Length}");
                Debug.Log($"{i} | SMPLX Joint {smplxIndex-1} ({smplxJointName}) mapped to Unity Joint {joints[i].name}");
            }
            joints[i].localRotation = bodyPoseRotations[smplxIndex-1].ToQuaternion();
        }

    }
}
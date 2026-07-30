using UnityEngine;
using System.Linq; // Added for .Take() extension method on Arrays

/// <summary>
/// Collects motion data (positions, rotations, velocities, and angular velocities)
/// from the HMD and hand controllers each frame, converts them into the AMASS axis
/// system and head-relative space, and packs the result into a flat float array
/// suitable for feeding into the pose-estimation neural network. Also provides
/// helpers to wrap that array into an <see cref="Unity.InferenceEngine.Tensor{T}"/>.
/// </summary>
public class TensorData : MonoBehaviour
{
    /// <summary>
    /// If <c>true</c>, enables verbose debug logging of VR state and velocities
    /// (used by the debug code paths in this class).
    /// </summary>
    public bool verbose = true;

    // Previous frame data for velocity calculation
    private Vector3     _prevHmdPos;                    //head pos
    private Vector3     _prevLeftPos;                   // left pos
    private Vector3     _prevRightPos;                  //right pos
    private Matrix3x3   _prevHMDRotations;              //head rot
    private Matrix3x3   _prevLeftRotations;             //left rot
    private Matrix3x3   _prevRightRotations;            //right rot
    private Vector3     _prevLeftPosInHeadSpace;        //left pos in head space
    private Vector3     _prevRightPosInHeadSpace;       //right pos in head space
    private Matrix3x3   _prevLeftRotationsInHeadSpace;  //left rot in head space
    private Matrix3x3   _prevRightRotationsInHeadSpace; //right rot in head space

    /// <summary>
    /// Tracks whether the current call is processing the first frame, in which case
    /// velocities default to zero/identity since there is no previous frame to diff against.
    /// </summary>
    private bool        _isFirstFrame = true;           //flag if first frame

    /// <summary>
    /// Builds the full per-frame motion feature vector used as input to the pose model.
    /// Reads global position/rotation from the HMD and both controllers, converts them
    /// into the AMASS axis system, derives head-relative positions/rotations, computes
    /// linear and angular velocities (zeroed on the first frame), and appends HMR joint/
    /// body-pose placeholders and recurrent hidden states. Also updates the internal
    /// "previous frame" cache for use on the next call.
    /// </summary>
    /// <param name="hmd">The GameObject tracking the headset. May be <c>null</c> (logs a warning; treated as identity/zero).</param>
    /// <param name="leftController">The GameObject tracking the left controller. May be <c>null</c> (logs a warning; treated as identity/zero).</param>
    /// <param name="rightController">The GameObject tracking the right controller. May be <c>null</c> (logs a warning; treated as identity/zero).</param>
    /// <returns>
    /// A flat <see cref="float"/> array of length <see cref="Constants.INPUT_SIZE"/> containing,
    /// in order: global positions/rotations, head-space positions/rotations, linear velocities,
    /// angular velocities, HMR position/body-pose features, and recurrent hidden states.
    /// </returns>
    public float[] GetMotionFeaturesFromGameObjects(GameObject hmd, GameObject leftController, GameObject rightController){
        
        float[] poseData = new float[Constants.INPUT_SIZE];
        int index = 0;
        
        if (hmd == null) Debug.LogWarning("HMD GameObject is null in TensorData.");
        if (leftController == null) Debug.LogWarning("Left Controller GameObject is null in TensorData.");
        if (rightController == null) Debug.LogWarning("Right Controller GameObject is null in TensorData.");

        //Global Positions
        Vector3 hmdPosition               = hmd != null ? hmd.transform.position : Vector3.zero; //head
        Vector3 leftControllerPosition    = leftController != null ? leftController.transform.position : Vector3.zero; //left hand
        Vector3 rightControllerPosition   = rightController != null ? rightController.transform.position : Vector3.zero;//right hand

        //Global Rotations
        Matrix3x3 hmdRotation               = Matrix3x3.FromQuaternion(hmd?.transform.rotation ?? Quaternion.identity); //head 
        Matrix3x3 leftControllerRotation    = Matrix3x3.FromQuaternion(leftController?.transform.rotation ?? Quaternion.identity); //left hand
        Matrix3x3 rightControllerRotation   = Matrix3x3.FromQuaternion(rightController?.transform.rotation ?? Quaternion.identity);//right hand

        //Convert hmd positions and rotations to AMASS axis sytem 
        (hmdPosition, hmdRotation)                          = TransformData.UnityToAMASSAxisSystemFromOffsetGO(hmd.transform);
        (leftControllerPosition, leftControllerRotation)    = TransformData.UnityToAMASSAxisSystemFromOffsetGO(leftController.transform);
        (rightControllerPosition, rightControllerRotation)  = TransformData.UnityToAMASSAxisSystemFromOffsetGO(rightController.transform);


        /*
        T-pose is 
            (1, 0, 0)
            (0, 0, -1)
            (0, 1, 0) 
        */
    
        //Positions in head space
        Matrix4x4 _hmdRotation4x4 = hmdRotation.ToMatrix4x4();
        Matrix4x4 _hmdRotInv      = _hmdRotation4x4.inverse;

        Vector3 leftControllerPositionInHeadSpace              = _hmdRotInv.MultiplyPoint3x4(leftControllerPosition - hmdPosition);//left hand
        Vector3 rightControllerPositionInHeadSpace             = _hmdRotInv.MultiplyPoint3x4(rightControllerPosition - hmdPosition);//right hand

        //Rotations in head space
        Matrix4x4 _leftControllerRotation4x4                    = leftControllerRotation.ToMatrix4x4();
        Matrix4x4 _rightControllerRotation4x4                   = rightControllerRotation.ToMatrix4x4();
        Matrix3x3 LeftControllerRotationsInHeadSpace            = Matrix3x3.FromMatrix4x4(_hmdRotInv * _leftControllerRotation4x4); //left hand
        Matrix3x3 RightControllerRotationsInHeadSpace           = Matrix3x3.FromMatrix4x4(_hmdRotInv * _rightControllerRotation4x4);//right hand

        //Velocities
        Vector3 hmdVelocity, leftControllerVelocity, rightControllerVelocity; //Linear Velocity            
        Matrix3x3 HMDAngularVelocity, LeftAngularVelocity, RightAngularVelocity; //Angular Velocity
        Vector3 leftControllerVelocityInHeadSpace, rightControllerVelocityInHeadSpace; //Linear Velocity in head space
        Matrix3x3 LeftAngularVelocityInHeadSpace, RightAngularVelocityInHeadSpace; //Angular Velocity in head space
        
        if (_isFirstFrame){
            //Linear Velocity
            hmdVelocity = Vector3.zero;
            leftControllerVelocity = Vector3.zero;
            rightControllerVelocity = Vector3.zero;
            //Angular Velocity
            HMDAngularVelocity = Matrix3x3.identity;
            LeftAngularVelocity = Matrix3x3.identity;
            RightAngularVelocity = Matrix3x3.identity;
            //Linear Velocity in head space
            leftControllerVelocityInHeadSpace = Vector3.zero;
            rightControllerVelocityInHeadSpace = Vector3.zero;
            //Angular Velocity in head space
            LeftAngularVelocityInHeadSpace = Matrix3x3.identity;
            RightAngularVelocityInHeadSpace = Matrix3x3.identity;
        }
        else{
            //Linear Velocity
            hmdVelocity             = hmdPosition               - _prevHmdPos; //head
            leftControllerVelocity  = leftControllerPosition    - _prevLeftPos; //left hand
            rightControllerVelocity = rightControllerPosition   - _prevRightPos;//right hand
            //Angular Velocity
            HMDAngularVelocity         = Matrix3x3.FromMatrix4x4(_prevHMDRotations.ToMatrix4x4().inverse          * hmdRotation.ToMatrix4x4());
            LeftAngularVelocity        = Matrix3x3.FromMatrix4x4(_prevLeftRotations.ToMatrix4x4().inverse         * leftControllerRotation.ToMatrix4x4());
            RightAngularVelocity       = Matrix3x3.FromMatrix4x4(_prevRightRotations.ToMatrix4x4().inverse        * rightControllerRotation.ToMatrix4x4());
            //Linear Velocity in head space
            leftControllerVelocityInHeadSpace   = leftControllerPositionInHeadSpace - _prevLeftPosInHeadSpace;
            rightControllerVelocityInHeadSpace  = rightControllerPositionInHeadSpace - _prevRightPosInHeadSpace;
            //Angular Velocity in head space
            LeftAngularVelocityInHeadSpace    = Matrix3x3.FromMatrix4x4(_prevLeftRotationsInHeadSpace.ToMatrix4x4().inverse    * LeftControllerRotationsInHeadSpace.ToMatrix4x4());
            RightAngularVelocityInHeadSpace   = Matrix3x3.FromMatrix4x4(_prevRightRotationsInHeadSpace.ToMatrix4x4().inverse    * RightControllerRotationsInHeadSpace.ToMatrix4x4());

        }

        // Update previous motion features
        //Global positions
        _prevHmdPos = hmdPosition; 
        _prevLeftPos = leftControllerPosition;
        _prevRightPos = rightControllerPosition;
        //Global rotations
        _prevHMDRotations = hmdRotation;
        _prevLeftRotations = leftControllerRotation;
        _prevRightRotations = rightControllerRotation;
        //Positions in head space
        _prevLeftPosInHeadSpace = leftControllerPositionInHeadSpace;
        _prevRightPosInHeadSpace = rightControllerPositionInHeadSpace;
        //Rotations in head space
        _prevLeftRotationsInHeadSpace = LeftControllerRotationsInHeadSpace;
        _prevRightRotationsInHeadSpace = RightControllerRotationsInHeadSpace;

        // ------------
        // Add motion features to float[] poseData 
        // ------------

        // Global positions and rotations
        FillArray.AddPositionToArray(poseData, ref index, hmdPosition);
        FillArray.AddRotationToArray(poseData, ref index, hmdRotation);
        FillArray.AddPositionToArray(poseData, ref index, leftControllerPosition);
        FillArray.AddRotationToArray(poseData, ref index, leftControllerRotation);
        FillArray.AddPositionToArray(poseData, ref index, rightControllerPosition);
        FillArray.AddRotationToArray(poseData, ref index, rightControllerRotation);
        // Head centered positions and rotations 
        FillArray.AddPositionToArray(poseData, ref index, leftControllerPositionInHeadSpace);
        FillArray.AddRotationToArray(poseData, ref index, LeftControllerRotationsInHeadSpace);
        FillArray.AddPositionToArray(poseData, ref index, rightControllerPositionInHeadSpace);
        FillArray.AddRotationToArray(poseData, ref index, RightControllerRotationsInHeadSpace);
        // Linear velocity
        FillArray.AddPositionToArray(poseData, ref index, hmdVelocity);
        FillArray.AddPositionToArray(poseData, ref index, leftControllerVelocity);
        FillArray.AddPositionToArray(poseData, ref index, leftControllerVelocityInHeadSpace);
        FillArray.AddPositionToArray(poseData, ref index, rightControllerVelocity);
        FillArray.AddPositionToArray(poseData, ref index, rightControllerVelocityInHeadSpace);
        // Angular velocity
        FillArray.AddRotationToArray(poseData, ref index, HMDAngularVelocity);
        FillArray.AddRotationToArray(poseData, ref index, LeftAngularVelocity);
        FillArray.AddRotationToArray(poseData, ref index, LeftAngularVelocityInHeadSpace);
        FillArray.AddRotationToArray(poseData, ref index, RightAngularVelocity);
        FillArray.AddRotationToArray(poseData, ref index, RightAngularVelocityInHeadSpace);

        // hmr positions
        FillArray.AddHMRPosition(poseData, ref index);
        // hmr body pose
        FillArray.AddHMRBodyPose(poseData, ref index);
    
        //Add input hidden states
        float[] hiddenStates = NeuralNetwork.GetPrevStates(_isFirstFrame);
        FillArray.AddHiddenStates(poseData, ref index, hiddenStates);

        // Update _isFirstFrame for the next call
        if (_isFirstFrame){
            _isFirstFrame = false;
        }
        
        return poseData;
    }

    /// <summary>
    /// Wraps a flat pose-data float array into a single-batch, single-frame
    /// <see cref="Unity.InferenceEngine.Tensor{T}"/> ready to be passed to the inference engine.
    /// </summary>
    /// <param name="poseData">
    /// The flat feature array to copy into the tensor, typically produced by
    /// <see cref="GetMotionFeaturesFromGameObjects"/>. Only the first
    /// <see cref="Constants.INPUT_SIZE"/> elements (or fewer, if the array is shorter) are copied.
    /// </param>
    /// <returns>
    /// A <see cref="Unity.InferenceEngine.Tensor{T}"/> of shape <c>[1, 1, Constants.INPUT_SIZE]</c>
    /// containing the copied pose data.
    /// </returns>
    public Unity.InferenceEngine.Tensor<float> CreateTensorFromPoseData(float[] poseData){
        // Shape: [1, 1, Constants.INPUT_SIZE] - 1 batch, 1 frame, Constants.INPUT_SIZE features
        var shape = new Unity.InferenceEngine.TensorShape(1, 1, Constants.INPUT_SIZE);
        var tensor = new Unity.InferenceEngine.Tensor<float>(shape);

        // Copy data to tensor
        for (int i = 0; i < Constants.INPUT_SIZE && i < poseData.Length; i++)
        {
            tensor[i] = poseData[i];
        }
        return tensor;
    }
}

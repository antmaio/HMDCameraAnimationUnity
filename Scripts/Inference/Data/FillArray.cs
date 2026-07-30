
using UnityEngine;
using Unity.InferenceEngine;

/// <summary>
/// Static helper for serializing motion-related data (positions, rotations,
/// velocities, HMR keypoints/body pose, and recurrent hidden states) into a flat
/// float array, writing sequentially at a caller-managed index. Used to assemble
/// the input feature vector for the pose-estimation neural network.
/// </summary>
public static class FillArray
{
    /// <summary>
    /// Writes a position's X, Y, Z components into the array at the current index.
    /// </summary>
    /// <param name="array">The destination array to write into.</param>
    /// <param name="index">
    /// The current write position, advanced by 3 (one per component) after the call.
    /// </param>
    /// <param name="position">The position to write.</param>
    public static void AddPositionToArray(float[] array, ref int index, Vector3 position)
    {
        array[index++] = position.x;
        array[index++] = position.y;
        array[index++] = position.z;
    }

    /// <summary>
    /// Writes a linear velocity's X, Y, Z components into the array at the current index.
    /// </summary>
    /// <param name="array">The destination array to write into.</param>
    /// <param name="index">
    /// The current write position, advanced by 3 (one per component) after the call.
    /// </param>
    /// <param name="velocity">The linear velocity to write.</param>
    public static void AddPosVelocityToArray(float[] array, ref int index, Vector3 velocity)
    {
        array[index++] = velocity.x;
        array[index++] = velocity.y;
        array[index++] = velocity.z;
    }

    /// <summary>
    /// Writes all 9 components of a rotation matrix (row-major: m00..m22) into the
    /// array at the current index.
    /// </summary>
    /// <param name="array">The destination array to write into.</param>
    /// <param name="index">
    /// The current write position, advanced by 9 (one per matrix element) after the call.
    /// </param>
    /// <param name="rotation">The rotation matrix to write.</param>
    public static void AddRotationToArray(float[] array, ref int index, Matrix3x3 rotation)
    {
        array[index++] = rotation.m00;
        array[index++] = rotation.m01;
        array[index++] = rotation.m02;
        array[index++] = rotation.m10;
        array[index++] = rotation.m11;
        array[index++] = rotation.m12;
        array[index++] = rotation.m20;
        array[index++] = rotation.m21;
        array[index++] = rotation.m22;
    }

    /// <summary>
    /// Writes all 9 components of an angular velocity matrix (row-major: m00..m22)
    /// into the array at the current index.
    /// </summary>
    /// <param name="array">The destination array to write into.</param>
    /// <param name="index">
    /// The current write position, advanced by 9 (one per matrix element) after the call.
    /// </param>
    /// <param name="rvelocity">The angular velocity matrix to write.</param>
    public static void AddRotVelocityToArray(float[] array, ref int index, Matrix3x3 rvelocity)
    {
        array[index++] = rvelocity.m00;
        array[index++] = rvelocity.m01;
        array[index++] = rvelocity.m02;
        array[index++] = rvelocity.m10;
        array[index++] = rvelocity.m11;
        array[index++] = rvelocity.m12;
        array[index++] = rvelocity.m20;
        array[index++] = rvelocity.m21;
        array[index++] = rvelocity.m22;
    }

    /// <summary>
    /// Writes the HMR (multi-view camera pose estimation) keypoint positions into
    /// the array, converting each keypoint from Unity space into the AMASS axis
    /// system (Unity→AMASS basis change, then Y-up→Z-up remap) to match the
    /// convention used elsewhere in the feature vector. If no
    /// <see cref="YoloPoseVisualizer"/> instance is available, writes zeros for all
    /// joints instead.
    /// </summary>
    /// <param name="array">The destination array to write into.</param>
    /// <param name="index">
    /// The current write position, advanced by <c>Constants.HMR_JOINTS_SIZE * 3</c>
    /// after the call.
    /// </param>
    public static void AddHMRPosition(float[] array, ref int index)
    {
        YoloPoseVisualizer yolo = YoloPoseVisualizer.Instance;

        if (yolo == null)
        {
            Debug.LogWarning("[FillArray] YoloPoseVisualizer instance not found, using zeros.");
            for (int i = 0; i < Constants.HMR_JOINTS_SIZE * 3; i++)
                array[index++] = 0f;
            return;
        }

        Vector3[] kps = yolo.KeypointPositions;

        for (int j = 0; j < Constants.HMR_JOINTS_SIZE; j++)
        {

            Vector3 unityPos = (kps != null && j < kps.Length) ? kps[j] : Vector3.zero;

            // Apply same Unity -> AMASS position conversion as TensorData does for HMD/controllers
            Vector3 amassYup = TransformData.T_UNITYBasis2AMASSBasis.MultiplyPoint(unityPos); // (-x, y, z)
            Vector3 amassZup = TransformData.R_OFFSET_UNITY_TO_AMASS.MultiplyPoint(amassYup); // Z-up remap

            array[index++] = amassZup.x;
            array[index++] = amassZup.y;
            array[index++] = amassZup.z;

        }



    }

    /// <summary>
    /// Writes placeholder HMR body pose data into the array.
    /// </summary>
    /// <remarks>
    /// Not yet implemented against real HMR body-pose estimation output — currently
    /// fills the slot with random values in <c>[-1, 1]</c> for each of the
    /// <c>Constants.SMPLX_JOINTS_SIZE * 9</c> rotation-matrix components. Replace
    /// with real data once available.
    /// </remarks>
    /// <param name="array">The destination array to write into.</param>
    /// <param name="index">
    /// The current write position, advanced by <c>Constants.SMPLX_JOINTS_SIZE * 9</c>
    /// after the call.
    /// </param>
    public static void AddHMRBodyPose(float[] array, ref int index){
        //Debug.LogWarning("AddHMRBodyPose not implemented yet, Random values are used instead");
        for (int i = 0; i < Constants.SMPLX_JOINTS_SIZE * 9; i++)
        {
            array[index++] = UnityEngine.Random.Range(-1f, 1f);
        }
    }

    /// <summary>
    /// Copies a recurrent network's hidden-state values into the array.
    /// </summary>
    /// <param name="array">The destination array to write into.</param>
    /// <param name="index">
    /// The current write position, advanced by <c>hiddenStates.Length</c> after the call.
    /// </param>
    /// <param name="hiddenStates">The hidden-state values to copy in, in order.</param>
    public static void AddHiddenStates(float[] array, ref int index, float[] hiddenStates){
        for (int i = 0; i < hiddenStates.Length; i++){
            array[index++] = hiddenStates[i];
        }
    }
}
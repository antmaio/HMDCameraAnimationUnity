
using UnityEngine;
/// <summary>
/// Provides coordinate system conversion utilities between Unity's left-handed, Y-up
/// axis system and AMASS's right-handed, Z-up axis system. Handles both global and
/// local rotation/position transformations, including chirality flips and up-axis
/// realignment offsets.
/// </summary>
public class TransformData
{
    /// <summary>
    /// The current rotation matrix associated with this transform data instance.
    /// </summary>
    public Matrix3x3 RotationMatrix { get; private set; }
    /// <summary>
    /// The current rotation matrix associated with this transform data instance.
    /// </summary>
    public Matrix3x3 RotationOffset { get; private set; }

    /*Unity to AMASS axis system matrices*/
    /// <summary>
    /// Change-of-basis matrix converting from Unity's basis to AMASS's basis.
    /// Flips chirality by negating the X axis (left-handed → right-handed).
    /// </summary>
    public static readonly Matrix3x3 T_UNITYBasis2AMASSBasis = new Matrix3x3(
        new Vector3(-1, 0, 0),  // First column
        new Vector3(0, 1, 0),   // Second column
        new Vector3(0, 0, 1)   // Third column
    );

    /// <summary>
    /// Change-of-basis matrix converting from AMASS's basis back to Unity's basis.
    /// Computed as the transpose of <see cref="T_UNITYBasis2AMASSBasis"/>.
    /// </summary>
    public static readonly Matrix3x3 T_AMASSBasis2UnityBasis = T_UNITYBasis2AMASSBasis.Transpose3x3();
    /// <summary>
    /// Global orientation offset representing a +90° rotation around the X axis,
    /// used to convert AMASS's Y-up convention to a Z-up convention.
    /// </summary>
    public static readonly Matrix3x3 T_OFFSET_GLOBAL_ORIENT_AMASS = new Matrix3x3(
        new Vector3(1, 0, 0),  // First column (No change)
        new Vector3(0, 0, 1),   // Second column: Y -> Z
        new Vector3(0, -1, 0)   // Third column: Z -> -Y
    );

    /*AMASS to Unity axis system matrix*/
    /// <summary>
    /// Direct axis-remapping matrix converting a full AMASS-space vector/rotation
    /// into Unity space (combines chirality flip and axis reordering in one step).
    /// </summary>
    public static readonly Matrix3x3 T_AMASS2UNITY = new Matrix3x3(
        new Vector3(0, 0, -1),  // First column
        new Vector3(-1, 0, 0),   // Second column
        new Vector3(0, 1, 0)   // Third column
    );
    /// <summary>
    /// Rotation offset used when converting a rotation from AMASS's Z-up convention
    /// to Unity's Y-up convention.
    /// </summary>
    public static readonly Matrix3x3 R_OFFSET_AMASS_TO_UNITY = new Matrix3x3(
        new Vector3(0, 0, 1),  // First column (No change)
        new Vector3(1, 0, 0),   // Second column: Y -> Z
        new Vector3(0, 1, 0)   // Third column: Z -> -Y
    );
    /// <summary>
    /// Rotation offset used when converting a rotation from Unity's Y-up convention
    /// to AMASS's Z-up convention.
    /// </summary>
    public static readonly Matrix3x3 R_OFFSET_UNITY_TO_AMASS = new Matrix3x3(
        new Vector3(0, 1, 0),  // First column (No change)
        new Vector3(0, 0, 1),   // Second column: Y -> Z
        new Vector3(1, 0, 0)   // Third column: Z -> -Y
    );


    /* Axis system conversion */

    /// <summary>
    /// Converts a position and rotation from Unity's axis system to AMASS's axis
    /// system, accounting for an additional offset GameObject in the hierarchy.
    /// Applies chirality change of basis for the rotation, then remaps from
    /// Y-up to Z-up for both position and rotation.
    /// </summary>
    /// <param name="position">The position in Unity world/local space.</param>
    /// <param name="rotation">The rotation matrix in Unity world/local space.</param>
    /// <returns>
    /// A tuple containing the converted <c>position</c> and <c>rotation</c> expressed
    /// in the AMASS (Z-up) axis system.
    /// </returns>
    public static (Vector3 position, Matrix3x3 rotation) UnityToAMASSAxisSystemFromOffsetGO(Vector3 position, Matrix3x3 rotation){
        // Rotations (Unity -> AMASS)
        Matrix3x3 temp = T_UNITYBasis2AMASSBasis.MultiplyMat(rotation);
        Matrix3x3 rotationAmassYup = temp.MultiplyMat(T_UNITYBasis2AMASSBasis.Transpose3x3());
        Matrix3x3 rotationAmassZup = R_OFFSET_UNITY_TO_AMASS.MultiplyMat(rotationAmassYup);
        // Positions (Unity -> AMASS)
        Vector3 positionAmassYup = T_UNITYBasis2AMASSBasis.MultiplyPoint(position); // -x, y, z
        Vector3 positionAmassZup = R_OFFSET_UNITY_TO_AMASS.MultiplyPoint(positionAmassYup); //Z up remap
        
        return (positionAmassZup, rotationAmassZup);
    }

    /// <summary>
    /// Convenience overload that extracts position and rotation directly from a
    /// Unity <see cref="Transform"/> and converts them from Unity's axis system
    /// to AMASS's axis system.
    /// </summary>
    /// <param name="transform">
    /// The source transform to convert. If <c>null</c>, returns
    /// (<see cref="Vector3.zero"/>, <see cref="Matrix3x3.identity"/>).
    /// </param>
    /// <returns>
    /// A tuple containing the converted <c>position</c> and <c>rotation</c> expressed
    /// in the AMASS (Z-up) axis system.
    /// </returns>
    public static (Vector3 position, Matrix3x3 rotation) UnityToAMASSAxisSystemFromOffsetGO(Transform transform){
        if (transform == null)
            return (Vector3.zero, Matrix3x3.identity);

        return UnityToAMASSAxisSystemFromOffsetGO(transform.position, Matrix3x3.TransformToMatrix3x3(transform));
    }

    /// <summary>
    /// Converts a global rotation matrix from AMASS's axis system (Z-up, right-handed)
    /// to Unity's axis system (Y-up, left-handed). Removes the global orientation
    /// offset, then applies the chirality change of basis.
    /// </summary>
    /// <param name="rotation">The global rotation matrix expressed in AMASS space.</param>
    /// <returns>The equivalent rotation matrix expressed in Unity space.</returns>
    public static Matrix3x3 GlobalAMASSToUnityAxisSystem(Matrix3x3 rotation){
        //Remove global orientation offset (Zup, Xforward, Yleft) -> (Yup, Zforward, Xleft)
        Matrix3x3 rotation_no_offset = R_OFFSET_AMASS_TO_UNITY.MultiplyMat(rotation);
        //Right handed axis system -> Left handed axis system (flip x axis)
        Matrix3x3 rotation_unity = T_AMASSBasis2UnityBasis.MultiplyMat(rotation_no_offset);
        return rotation_unity.MultiplyMat(T_AMASSBasis2UnityBasis.Transpose3x3());
    }

    /// <summary>
    /// Converts a local rotation matrix from AMASS's axis system to Unity's axis
    /// system by applying only the chirality change of basis (no global up-axis
    /// offset is removed).
    /// </summary>
    /// <param name="rotation">The local rotation matrix expressed in AMASS space.</param>
    /// <returns>The equivalent local rotation matrix expressed in Unity space.</returns>
    public static Matrix3x3 LocalAMASSToUnityAxisSystem(Matrix3x3 rotation){
        //change of basis AMASS -> Unity (right handed -> left handed by flipping x axis)
        Matrix3x3 temp = T_AMASSBasis2UnityBasis.MultiplyMat(rotation);
        return temp.MultiplyMat(T_AMASSBasis2UnityBasis.Transpose3x3());
    }
    

    /// <summary>
    /// Transforms a global position vector from AMASS coordinate system to Unity coordinate system.
    /// Applies the global axis realignment (T_AMASS2UNITY).
    /// </summary>
    public static Vector3 GlobalAMASSToUnityAxisSystem(Vector3 position)
    {
        //Vector3 PosInUnity = Multiply3x3(T_AMASS2UNITY, position);
        Vector3 PosInUnity = T_AMASS2UNITY.MultiplyPoint(position);
        PosInUnity.z = -PosInUnity.z;
        return PosInUnity;  
    }


}
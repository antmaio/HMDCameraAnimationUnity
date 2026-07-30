using UnityEngine;
using Unity.Collections;

/// <summary>
/// A lightweight 3x3 matrix type (column-major storage) used throughout the
/// project for rotations and axis-system conversions, since Unity's built-in
/// <see cref="Matrix4x4"/> carries unneeded 4x4 overhead for this purpose.
/// Supports conversion to/from quaternions, 6D rotation representations,
/// and Unity's <see cref="Matrix4x4"/> and <see cref="Transform"/> types.
/// </summary>
[System.Serializable]
public struct Matrix3x3
{
    
    /*
    Column major order and convention 
    x-col is x-local in world
    y-col is y-local in world
    z-col is z-local in world
    M = [
        [m00, m01, m02],
        [m10, m11, m12],
        [m20, m21, m22]
    ]
    */

    /// <summary>Row 0 elements (columns 0, 1, 2) of the matrix.</summary>
    public float m00, m01, m02;
    /// <summary>Row 1 elements (columns 0, 1, 2) of the matrix.</summary>
    public float m10, m11, m12;
    /// <summary>Row 2 elements (columns 0, 1, 2) of the matrix.</summary>
    public float m20, m21, m22;

    /// <summary>
    /// Gets or sets the matrix element at the given zero-based row and column.
    /// </summary>
    /// <param name="row">Row index, must be 0, 1, or 2.</param>
    /// <param name="col">Column index, must be 0, 1, or 2.</param>
    /// <returns>The value at the specified row and column.</returns>
    /// <exception cref="System.IndexOutOfRangeException">
    /// Thrown if <paramref name="row"/> or <paramref name="col"/> is outside the range [0, 2].
    /// </exception>
    public float this[int row, int col]
    {
        get
        {
            return (row, col) switch
            {
                (0, 0) => m00,
                (0, 1) => m01,
                (0, 2) => m02,
                (1, 0) => m10,
                (1, 1) => m11,
                (1, 2) => m12,
                (2, 0) => m20,
                (2, 1) => m21,
                (2, 2) => m22,
                _ => throw new System.IndexOutOfRangeException("Matrix3x3 indices must be between 0 and 2")
            };
        }
        set
        {
            switch (row, col)
            {
                case (0, 0): m00 = value; break;
                case (0, 1): m01 = value; break;
                case (0, 2): m02 = value; break;
                case (1, 0): m10 = value; break;
                case (1, 1): m11 = value; break;
                case (1, 2): m12 = value; break;
                case (2, 0): m20 = value; break;
                case (2, 1): m21 = value; break;
                case (2, 2): m22 = value; break;
                default: throw new System.IndexOutOfRangeException("Matrix3x3 indices must be between 0 and 2");
            }
        }
    }

    /// <summary>
    /// Constructs a matrix from three column vectors.
    /// </summary>
    /// <param name="col0">The first column of the matrix.</param>
    /// <param name="col1">The second column of the matrix.</param>
    /// <param name="col2">The third column of the matrix.</param>
    public Matrix3x3(Vector3 col0, Vector3 col1, Vector3 col2)
    {
        m00 = col0.x; m01 = col1.x; m02 = col2.x;
        m10 = col0.y; m11 = col1.y; m12 = col2.y;
        m20 = col0.z; m21 = col1.z; m22 = col2.z;
    }

    /// <summary>
    /// Constructs a matrix from a 3x3 row-major float array.
    /// </summary>
    /// <param name="values">A 3x3 array where <c>values[row, col]</c> gives the element at that position.</param>
    public Matrix3x3(float[,] values)
    {
        m00 = values[0, 0]; m01 = values[0, 1]; m02 = values[0, 2];
        m10 = values[1, 0]; m11 = values[1, 1]; m12 = values[1, 2];
        m20 = values[2, 0]; m21 = values[2, 1]; m22 = values[2, 2];
    }


    /// <summary>
    /// The 3x3 identity matrix.
    /// </summary>
    public static readonly Matrix3x3 identity = new Matrix3x3(
        new Vector3(1f, 0f, 0f),
        new Vector3(0f, 1f, 0f),
        new Vector3(0f, 0f, 1f)
    );

    /* Matrix Conversions */

    /// <summary>
    /// Converts this 3x3 matrix into a 4x4 matrix, embedding it in the upper-left
    /// 3x3 block of an otherwise identity <see cref="Matrix4x4"/> (no translation
    /// or projection components are set).
    /// </summary>
    /// <returns>The equivalent 4x4 matrix.</returns>
    public Matrix4x4 ToMatrix4x4()
    {
        Matrix4x4 matrix = Matrix4x4.identity;
        matrix[0, 0] = m00; matrix[0, 1] = m01; matrix[0, 2] = m02;
        matrix[1, 0] = m10; matrix[1, 1] = m11; matrix[1, 2] = m12;
        matrix[2, 0] = m20; matrix[2, 1] = m21; matrix[2, 2] = m22;
        return matrix;
    }

    /// <summary>
    /// Extracts the upper-left 3x3 block of a <see cref="Matrix4x4"/> into a new
    /// <see cref="Matrix3x3"/>, discarding translation/projection components.
    /// </summary>
    /// <param name="matrix">The source 4x4 matrix.</param>
    /// <returns>A new <see cref="Matrix3x3"/> containing the rotation/scale block of <paramref name="matrix"/>.</returns>
    public static Matrix3x3 FromMatrix4x4(Matrix4x4 matrix)
    {
        Matrix3x3 m = new Matrix3x3();
        m.m00 = matrix.m00; m.m01 = matrix.m01; m.m02 = matrix.m02;
        m.m10 = matrix.m10; m.m11 = matrix.m11; m.m12 = matrix.m12;
        m.m20 = matrix.m20; m.m21 = matrix.m21; m.m22 = matrix.m22;
        return m;
    }

    /// <summary>
    /// Returns a human-readable, row-by-row string representation of the matrix,
    /// with each element formatted to 3 decimal places. Useful for debug logging.
    /// </summary>
    /// <returns>A multi-line string showing the matrix's 3 rows.</returns>
    public override string ToString()
    {
        return $"[{m00:F3}, {m01:F3}, {m02:F3}]\n[{m10:F3}, {m11:F3}, {m12:F3}]\n[{m20:F3}, {m21:F3}, {m22:F3}]";
    }
    
    /* Rotation format conversion */

    /// <summary>
    /// Converts a quaternion into its equivalent 3x3 rotation matrix.
    /// </summary>
    /// <param name="q">The quaternion to convert.</param>
    /// <returns>The equivalent rotation matrix.</returns>
    public static Matrix3x3 FromQuaternion(Quaternion q)
    {
        float xx = q.x * q.x;
        float yy = q.y * q.y;
        float zz = q.z * q.z;
        float xy = q.x * q.y;
        float xz = q.x * q.z;
        float yz = q.y * q.z;
        float wx = q.w * q.x;
        float wy = q.w * q.y;
        float wz = q.w * q.z;

        Matrix3x3 matrix = new Matrix3x3();

        // First column
        matrix.m00 = 1.0f - 2.0f * (yy + zz);
        matrix.m10 = 2.0f * (xy + wz);
        matrix.m20 = 2.0f * (xz - wy);

        // Second column
        matrix.m01 = 2.0f * (xy - wz);
        matrix.m11 = 1.0f - 2.0f * (xx + zz);
        matrix.m21 = 2.0f * (yz + wx);

        // Third column
        matrix.m02 = 2.0f * (xz + wy);
        matrix.m12 = 2.0f * (yz - wx);
        matrix.m22 = 1.0f - 2.0f * (xx + yy);

        return matrix;
    }

    /// <summary>
    /// Converts this rotation matrix into an equivalent quaternion, using the
    /// numerically robust trace-based algorithm (choosing the largest diagonal
    /// term to avoid division by near-zero values).
    /// </summary>
    /// <returns>The equivalent quaternion representing this matrix's rotation.</returns>
    public Quaternion ToQuaternion(){
        // Using the robust conversion algorithm
        float trace = m00 + m11 + m22;

        if (trace > 0)
        {
            float s = 0.5f / Mathf.Sqrt(trace + 1.0f);
            return new Quaternion(
                (m21 - m12) * s,
                (m02 - m20) * s,
                (m10 - m01) * s,
                0.25f / s
            );
        }
        else
        {
            if (m00 > m11 && m00 > m22)
            {
                float s = 2.0f * Mathf.Sqrt(1.0f + m00 - m11 - m22);
                return new Quaternion(
                    0.25f * s,
                    (m01 + m10) / s,
                    (m02 + m20) / s,
                    (m21 - m12) / s
                );
            }
            else if (m11 > m22)
            {
                float s = 2.0f * Mathf.Sqrt(1.0f + m11 - m00 - m22);
                return new Quaternion(
                    (m01 + m10) / s,
                    0.25f * s,
                    (m12 + m21) / s,
                    (m02 - m20) / s
                );
            }
            else
            {
                float s = 2.0f * Mathf.Sqrt(1.0f + m22 - m00 - m11);
                return new Quaternion(
                    (m02 + m20) / s,
                    (m12 + m21) / s,
                    0.25f * s,
                    (m10 - m01) / s
                );
            }
        }
    }
    /// <summary>
    /// Computes the 3x3 rotation matrix corresponding to a <see cref="Transform"/>'s
    /// world rotation directly from its quaternion components, without going
    /// through <see cref="FromQuaternion"/>.
    /// </summary>
    /// <param name="transform">The transform whose rotation should be converted.</param>
    /// <returns>The equivalent rotation matrix.</returns>
    public static Matrix3x3 TransformToMatrix3x3(Transform transform){
        float xx = transform.rotation.x * transform.rotation.x;
        float yy = transform.rotation.y * transform.rotation.y;
        float zz = transform.rotation.z * transform.rotation.z;
        float xy = transform.rotation.x * transform.rotation.y;
        float xz = transform.rotation.x * transform.rotation.z;
        float yz = transform.rotation.y * transform.rotation.z;
        float wx = transform.rotation.w * transform.rotation.x;
        float wy = transform.rotation.w * transform.rotation.y;
        float wz = transform.rotation.w * transform.rotation.z;

        Matrix3x3 matrix = new Matrix3x3();

        // First column
        matrix[0, 0] = 1.0f - 2.0f * (yy + zz);  // m00
        matrix[1, 0] = 2.0f * (xy + wz);          // m10
        matrix[2, 0] = 2.0f * (xz - wy);          // m20

        // Second column
        matrix[0, 1] = 2.0f * (xy - wz);          // m01
        matrix[1, 1] = 1.0f - 2.0f * (xx + zz);  // m11
        matrix[2, 1] = 2.0f * (yz + wx);          // m21

        // Third column
        matrix[0, 2] = 2.0f * (xz + wy);          // m02
        matrix[1, 2] = 2.0f * (yz - wx);          // m12
        matrix[2, 2] = 1.0f - 2.0f * (xx + yy);  // m22

        return matrix;
    }

    /// <summary>
    /// Converts from 6D two-axis rotation representation to 3x3 matrix representation.
    /// Based on Gram-Schmidt orthogonalization process with fallback for near-zero axes.
    /// </summary>
    /// <param name="sixD">6D representation as array of 6 floats: [a1, a2, a3, a4, a5, a6]</param>
    /// <param name="axisNormThreshold">Threshold for axis norms below which we fix the value of resulting matrix columns (default: 1e-8)</param>
    /// <returns>3x3 rotation matrix</returns>
    public static Matrix3x3 From6d(float[] sixD, float axisNormThreshold = 1e-8f)
    {
        if (sixD == null || sixD.Length != 6)
        {
            Debug.LogError($"From6d: Input array must have exactly 6 elements, got {(sixD == null ? "null" : sixD.Length.ToString())}");
            return identity;
        }

        return From6d(sixD[0], sixD[1], sixD[2], sixD[3], sixD[4], sixD[5], axisNormThreshold);
    }

    /// <summary>
    /// Converts from 6D two-axis rotation representation to 3x3 matrix representation.
    /// Optimized version using NativeArray for performance.
    /// </summary>
    /// <param name="sixD">6D representation as NativeArray of 6 floats</param>
    /// <param name="axisNormThreshold">Threshold for axis norms below which we fix the value of resulting matrix columns (default: 1e-8)</param>
    /// <returns>3x3 rotation matrix</returns>
    public static Matrix3x3 From6d(NativeArray<float> sixD, float axisNormThreshold = 1e-8f)
    {
        if (!sixD.IsCreated || sixD.Length != 6)
        {
            Debug.LogError($"From6d: NativeArray must be created and have exactly 6 elements, got {(sixD.IsCreated ? sixD.Length.ToString() : "not created")}");
            return identity;
        }

        return From6d(sixD[0], sixD[1], sixD[2], sixD[3], sixD[4], sixD[5], axisNormThreshold);
    }

    /// <summary>
    /// Converts from 6D two-axis rotation representation to 3x3 matrix representation.
    /// Vector version for convenience.
    /// </summary>
    /// <param name="axis1">First 3D axis (first half of 6D)</param>
    /// <param name="axis2">Second 3D axis (second half of 6D)</param>
    /// <param name="axisNormThreshold">Threshold for axis norms below which we fix the value of resulting matrix columns (default: 1e-8)</param>
    /// <returns>3x3 rotation matrix</returns>
    public static Matrix3x3 From6d(Vector3 axis1, Vector3 axis2, float axisNormThreshold = 1e-8f)
    {
        return From6d(axis1.x, axis1.y, axis1.z, axis2.x, axis2.y, axis2.z, axisNormThreshold);
    }

    /// <summary>
    /// Converts from 6D two-axis rotation representation to 3x3 matrix representation.
    /// Based on Gram-Schmidt orthogonalization process with fallback for near-zero axes.
    /// </summary>
    /// <param name="a1">First axis, x component</param>
    /// <param name="a2">First axis, y component</param>
    /// <param name="a3">First axis, z component</param>
    /// <param name="a4">Second axis, x component</param>
    /// <param name="a5">Second axis, y component</param>
    /// <param name="a6">Second axis, z component</param>
    /// <param name="axisNormThreshold">Threshold for axis norms below which we fix the value of resulting matrix columns (default: 1e-8)</param>
    /// <returns>3x3 rotation matrix</returns>
    public static Matrix3x3 From6d(float a1, float a2, float a3, 
                                   float a4, float a5, float a6, 
                                   float axisNormThreshold = 1e-8f)
    {
        if (axisNormThreshold < 0f)
        {
            Debug.LogWarning($"From6d: axisNormThreshold should be >= 0, got {axisNormThreshold}. Using absolute value.");
            axisNormThreshold = Mathf.Abs(axisNormThreshold);
        }

        // First 3D vector (first half of 6D)
        Vector3 in1 = new Vector3(a1, a2, a3);
        
        // Compute first column
        float in1Norm = in1.magnitude;
        Vector3 col1;
        
        if (in1Norm >= axisNormThreshold)
        {
            col1 = in1 / in1Norm;
        }
        else
        {
            // Fallback to fixed axis if norm is too small
            col1 = new Vector3(1f, 0f, 0f);
        }

        // Second 3D vector (second half of 6D)
        Vector3 in2 = new Vector3(a4, a5, a6);
        
        // Compute second column (Gram-Schmidt orthogonalization)
        Vector3 col2 = in2 - Vector3.Dot(in2, col1) * col1;
        float col2Norm = col2.magnitude;
        
        if (col2Norm >= axisNormThreshold)
        {
            col2 = col2 / col2Norm;
        }
        else
        {
            // Fallback to fixed axis if norm is too small
            col2 = new Vector3(0f, 1f, 0f);
        }

        // Compute third column (cross product ensures orthonormality)
        Vector3 col3 = Vector3.Cross(col1, col2);
        
        // Normalize for safety (should already be unit length due to orthonormal col1 and col2)
        col3.Normalize();

        return new Matrix3x3(col1, col2, col3);
    }

    /// <summary>
    /// Validates if this matrix is a valid rotation matrix (orthonormal).
    /// </summary>
    /// <param name="tolerance">Allowed deviation from orthonormality</param>
    /// <returns>True if matrix is approximately orthonormal</returns>
    public bool IsRotationMatrix(float tolerance = 1e-6f)
    {
        // Check if columns are unit length
        Vector3 col0 = new Vector3(m00, m10, m20);
        Vector3 col1 = new Vector3(m01, m11, m21);
        Vector3 col2 = new Vector3(m02, m12, m22);
        
        if (Mathf.Abs(col0.sqrMagnitude - 1f) > tolerance) return false;
        if (Mathf.Abs(col1.sqrMagnitude - 1f) > tolerance) return false;
        if (Mathf.Abs(col2.sqrMagnitude - 1f) > tolerance) return false;
        
        // Check if columns are orthogonal
        if (Mathf.Abs(Vector3.Dot(col0, col1)) > tolerance) return false;
        if (Mathf.Abs(Vector3.Dot(col0, col2)) > tolerance) return false;
        if (Mathf.Abs(Vector3.Dot(col1, col2)) > tolerance) return false;
        
        // Check if determinant is approximately 1 (for proper rotation)
        float det = m00 * (m11 * m22 - m12 * m21) -
                   m01 * (m10 * m22 - m12 * m20) +
                   m02 * (m10 * m21 - m11 * m20);
        
        return Mathf.Abs(det - 1f) < tolerance;
    }

    /* ops */
    /// <summary>
    /// Multiplies the matrix by a vector (point).
    /// </summary>
    /// <param name="point">The vector to multiply.</param>
    /// <returns>The resulting vector.</returns>
    public Vector3 MultiplyPoint(Vector3 point)
    {
        return new Vector3(
            m00 * point.x + m01 * point.y + m02 * point.z,
            m10 * point.x + m11 * point.y + m12 * point.z,
            m20 * point.x + m21 * point.y + m22 * point.z
        );
    }

    /// <summary>
    /// Multiplies this matrix with another Matrix3x3.
    /// The multiplication is done in the following way:
    /// M = Matrix3x3 @ b
    /// </summary>
    /// <param name="b">The matrix to multiply with.</param>
    /// <returns>The resulting Matrix3x3.</returns>
    public Matrix3x3 MultiplyMat(Matrix3x3 b)
    {
        return new Matrix3x3()
        {
            m00 = m00 * b.m00 + m01 * b.m10 + m02 * b.m20,
            m01 = m00 * b.m01 + m01 * b.m11 + m02 * b.m21,
            m02 = m00 * b.m02 + m01 * b.m12 + m02 * b.m22,

            m10 = m10 * b.m00 + m11 * b.m10 + m12 * b.m20,
            m11 = m10 * b.m01 + m11 * b.m11 + m12 * b.m21,
            m12 = m10 * b.m02 + m11 * b.m12 + m12 * b.m22,

            m20 = m20 * b.m00 + m21 * b.m10 + m22 * b.m20,
            m21 = m20 * b.m01 + m21 * b.m11 + m22 * b.m21,
            m22 = m20 * b.m02 + m21 * b.m12 + m22 * b.m22
        };
    }

    /// <summary>
    /// Returns the transpose of this matrix.
    /// </summary>
    /// <returns>A new Matrix3x3 that is the transpose of this matrix.</returns>
    public Matrix3x3 Transpose3x3()
    {
        return new Matrix3x3()
        {
            m00 = m00,
            m01 = m10,
            m02 = m20,
            m10 = m01,
            m11 = m11,
            m12 = m21,
            m20 = m02,
            m21 = m12,
            m22 = m22
        };
    }
    
    /// <summary>
    /// Returns the inverse of this matrix.
    /// </summary>
    /// <returns>A new Matrix3x3 that is the inverse of this matrix, or identity if the determinant is near zero.</returns>
    public Matrix3x3 Inverse3x3()
    {
        // Calculate determinant
        float det = m00 * (m11 * m22 - m21 * m12)
                  - m01 * (m10 * m22 - m20 * m12)
                  + m02 * (m10 * m21 - m20 * m11);

        // Check if matrix is invertible
        if (Mathf.Abs(det) < 1e-8f)
            return Matrix3x3.identity;

        float invDet = 1.0f / det;

        Matrix3x3 inv = new Matrix3x3();

        // Calculate the inverse using the adjugate matrix
        inv.m00 = (m11 * m22 - m21 * m12) * invDet;
        inv.m01 = (m02 * m21 - m01 * m22) * invDet;
        inv.m02 = (m01 * m12 - m02 * m11) * invDet;

        inv.m10 = (m12 * m20 - m10 * m22) * invDet;
        inv.m11 = (m00 * m22 - m02 * m20) * invDet;
        inv.m12 = (m02 * m10 - m00 * m12) * invDet;

        inv.m20 = (m10 * m21 - m20 * m11) * invDet;
        inv.m21 = (m20 * m01 - m00 * m21) * invDet;
        inv.m22 = (m00 * m11 - m10 * m01) * invDet;

        return inv;
    }
}
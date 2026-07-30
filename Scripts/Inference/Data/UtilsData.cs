using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Provides joint-order mapping utilities between the SMPL-X skeleton joint
/// ordering (as used by AMASS/SMPL-X data) and Unity's own humanoid joint
/// ordering, so per-joint data (e.g. HMR body pose) can be reindexed correctly
/// when transferring between the two conventions.
/// </summary>
public class UtilsData
{
    /// <summary>
    /// Joint indices as defined by the SMPL-X body model topology.
    /// </summary>
    public enum SmplxJoints
    {
        PELVIS = 0,
        LEFT_HIP = 1,
        RIGHT_HIP = 2,
        SPINE_1 = 3,
        LEFT_KNEE = 4,
        RIGHT_KNEE = 5,
        SPINE_2 = 6,
        LEFT_ANKLE = 7,
        RIGHT_ANKLE = 8,
        SPINE_3 = 9,
        LEFT_FOOT = 10,
        RIGHT_FOOT = 11,
        NECK = 12,
        LEFT_COLLAR = 13,
        RIGHT_COLLAR = 14,
        HEAD = 15,
        LEFT_SHOULDER = 16,
        RIGHT_SHOULDER = 17,
        LEFT_ELBOW = 18,
        RIGHT_ELBOW = 19,
        LEFT_WRIST = 20,
        RIGHT_WRIST = 21
    }

    /// <summary>
    /// Joint indices as defined by Unity's humanoid skeleton topology.
    /// Note the ordering differs from <see cref="SmplxJoints"/> (e.g. Unity groups
    /// each limb's chain together, while SMPL-X interleaves left/right at each level).
    /// </summary>
    public enum UnityJoints
    {
        PELVIS = 0,
        LEFT_HIP = 1,
        LEFT_KNEE = 2,
        LEFT_ANKLE = 3,
        LEFT_FOOT = 4,
        RIGHT_HIP = 5,
        RIGHT_KNEE = 6,
        RIGHT_ANKLE = 7,
        RIGHT_FOOT = 8,
        SPINE1 = 9,
        SPINE2 = 10,
        SPINE3 = 11,
        LEFT_COLLAR = 12,
        LEFT_SHOULDER = 13,
        LEFT_ELBOW = 14,
        LEFT_WRIST = 15,
        NECK = 16,
        HEAD = 17,
        RIGHT_COLLAR = 18,
        RIGHT_SHOULDER = 19,
        RIGHT_ELBOW = 20,
        RIGHT_WRIST = 21
    }

    /// <summary>
    /// Builds a lookup table that maps each Unity joint index to its corresponding
    /// SMPL-X joint index, by matching joints via <see cref="_FromUnityJointOrder"/>
    /// in Unity's enum order. Used to reindex per-joint arrays (e.g. HMR body pose,
    /// which comes in SMPL-X order) into Unity's joint order.
    /// </summary>
    /// <returns>
    /// An array of length equal to the joint count, where <c>result[unityJointIndex]</c>
    /// gives the matching SMPL-X joint index. Returns an array sized by
    /// <see cref="_AssertJointCountIsSimilarBetweenTopologies"/>, which is <c>-1</c>
    /// (and logs an error) if the two enums don't have the same number of entries.
    /// </returns>
    public int[] CreateSmplxToUnityJointMapping()
    {
        int njoints = _AssertJointCountIsSimilarBetweenTopologies();

        // Get all Unity joints
        var unityJoints = (UnityJoints[])Enum.GetValues(typeof(UnityJoints));
        
        int[] adjustedMapping = new int[njoints];

        // Start from index 1 to skip PELVIS in UnityJoints
        for (int j = 0; j < njoints; j++)
        {
            var unityJoint = unityJoints[j];
            SmplxJoints smplxJoint = _FromUnityJointOrder(unityJoint);
            adjustedMapping[j] = (int)smplxJoint;
        }
        
        return adjustedMapping;
    }

    /// <summary>
    /// Finds the SMPL-X joint that corresponds to a given Unity joint, matching by
    /// name (accounting for Unity's "SPINE1/2/3" naming vs. SMPL-X's "SPINE_1/2/3",
    /// and ignoring underscores as a fallback comparison).
    /// </summary>
    /// <param name="unityJoint">The Unity joint to find a matching SMPL-X joint for.</param>
    /// <returns>
    /// The matching <see cref="SmplxJoints"/> value, or <see cref="SmplxJoints.PELVIS"/>
    /// (with an error logged) if no match is found.
    /// </returns>
    private SmplxJoints _FromUnityJointOrder(UnityJoints unityJoint)
    {
        string searchName = unityJoint.ToString();
        if (searchName.StartsWith("SPINE"))
        {
            searchName = searchName.Replace("SPINE", "SPINE_");
        }

        foreach (SmplxJoints smplxJoint in Enum.GetValues(typeof(SmplxJoints)))
        {
            string smplxName = smplxJoint.ToString();
            if (smplxName == searchName || 
                smplxName.Replace("_", "") == searchName.Replace("_", ""))
            {
                return smplxJoint;
            }
        }
        
        Debug.LogError($"No mapping found for Unity joint {unityJoint}");
        return SmplxJoints.PELVIS;
    }

    /// <summary>
    /// Finds the Unity joint that corresponds to a given SMPL-X joint, matching by
    /// name with underscores stripped from both sides for comparison.
    /// </summary>
    /// <param name="smplxJoint">The SMPL-X joint to find a matching Unity joint for.</param>
    /// <returns>
    /// The matching <see cref="UnityJoints"/> value, or <see cref="UnityJoints.PELVIS"/>
    /// (with an error logged) if no match is found.
    /// </returns>
    private UnityJoints _FromSmplxJointOrder(SmplxJoints smplxJoint)
    {
        string searchName = smplxJoint.ToString().Replace("_", "");

        foreach (UnityJoints unityJoint in Enum.GetValues(typeof(UnityJoints)))
        {
            string unityName = unityJoint.ToString().Replace("_", "");
            if (unityName == searchName)
            {
                return unityJoint;
            }
        }

        Debug.LogError($"No mapping found for SMPLX joint {smplxJoint}");
        return UnityJoints.PELVIS;
    }


    /// <summary>
    /// Verifies that <see cref="UnityJoints"/> and <see cref="SmplxJoints"/> define the
    /// same number of joints, since the mapping logic assumes a 1-to-1 correspondence.
    /// </summary>
    /// <returns>
    /// The shared joint count if both enums match in size; otherwise <c>-1</c>,
    /// with an error logged describing the mismatch.
    /// </returns>
    private int _AssertJointCountIsSimilarBetweenTopologies()
    {
        int unityCount = Enum.GetValues(typeof(UnityJoints)).Length;
        int smplxCount = Enum.GetValues(typeof(SmplxJoints)).Length;
        if (unityCount != smplxCount)
        {
            Debug.LogError($"Joint count mismatch! UnityJoints: {unityCount}, SmplxJoints: {smplxCount}");
            return -1;
        }
        else{
            return unityCount;
        }
    }
}
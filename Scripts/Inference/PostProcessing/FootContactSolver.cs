// using UnityEngine;
// using UnityEngine.Animations.Rigging;

// /// <summary>
// /// Drives foot-locking IK on top of a neural-network-predicted pose to remove
// /// foot sliding / ground penetration ("denoising" the contact, not the rotation noise).
// ///
// /// SETUP (one-time, in the Editor):
// /// 1. Window > Package Manager > install "Animation Rigging" (com.unity.animation.rigging).
// /// 2. On your avatar, add a "Rig" GameObject (Animation Rigging > Rig) as a child of an
// ///    object with a RigBuilder component (add RigBuilder to the avatar root if you don't
// ///    have one, and assign the Rig to its Rig Layers list).
// /// 3. Under the Rig, create two child GameObjects, one per leg, each with a
// ///    TwoBoneIKConstraint component:
// ///      - Root      = thigh/hip bone Transform
// ///      - Mid       = knee bone Transform
// ///      - Tip       = ankle bone Transform
// ///      - Target    = a new empty Transform (this is what THIS script moves)
// ///      - Hint      = a new empty Transform placed slightly in front of/below the knee
// ///                    (defines bend direction; without it the knee can flip)
// ///      - Weight    = 0 by default (this script will animate it at runtime)
// /// 4. Add this FootContactSolver component anywhere on the avatar and assign the
// ///    constraint, ikTarget, and ankleBone references for each leg in the Inspector.
// ///
// /// EXECUTION ORDER NOTE:
// /// RigBuilder evaluates constraints via a PlayableGraph that runs after normal
// /// MonoBehaviour LateUpdate calls in the same frame, so as long as this script's
// /// LateUpdate (or whatever updates ankleBone's pose) runs before that, the IK target
// /// will reflect the current frame's pose with no extra lag. If you see the foot IK
// /// trailing by a frame, move FootContactSolver earlier in
// /// Edit > Project Settings > Script Execution Order.
// /// </summary>
// public class FootContactSolver : MonoBehaviour
// {
//     [System.Serializable]
//     public class FootIK
//     {
//         [Tooltip("The TwoBoneIKConstraint living on the Rig for this leg.")]
//         public TwoBoneIKConstraint constraint;

//         [Tooltip("The empty Transform assigned as the constraint's Target.")]
//         public Transform ikTarget;

//         [Tooltip("The actual ankle/foot bone being driven by the neural net's raw pose.")]
//         public Transform ankleBone;

//         [HideInInspector] public Vector3 prevAnklePos;
//         [HideInInspector] public bool grounded;
//         [HideInInspector] public Vector3 lockPos;
//     }

//     [Header("Legs")]
//     public FootIK leftFoot;
//     public FootIK rightFoot;

//     [Header("Ground detection")]
//     public LayerMask groundMask;
//     public float raycastHeight = 0.5f;
//     public float raycastDistance = 1.5f;
//     [Tooltip("How high the ankle joint sits above the floor when fully planted.")]
//     public float ankleGroundOffset = 0.02f;

//     [Header("Contact thresholds")]
//     [Tooltip("Ankle speed (m/s) below which the foot is a candidate for being planted.")]
//     public float velocityThreshold = 0.08f;
//     [Tooltip("Height above ground (m) below which the foot is a candidate for being planted.")]
//     public float heightThreshold = 0.04f;
//     [Tooltip("How fast the IK weight ramps in/out (per second). Higher = snappier, lower = smoother.")]
//     public float blendSpeed = 12f;

//     void LateUpdate()
//     {
//         SolveFoot(leftFoot);
//         SolveFoot(rightFoot);
//     }

//     void SolveFoot(FootIK foot)
//     {
//         if (foot.ankleBone == null || foot.constraint == null || foot.ikTarget == null)
//             return;

//         Vector3 anklePos = foot.ankleBone.position;
//         float dt = Mathf.Max(Time.deltaTime, 0.0001f);
//         float speed = (anklePos - foot.prevAnklePos).magnitude / dt;
//         foot.prevAnklePos = anklePos;

//         // Find the floor under the ankle.
//         float groundY = anklePos.y;
//         Vector3 rayOrigin = anklePos + Vector3.up * raycastHeight;
//         if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundMask))
//         {
//             groundY = hit.point.y;
//         }
//         float heightAboveGround = anklePos.y - groundY;

//         bool wantsContact = speed < velocityThreshold && heightAboveGround < heightThreshold;

//         // Re-lock the moment contact begins; otherwise hold the existing lock until released.
//         if (wantsContact && !foot.grounded)
//         {
//             foot.lockPos = new Vector3(anklePos.x, groundY + ankleGroundOffset, anklePos.z);
//             foot.grounded = true;
//         }
//         else if (!wantsContact)
//         {
//             foot.grounded = false;
//         }

//         // Smoothly blend the constraint weight rather than toggling it, to avoid popping.
//         float targetWeight = foot.grounded ? 1f : 0f;
//         foot.constraint.weight = Mathf.MoveTowards(foot.constraint.weight, targetWeight, blendSpeed * dt);

//         // Blend the IK target between the raw (possibly sliding/noisy) ankle position
//         // and the locked ground position, weighted by current IK influence.
//         foot.ikTarget.position = Vector3.Lerp(anklePos, foot.lockPos, foot.constraint.weight);

//         // Keep the foot flat against the ground as lock weight increases, rather than
//         // keeping whatever pitch/roll the network predicted.
//         Quaternion rawRot = foot.ankleBone.rotation;
//         Vector3 flattenedForward = Vector3.ProjectOnPlane(rawRot * Vector3.forward, Vector3.up);
//         if (flattenedForward.sqrMagnitude > 0.0001f)
//         {
//             Quaternion flatRot = Quaternion.LookRotation(flattenedForward, Vector3.up);
//             foot.ikTarget.rotation = Quaternion.Slerp(rawRot, flatRot, foot.constraint.weight);
//         }
//         else
//         {
//             foot.ikTarget.rotation = rawRot;
//         }
//     }
// }
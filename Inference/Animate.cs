using UnityEngine;
using Unity.InferenceEngine;
using System.Threading.Tasks;
using TMPro;

/// <summary>
/// Handles the real-time tracking of XR inputs, runs deep learning model inference, 
/// and applies generated motion data to the avatar.
/// </summary>
public class Animate : MonoBehaviour
{

    private MotionApplierSelfAvatar motionApplierSelfAvatar;

    [Header("XR References")]
    public GameObject hmd;
    public GameObject rightController;
    public GameObject leftController;

    /* Deep Learning */
    private NeuralNetwork neuralNetwork;
    private Worker worker;

    /* Events function */

    void Start()
    {

        //Check if the components are loaded
        if (hmd == null || rightController == null || leftController == null){
            Debug.LogError("No XR references found! Please attach a XR references to this GameObject.");
            return;
        }
        // Loading required components 
        if (neuralNetwork == null){
            neuralNetwork = GetComponent<NeuralNetwork>();
        }
        if (motionApplierSelfAvatar == null){
            motionApplierSelfAvatar = GetComponentInChildren<MotionApplierSelfAvatar>();
        }

        //Check if the components are loaded
        if (neuralNetwork == null || motionApplierSelfAvatar == null) {
            if (neuralNetwork == null){
                Debug.LogError("No NeuralNetwork found! Please attach a NeuralNetwork component to this GameObject.");
            }
            if (motionApplierSelfAvatar == null){
                Debug.LogError("No MotionApplierSelfAvatar found! Please attach a MotionApplierSelfAvatar component to this GameObject.");
            }
            return;
        }
    
        else {
            worker = neuralNetwork.LoadModel();
        }

    }

    void FixedUpdate()
    {
        
        //Get Motion data from hmd and handheld controllers (NEW)
        TensorData TensorWrapper = GetComponent<TensorData>();
        float[] poseData = TensorWrapper.GetMotionFeaturesFromGameObjects(hmd, leftController, rightController);
        Tensor<float> poseTensor = TensorWrapper.CreateTensorFromPoseData(poseData);

        //Inference
        neuralNetwork.SyncInference(worker, poseTensor);
        motionApplierSelfAvatar.ApplyMotion();
        
    }

    void onDisable(){
        worker.Dispose();
    }

    // public void RecenterHMD(GameObject hmd){}

}


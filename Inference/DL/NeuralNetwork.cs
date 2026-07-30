using UnityEngine;
using Unity.InferenceEngine;    
using System.Threading.Tasks;
using TMPro;
using Unity.Collections;

/// <summary>
/// Manages the Sentis/InferenceEngine model execution pipeline. 
/// Handles model loading, tensor input scheduling, output extraction, and state management for avatar pose estimation.
/// </summary>
public class NeuralNetwork : MonoBehaviour
{
/// <summary>
    /// Toggles verbose console logging for debug visualization.
    /// </summary>
    public bool verbose = true;

    /// <summary>
    /// The Unity InferenceEngine ONNX or Sentis model asset reference.
    /// </summary>
    public ModelAsset modelAsset;
  
    private Worker worker;

    //declare output data
    private Vector3 rootPosition;
    private Vector3 rootPositionInUnity;
    private Matrix3x3 globalOrient;
    private Matrix3x3 globalOrientInUnity;
    private Matrix3x3[] bodyPoseRotations; // 21 joint rotations
    private Matrix3x3[] bodyPoseRotationsInUnity;
    private float[] betas;
    private static float[] flattenedStates;
    private TMP_Text fpsText;

    private bool isInferencing = false; // Prevent overlapping inferences
    
    /// <summary>
    /// Loads the assigned <see cref="modelAsset"/> into memory and creates an execution worker using the CPU backend.
    /// </summary>
    /// <returns>The initialized <see cref="Worker"/> engine ready to execute model inference.</returns>
    public Worker LoadModel(){
        Model runtimeModel = ModelLoader.Load(modelAsset);
        worker = new Worker(runtimeModel, BackendType.CPU);
        return worker;
    }

    /// <summary>
    /// Synchronously schedules model execution using an input pose tensor, extracts the output tensors (root position, 
    /// global orientation, body joint poses, and RNN states), converts spatial data to Unity's coordinate system, 
    /// and frees native memory allocations.
    /// </summary>
    /// <param name="worker">The inference engine worker executing the model.</param>
    /// <param name="inputTensor">The input pose feature tensor fed into the model.</param>
    public void SyncInference(Worker worker, Tensor<float> inputTensor){
        
        // null check
        if (worker == null){
            Debug.LogError("Worker is null in SyncInference");
            return;
        }
        if (inputTensor == null){
            Debug.LogError("Input Tensor is null SyncInference");
            return;
        }
        
        //Inference
        worker.Schedule(inputTensor);

        //Get outputs
        Tensor<float> RootPosTensor         = worker.PeekOutput("root_pos") as Tensor<float>;
        Tensor<float> GlobalOrientTensor    = worker.PeekOutput("global_orient_6d_pred") as Tensor<float>;
        Tensor<float> BodyPoseTensor        = worker.PeekOutput("body_pose_6d_pred") as Tensor<float>;
        Tensor<float> RnnStatesTensor       = worker.PeekOutput("rnn_states") as Tensor<float>;

        // Fast access: Download tensor data to NativeArray
        NativeArray<float> rootPosData      = RootPosTensor.DownloadToNativeArray();
        NativeArray<float> globalOrientData = GlobalOrientTensor.DownloadToNativeArray();
        NativeArray<float> bodyPoseData     = BodyPoseTensor.DownloadToNativeArray();
        NativeArray<float> rnnStatesData    = RnnStatesTensor.DownloadToNativeArray();

        // Create Vector3 directly from NativeArray (fastest method - direct indexing)
        rootPosition = new Vector3(rootPosData[0], rootPosData[1], rootPosData[2]);
        globalOrient = Matrix3x3.From6d(globalOrientData);

        int nJoints = bodyPoseData.Length / Constants.ROT_FEAT_BY_JOINT;

        if (bodyPoseRotations == null || bodyPoseRotations.Length != nJoints){
            bodyPoseRotations = new Matrix3x3[nJoints];
            bodyPoseRotationsInUnity = new Matrix3x3[nJoints];
        }
        
        for (int i = 0; i < nJoints; i++)
        {
            int offset = i * Constants.ROT_FEAT_BY_JOINT;
            bodyPoseRotations[i] = Matrix3x3.From6d(
               bodyPoseData[offset + 0], bodyPoseData[offset + 1], bodyPoseData[offset + 2],
               bodyPoseData[offset + 3], bodyPoseData[offset + 4], bodyPoseData[offset + 5]
            );

            //Convert to Unity Axis System
            bodyPoseRotationsInUnity[i] = TransformData.LocalAMASSToUnityAxisSystem(bodyPoseRotations[i]);
        }

        if (verbose){
            /* Before transform */
            for (int i = 0; i < nJoints; i++)
            {
                LogVRStateDebug.LogVRState($"Body Pose Output Joint {i}", Vector3.zero, bodyPoseRotations[i]);
            }
        }

        //RnnStatesTensor has a shape of (n_block, n_channels, [h,c], 1, 1, HIDDEN_STATE_SIZE)
        //flatten the rnnStatesData array, should be [h_block_channel, c_block_channel], e.g h00, c00, h01, c01, h10, c10, h11, c11 etc.

        int totalSize = Constants.NBLOCK * Constants.NCHANNEL * 2 * Constants.HIDDEN_STATE_SIZE;
        
        // Optimize: Allocate only once
        if (flattenedStates == null || flattenedStates.Length != totalSize)
        {
            flattenedStates = new float[totalSize];
        }
        
        // rnnStatesData is already flat in memory from the tensor, and dimensions are sequential.
        // Tensor shape: (2, 5, 2, 1, 1, 256) -> already fits the iterators order directly.
        // We can just copy the data if the layout matches.
        // Layout: dim0(layers), dim1(channels), dim2(h/c), ..., dimN(hidden_size)
        // This naturally serializes to h_00, c_00, h_01, c_01, ... which is exactly what we need.
        
        if (rnnStatesData.Length == totalSize){
            NativeArray<float>.Copy(rnnStatesData, flattenedStates, totalSize);
        }
        else{
            Debug.LogError($"RNN State mismatch! Expected {totalSize}, got {rnnStatesData.Length}");
        }

        /* Convert to Unity Axis System */
        rootPositionInUnity = TransformData.GlobalAMASSToUnityAxisSystem(rootPosition);
        globalOrientInUnity = TransformData.GlobalAMASSToUnityAxisSystem(globalOrient);

        // IMPORTANT: Dispose the NativeArray when done to prevent memory leaks
        rootPosData.Dispose();
        globalOrientData.Dispose();
        bodyPoseData.Dispose();
        rnnStatesData.Dispose();


        //Also, dispose torch tensors-like
        RootPosTensor.Dispose();
        GlobalOrientTensor.Dispose();
        BodyPoseTensor.Dispose();
        RnnStatesTensor.Dispose();

    }

    /* Getters */
    /// <summary>
    /// Gets the processed local rotation matrices for each body joint, converted to the Unity axis system.
    /// </summary>
    /// <returns>An array of <see cref="Matrix3x3"/> representing body joint rotations.</returns>
    public Matrix3x3[] GetBodyPoseRotations(){
        return bodyPoseRotationsInUnity;
    }
    /// <summary>
    /// Gets the estimated root position transformed to the Unity axis system.
    /// </summary>
    /// <returns>The root position as a <see cref="Vector3"/>.</returns>
    public Vector3 GetRootPosition(){
        return rootPositionInUnity;
    }
    /// <summary>
    /// Gets the predicted global orientation transformed to the Unity axis system.
    /// </summary>
    /// <returns>The root orientation as a <see cref="Matrix3x3"/>.</returns>
    public Matrix3x3 GetGlobalOrientation(){
        return globalOrientInUnity;
    }
    /// <summary>
    /// Gets the extracted SMPL/AMASS shape parameter betas.
    /// </summary>
    /// <returns>An array of float values representing shape parameters.</returns>
    public float[] GetBetas(){
        return betas;
    }
    /// <summary>
    /// Retrieves the cached RNN/LSTM hidden and cell state sequence from the previous inference cycle.
    /// </summary>
    /// <param name="_isFirstFrame">If set to <c>true</c>, returns a fresh zero-initialized state array for initial frame execution.</param>
    /// <returns>A flat array containing sequential recurrent hidden states.</returns>
    public static float[] GetPrevStates(bool _isFirstFrame){
        if (_isFirstFrame || flattenedStates == null){
            // Note: new float[] is automatically filled with 0s in C#
            float[] _newfloat = new float[Constants.NBLOCK * Constants.NCHANNEL * Constants.HIDDEN_STATE_SIZE * 2];
            return _newfloat;
        }
        return flattenedStates;
    }

    /* Debug function to remove after validation */

    // public void _SyncInferenceDebug(Worker worker, TMP_Text fpsText){

    //     if (worker ==  null)   
    //     {
    //         Debug.LogError("Worker is null");
    //         return;
    //     }

    //     //Create Input
    //     float[] _data = new float[10600];
    //     for (int i = 0; i < 10600; i++){
    //         _data[i] = UnityEngine.Random.Range(-1f, 1f);
    //     }
        
    //     using (Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 1, 10600), _data)){
    //         //Debug.Log($"Input Tensor Shape: {inputTensor.shape}");
            

    //         //Inference
    //         worker.Schedule(inputTensor);

    //         //Get outputs
    //         Tensor<float> RootPosTensor = worker.PeekOutput("root_pos") as Tensor<float>;
    //         Tensor<float> BetasTensor = worker.PeekOutput("betas_pred") as Tensor<float>;
    //         Tensor<float> GlobalOrientTensor = worker.PeekOutput("global_orient_6d_pred") as Tensor<float>;
    //         Tensor<float> BodyPoseTensor = worker.PeekOutput("body_pose_6d_pred") as Tensor<float>;

    //         //Debug.Log($"RootPosTensor Shape: {RootPosTensor.shape} | BetasTensor Shape: {BetasTensor.shape} | GlobalOrientTensor Shape: {GlobalOrientTensor.shape} | BodyPoseTensor Shape: {BodyPoseTensor.shape}");

    //         // Fast access: Download tensor data to NativeArray
    //         NativeArray<float> rootPosData = RootPosTensor.DownloadToNativeArray();
    //         NativeArray<float> betasData = BetasTensor.DownloadToNativeArray();
    //         NativeArray<float> globalOrientData = GlobalOrientTensor.DownloadToNativeArray();
    //         NativeArray<float> bodyPoseData = BodyPoseTensor.DownloadToNativeArray();

            
    //         // Create Vector3 directly from NativeArray (fastest method - direct indexing)
    //         rootPosition = new Vector3(rootPosData[0], rootPosData[1], rootPosData[2]);
    //         globalOrient = Matrix3x3.From6d(globalOrientData);
            
    //         int nJoints = bodyPoseData.Length / Constants.ROT_FEAT_BY_JOINT;
            
    //         if (bodyPoseRotations == null || bodyPoseRotations.Length != nJoints)
    //         {
    //             bodyPoseRotations = new Matrix3x3[nJoints];
    //         }
            
    //         for (int i = 0; i < nJoints; i++)
    //         {
    //             int offset = i * Constants.ROT_FEAT_BY_JOINT;    
    //             bodyPoseRotations[i] = Matrix3x3.From6d(
    //                 bodyPoseData[offset + 0], bodyPoseData[offset + 1], bodyPoseData[offset + 2],
    //                 bodyPoseData[offset + 3], bodyPoseData[offset + 4], bodyPoseData[offset + 5]
    //             );
    //         }

    //         // IMPORTANT: Dispose the NativeArray when done to prevent memory leaks
    //         rootPosData.Dispose();
    //         globalOrientData.Dispose();
    //         bodyPoseData.Dispose();
    //         betasData.Dispose();


    //         //Also, dispose torch tensors-like
    //         RootPosTensor.Dispose();
    //         BetasTensor.Dispose();
    //         GlobalOrientTensor.Dispose();
    //         BodyPoseTensor.Dispose();

    //     }
    // }

    // public async Task _AsyncInferenceDebug(Worker worker, TMP_Text fpsText){
    //     if (worker == null || isInferencing) return;
    //     isInferencing = true;

    //     //1. Create Input
    //     float[] _data = new float[360];
    //     for (int i = 0; i < 360; i++) _data[i] = UnityEngine.Random.Range(-1f, 1f);

    //     using (Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 1, 360), _data))
    //     {
    //         System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
    //         stopwatch.Start();

    //         // 2. Schedule GPU work (Non-blocking)
    //         worker.Schedule(inputTensor);

    //         // 3. Get Output References (Non-blocking)
    //         var RootPosTensor = worker.PeekOutput("root_pos") as Tensor<float>;
    //         var BodyPoseTensor = worker.PeekOutput("body_pose_6d_pred") as Tensor<float>;
    //         var GlobalOrientTensor = worker.PeekOutput("global_orient_6d_pred") as Tensor<float>;
            
    //         // 4. AWAIT the result (Releases the Main Thread so Unity keeps running)
    //         // This is the key fix: It does not block FixedUpdate or Update!

    //         var rootPosResult = await RootPosTensor.ReadbackAndCloneAsync();
    //         var bodyPoseResult = await BodyPoseTensor.ReadbackAndCloneAsync();
    //         var globalOrientResult = await GlobalOrientTensor.ReadbackAndCloneAsync();
            
    //         // 5. Process Data (Now we are back on the main thread with data ready)
    //         // Note: ReadbackAndCloneAsync returns a standard Tensor, so we can access .data directly or usually ReadonlyArray
            
    //         // Map your data here...
    //         // (Simulated mapping for brevity)
    //         //ProcessResults(rootPosResult, bodyPoseResult, globalOrientResult);

    //         stopwatch.Stop();
    //         if (fpsText != null) fpsText.text = $"Inference: {stopwatch.Elapsed.TotalMilliseconds:F2} ms";

    //         // Dispose Async copies
    //         rootPosResult.Dispose();
    //         bodyPoseResult.Dispose();
    //         globalOrientResult.Dispose();
    //     }

    //     isInferencing = false;
        
    // }

    // public void GetLayersNameDebug(Model runtimeModel){
    //     foreach (var output in runtimeModel.outputs) {
    //         Debug.Log(output.name);
    //     }
    // }


}

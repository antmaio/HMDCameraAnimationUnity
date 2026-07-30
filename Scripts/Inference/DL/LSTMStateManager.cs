// using UnityEngine;

// using System.Collections.Generic;
// using System;

// using System.Linq;
// using System.IO;

// public class LSTMStateManager {

//     [Serializable]
//     public class StateMetadata
//     {
//         public int motion_id;
//         public int batch_idx;
//         public int frame_idx;
//         public int num_blocks;
//         public int num_channels;
//     }

//     [Serializable]
//     public class LSTMStateVectors
//     {
//         public List<float> h;
//         public List<float> c;
//     }

//     [Serializable]
//     public class ChannelStateEntry
//     {
//         public int block;
//         public int channel;
//         public LSTMStateVectors input;
//         public LSTMStateVectors output;
//     }

//     [Serializable]
//     public class StateDebugFile
//     {
//         public StateMetadata metadata;
//         public List<ChannelStateEntry> hidden_states;
//     }

//     private const int HIDDEN_STATE_SIZE = 256;
//     private StateDebugFile _loadedData;    
//     private Unity.InferenceEngine.Worker worker;
//     // We store the tensors to pass them to the next frame
//     private Dictionary<string, Unity.InferenceEngine.Tensor<float>> rnnStates = new Dictionary<string, Unity.InferenceEngine.Tensor<float>>();
//     // This dictionary holds the actual data tensors for the states
//     private Dictionary<string, Unity.InferenceEngine.Tensor<float>> persistentStates = new Dictionary<string, Unity.InferenceEngine.Tensor<float>>();
//     // Mapping: Key = Input Name, Value = Output Name
//     private Dictionary<string, string> stateMap;
    

//     // The name you found in Netron
//     const string HIDDEN_OUT_NAME = "/temporal_encoder.1.3/LSTM_output_1";
//     const string HIDDEN_IN_NAME = "the_input_name_from_netron"; 

//     public void SetupStateMapping() {
//         // You need to manually pair them once based on your Netron names
//         // Example for one layer:
//         stateMap.Add("input_h_state_0", "/temporal_encoder.1.3/LSTM_output_1");
//         stateMap.Add("input_c_state_0", "/temporal_encoder.1.3/LSTM_output_2");
//         // ... Repeat for all layers or use a loop if names are predictable
//     }

//     public void StateUpdate(Unity.InferenceEngine.Tensor<float> inputFeatures) {
//         // 1. Set the main input
//         worker.SetInput("input", inputFeatures);

//         // 2. Set the HIDDEN state from the PREVIOUS frame
//         if (rnnStates.ContainsKey(HIDDEN_IN_NAME)) {
//             worker.SetInput(HIDDEN_IN_NAME, rnnStates[HIDDEN_IN_NAME]);
//         } else {
//             // First frame: initialize with zeros (check your hidden size, e.g., 256)
//             var zeroState = new Unity.InferenceEngine.Tensor<float>(new Unity.InferenceEngine.TensorShape(1, 1, 256), clearOnInit: true);
//             worker.SetInput(HIDDEN_IN_NAME, zeroState);
//         }

//         // 3. Run model
//         worker.Schedule();

//         // 4. Capture the NEW state for the NEXT frame
//         // We use PeekOutput to get a reference
//         Unity.InferenceEngine.Tensor<float> newState = worker.PeekOutput(HIDDEN_OUT_NAME) as Unity.InferenceEngine.Tensor<float>;

//         // 5. IMPORTANT: You must Clone it. 
//         // PeekOutput belongs to the worker and will be overwritten/deleted next frame.
//         //if (rnnStates.ContainsKey(HIDDEN_IN_NAME)) rnnStates[HIDDEN_IN_NAME].Dispose();
//         //rnnStates[HIDDEN_IN_NAME] = newState.Clone();
//     }

//     public void AutoDetectStatesInONNXOutput(Unity.InferenceEngine.Model runtimeModel) {
//         // We want to find outputs that match the Netron pattern you saw
//         Debug.Log("--- Detecting LSTM State Tensors ---");
        
//         foreach (var output in runtimeModel.outputs) {
//             // Look for the pattern you found: "/temporal_encoder.../LSTM_output_1" (Y_h)
//             // or "/temporal_encoder.../LSTM_output_2" (Y_c)
//             if (output.name.Contains("LSTM_output")) {
//                 Debug.Log($"Found State Output: {output.name}");
//             }
//         }

//         foreach (var input in runtimeModel.inputs) {
//             // Hidden states usually have 'h' or 'c' or are named after the initial state
//             Debug.Log($"Possible State Input: {input.name}");
//         }
//     }

//     /* DEBUG FUNCTION */
//     public void LoadJson(){
//         string path = Path.Combine(Application.dataPath, "DebugFiles", "_state_frame5.json");

//         if (!File.Exists(path)) {
//             Debug.LogError("File not found: " + path);
//             return;
//         }

//         string jsonContent = File.ReadAllText(path);
//         _loadedData = JsonUtility.FromJson<StateDebugFile>(jsonContent);
//         Debug.Log($"Loaded Frame {_loadedData.metadata.frame_idx}. " +
//                 $"Entries: {_loadedData.hidden_states.Count}");
//     }

//     /// <summary>
//     /// Converts a specific saved vector from the JSON into a Sentis Tensor
//     /// </summary>
//     public Unity.InferenceEngine.Tensor<float> GetTensorFromDebug(int block, int channel, bool isHiddenState)
//     {
//         // Find the specific block/channel entry
//         var entry = _loadedData.hidden_states.FirstOrDefault(x => x.block == block && x.channel == channel);
        
//         if (entry == null) return null;

//         // Use the 'input' vectors to seed the model
//         float[] dataArray = isHiddenState ? entry.input.h.ToArray() : entry.input.c.ToArray();

//         // Create Tensor [Batch: 1, Sequence: 1, HiddenSize: X]
//         return new Unity.InferenceEngine.Tensor<float>(new Unity.InferenceEngine.TensorShape(1, 1, dataArray.Length), dataArray);
//     }

//     public void ReadDebugStateFile()
//     {
//         LoadJson();
//     }

//     public float[] GetFlattenedInputHiddenStatesDebug()
//     {
//         if (_loadedData == null)
//         {
//             ReadDebugStateFile();
//         }

//         if (_loadedData == null || _loadedData.hidden_states == null)
//         {
//             Debug.LogError("No loaded data available.");
//             return new float[0];
//         }

//         List<float> flatStates = new List<float>();

//         // Sort to ensure deterministic order (Block 0..N, each Channel 0..M)
//         var sorted = _loadedData.hidden_states
//             .OrderBy(x => x.block)
//             .ThenBy(x => x.channel)
//             .ToList();

//         foreach (var state in sorted)
//         {
//             // Interleaved: h, c, h, c...
//             if (state.input.h != null) flatStates.AddRange(state.input.h);
//             if (state.input.c != null) flatStates.AddRange(state.input.c);
//         }


//         return flatStates.ToArray();
//     }

//     public int GetHiddenStateSize(){
//         return HIDDEN_STATE_SIZE;
//     }

// }
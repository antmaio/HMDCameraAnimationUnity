// using UnityEngine;
// using System.IO;
// using System.Collections.Generic;
// using System.Text;
// using UnityEngine.InputSystem;

// /// <summary>
// /// Records secondary cameras to MJPEG AVI files.
// /// Frames are captured synchronously in LateUpdate (no coroutines) so they
// /// are guaranteed to be in the buffer when OnApplicationQuit fires.
// /// Output: <ProjectRoot>/Recordings/<timestamp>/<CameraName>.avi
// /// </summary>
// public class SecondaryCameraRecorder : MonoBehaviour
// {
//     [Header("Cameras to record (exclude your XR camera)")]
//     public Camera[] camerasToRecord;

//     [Header("Output")]
//     [Tooltip("Frames per second to capture and encode into the video.")]
//     public float targetFPS = 60f;
//     public int outputWidth = 1920;
//     public int outputHeight = 1080;
//     [Range(1, 100)]
//     [Tooltip("JPEG quality per frame. Lower = less RAM used during recording.")]
//     public int jpegQuality = 85;

//     [Header("Control")]
//     public bool recordOnPlay = true;
//     [Tooltip("Toggle recording on/off during play mode with this key.")]
//     public Key toggleKey = Key.R;

//     // ── Internals ──────────────────────────────────────────────────────────
//     private RenderTexture[] renderTextures;
//     private Texture2D[] readbackBuffers;
//     private string[] outputPaths;
//     private List<byte[]>[] frameBuffers;
//     private bool isRecording;
//     private float captureInterval;
//     private float nextCaptureTime;
//     private bool skipFirstFrame; // RenderTexture may be empty before first camera render
//     private string sessionRoot;

//     void Start()
//     {
//         if (camerasToRecord == null || camerasToRecord.Length == 0)
//         {
//             Debug.LogWarning("[Recorder] No cameras assigned.");
//             return;
//         }

//         string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
//         sessionRoot = Path.Combine(
//             Directory.GetParent(Application.dataPath).FullName,
//             "Recordings", timestamp);
//         Directory.CreateDirectory(sessionRoot);

//         int n = camerasToRecord.Length;
//         renderTextures = new RenderTexture[n];
//         readbackBuffers = new Texture2D[n];
//         outputPaths = new string[n];
//         frameBuffers = new List<byte[]>[n];

//         for (int i = 0; i < n; i++)
//         {
//             Camera cam = camerasToRecord[i];
//             if (cam == null) continue;

//             string safeName = cam.name.Replace(" ", "_");
//             outputPaths[i] = Path.Combine(sessionRoot, $"{safeName}.avi");
//             frameBuffers[i] = new List<byte[]>();

//             renderTextures[i] = new RenderTexture(outputWidth, outputHeight, 24, RenderTextureFormat.ARGB32);
//             renderTextures[i].antiAliasing = 1;
//             renderTextures[i].Create();
//             cam.targetTexture = renderTextures[i];

//             readbackBuffers[i] = new Texture2D(outputWidth, outputHeight, TextureFormat.RGB24, false);
//             Debug.Log($"[Recorder] Camera '{cam.name}' set up → {outputPaths[i]}");
//         }

//         captureInterval = 1f / Mathf.Max(targetFPS, 1f);
//         nextCaptureTime = Time.time;
//         skipFirstFrame = true;

//         if (recordOnPlay) StartRecording();
//     }

//     void LateUpdate()
//     {
//         // Key toggle check
//         if (Keyboard.current != null && toggleKey != Key.None && Keyboard.current[toggleKey].wasPressedThisFrame)
//         {
//             if (isRecording) StopRecording();
//             else StartRecording();
//         }

//         if (!isRecording) return;

//         // Skip the very first LateUpdate: cameras haven't rendered to the
//         // RenderTextures yet this session, so they'd contain garbage or black.
//         if (skipFirstFrame)
//         {
//             skipFirstFrame = false;
//             return;
//         }

//         if (Time.time < nextCaptureTime) return;
//         nextCaptureTime += captureInterval;

//         // ── Capture all cameras synchronously ─────────────────────────────
//         // At LateUpdate time, cameras have already rendered to their RenderTextures
//         // in the PREVIOUS frame. Content is valid and stable — no coroutine needed.
//         for (int i = 0; i < camerasToRecord.Length; i++)
//         {
//             if (camerasToRecord[i] == null || renderTextures[i] == null) continue;

//             RenderTexture prev = RenderTexture.active;
//             RenderTexture.active = renderTextures[i];
//             readbackBuffers[i].ReadPixels(new Rect(0, 0, outputWidth, outputHeight), 0, 0, false);
//             readbackBuffers[i].Apply(false);
//             RenderTexture.active = prev;

//             frameBuffers[i].Add(readbackBuffers[i].EncodeToJPG(jpegQuality));
//         }

//         // Progress log every 60 captured frames
//         int count = frameBuffers[0]?.Count ?? 0;
//         if (count > 0 && count % 60 == 0)
//             Debug.Log($"[Recorder] {count} frames captured so far...");
//     }

//     public void StartRecording()
//     {
//         if (isRecording) return;
//         isRecording = true;
//         skipFirstFrame = true; // reset so the first frame after (re)start is also safe
//         Debug.Log("[Recorder] ▶ Recording started. Press R to pause.");
//     }

//     public void StopRecording()
//     {
//         if (!isRecording) return;
//         isRecording = false;
//         int count = frameBuffers?[0]?.Count ?? 0;
//         Debug.Log($"[Recorder] ⏸ Paused at {count} frames. Video written on Play Mode exit.");
//     }

//     void OnApplicationQuit()
//     {
//         isRecording = false;

//         if (frameBuffers == null) return;

//         for (int i = 0; i < camerasToRecord.Length; i++)
//         {
//             if (camerasToRecord[i] == null) continue;

//             List<byte[]> frames = frameBuffers[i];

//             if (frames == null || frames.Count == 0)
//             {
//                 Debug.LogWarning($"[Recorder] '{camerasToRecord[i].name}': 0 frames — " +
//                                   "check that recording was active (press R) before stopping.");
//                 continue;
//             }

//             Debug.Log($"[Recorder] Writing {frames.Count} frames @ {targetFPS} fps → {outputPaths[i]}");

//             try
//             {
//                 MjpegAviWriter.Write(outputPaths[i], frames, outputWidth, outputHeight, targetFPS);
//                 float duration = frames.Count / targetFPS;
//                 Debug.Log($"[Recorder] ✓ '{camerasToRecord[i].name}' saved — {duration:F2}s ({frames.Count} frames)");
//             }
//             catch (System.Exception e)
//             {
//                 Debug.LogError($"[Recorder] Write failed for '{camerasToRecord[i].name}': {e}");
//             }
//         }
//     }

//     void OnDestroy()
//     {
//         if (renderTextures != null)
//             foreach (var rt in renderTextures)
//                 if (rt != null) rt.Release();

//         if (readbackBuffers != null)
//             foreach (var t in readbackBuffers)
//                 if (t != null) Destroy(t);
//     }
// }


// /// <summary>
// /// Pure C# MJPEG AVI 1.0 writer. No external dependencies.
// /// Produces files readable by VLC, ffmpeg, Windows Media Player, etc.
// /// </summary>
// public static class MjpegAviWriter
// {
//     public static void Write(string path, List<byte[]> jpegFrames, int width, int height, float fps)
//     {
//         int frameCount = jpegFrames.Count;
//         int scale = 1000;
//         int rate = Mathf.RoundToInt(fps * scale);
//         int microSecPerFrame = Mathf.RoundToInt(1_000_000f / fps);

//         int maxFrameSize = 0;
//         foreach (var f in jpegFrames)
//             if (f.Length > maxFrameSize) maxFrameSize = f.Length;

//         using var ms = new MemoryStream(frameCount * (maxFrameSize + 8) + 8192);
//         using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

//         // ── RIFF AVI ──────────────────────────────────────────────────────
//         Write4CC(bw, "RIFF");
//         long riffSizePos = ms.Position; bw.Write(0);
//         Write4CC(bw, "AVI ");

//         // ── LIST hdrl ─────────────────────────────────────────────────────
//         Write4CC(bw, "LIST");
//         long hdrlSizePos = ms.Position; bw.Write(0);
//         Write4CC(bw, "hdrl");

//         Write4CC(bw, "avih"); bw.Write(56);
//         bw.Write(microSecPerFrame);
//         bw.Write(rate * maxFrameSize / scale);
//         bw.Write(0);
//         bw.Write(0x00000910);
//         bw.Write(frameCount);
//         bw.Write(0);
//         bw.Write(1);
//         bw.Write(maxFrameSize);
//         bw.Write(width);
//         bw.Write(height);
//         bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);

//         Write4CC(bw, "LIST");
//         long strlSizePos = ms.Position; bw.Write(0);
//         Write4CC(bw, "strl");

//         Write4CC(bw, "strh"); bw.Write(56);
//         Write4CC(bw, "vids");
//         Write4CC(bw, "MJPG");
//         bw.Write(0);
//         bw.Write((short)0);
//         bw.Write((short)0);
//         bw.Write(0);
//         bw.Write(scale);
//         bw.Write(rate);
//         bw.Write(0);
//         bw.Write(frameCount);
//         bw.Write(maxFrameSize);
//         bw.Write(-1);
//         bw.Write(0);
//         bw.Write((short)0); bw.Write((short)0);
//         bw.Write((short)width); bw.Write((short)height);

//         Write4CC(bw, "strf"); bw.Write(40);
//         bw.Write(40);
//         bw.Write(width);
//         bw.Write(height);
//         bw.Write((short)1);
//         bw.Write((short)24);
//         Write4CC(bw, "MJPG");
//         bw.Write(width * height * 3);
//         bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);

//         Patch(bw, ms, strlSizePos);
//         Patch(bw, ms, hdrlSizePos);

//         // ── LIST movi ─────────────────────────────────────────────────────
//         long moviListStart = ms.Position;
//         Write4CC(bw, "LIST");
//         long moviSizePos = ms.Position; bw.Write(0);
//         Write4CC(bw, "movi");

//         var idxOffsets = new int[frameCount];
//         var idxSizes = new int[frameCount];

//         for (int f = 0; f < frameCount; f++)
//         {
//             byte[] jpeg = jpegFrames[f];
//             idxOffsets[f] = (int)(ms.Position - moviListStart);
//             idxSizes[f] = jpeg.Length;
//             Write4CC(bw, "00dc");
//             bw.Write(jpeg.Length);
//             bw.Write(jpeg);
//             if (jpeg.Length % 2 != 0) bw.Write((byte)0);
//         }

//         Patch(bw, ms, moviSizePos);

//         // ── idx1 ──────────────────────────────────────────────────────────
//         Write4CC(bw, "idx1");
//         bw.Write(frameCount * 16);
//         for (int f = 0; f < frameCount; f++)
//         {
//             Write4CC(bw, "00dc");
//             bw.Write(0x00000010);
//             bw.Write(idxOffsets[f]);
//             bw.Write(idxSizes[f]);
//         }

//         Patch(bw, ms, riffSizePos);
//         bw.Flush();

//         File.WriteAllBytes(path, ms.ToArray());
//     }

//     static void Patch(BinaryWriter bw, MemoryStream ms, long sizeFieldPos)
//     {
//         long end = ms.Position;
//         ms.Position = sizeFieldPos;
//         bw.Write((int)(end - sizeFieldPos - 4));
//         ms.Position = end;
//     }

//     static void Write4CC(BinaryWriter bw, string cc)
//     {
//         bw.Write((byte)cc[0]);
//         bw.Write((byte)cc[1]);
//         bw.Write((byte)cc[2]);
//         bw.Write((byte)cc[3]);
//     }
// }

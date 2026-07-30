using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// Minimal UDP/OSC listener. Parses a single OSC message containing N floats.
/// No third-party package required.
/// </summary>
public class OscReceiver : MonoBehaviour
{
    [Header("Network")]
    public int listenPort = 9000;

    /// <summary>Fired on the main thread with the latest float payload.</summary>
    public event Action<float[]> OnFloatsReceived;

    private UdpClient _udp;
    private Thread _thread;
    private float[] _pending;
    private readonly object _lock = new();
    private bool _running;

    void OnEnable()
    {
        _udp = new UdpClient(listenPort);
        _running = true;
        _thread = new Thread(ReceiveLoop) { IsBackground = true };
        _thread.Start();
        Debug.Log($"[OSC] Listening on UDP :{listenPort}");
    }

    void OnDisable()
    {
        _running = false;
        _udp?.Close();
        _thread?.Join(500);
    }

    void FixedUpdate()
    {
        float[] snapshot;
        lock (_lock) { snapshot = _pending; _pending = null; }
        if (snapshot != null) OnFloatsReceived?.Invoke(snapshot);
    }

    // ── Background thread ────────────────────────────────────────────────────

    private void ReceiveLoop()
    {
        IPEndPoint ep = new(IPAddress.Any, listenPort);
        while (_running)
        {
            try
            {
                byte[] data = _udp.Receive(ref ep);
                float[] floats = ParseOscFloats(data);
                if (floats != null)
                    lock (_lock) { _pending = floats; };
            }
            catch (SocketException) {  break; }   // socket closed on disable
            catch (Exception e) { Debug.LogWarning($"[OSC] {e.Message}"); }
        }
    }

    /// <summary>
    /// Parses a minimal OSC message: address (padded to 4 bytes) + type tag string + float32 arguments.
    /// </summary>
    private static float[] ParseOscFloats(byte[] data)
    {
        int offset = 0;

        // --- Address string (null-terminated, padded to multiple of 4) ---
        int addrEnd = Array.IndexOf(data, (byte)0, offset);
        if (addrEnd < 0) return null;
        offset = Align4(addrEnd + 1);

        // --- Type tag string (starts with ',') ---
        if (offset >= data.Length || data[offset] != (byte)',') return null;
        int tagEnd = Array.IndexOf(data, (byte)0, offset);
        if (tagEnd < 0) return null;

        // Count 'f' tags
        int floatCount = 0;
        for (int i = offset + 1; i < tagEnd; i++)
            if (data[i] == (byte)'f') floatCount++;

        offset = Align4(tagEnd + 1);
        if (floatCount == 0) return null;

        // --- Float arguments (big-endian IEEE 754) ---
        var floats = new float[floatCount];
        for (int i = 0; i < floatCount; i++)
        {
            if (offset + 4 > data.Length) break;
            byte[] b = new byte[4] { data[offset + 3], data[offset + 2], data[offset + 1], data[offset] };
            floats[i] = BitConverter.ToSingle(b, 0);
            offset += 4;
        }
        return floats;
    }

    private static int Align4(int n) => (n + 3) & ~3;
}
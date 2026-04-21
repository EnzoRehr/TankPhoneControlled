using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;

public class TankSocket : WebSocketBehavior
{
    protected override void OnOpen()
    {
        Debug.Log("[Bridge] Phone connected!");
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        Debug.Log("[Bridge] Got: " + e.Data);
        lock (NetworkInputBridge.QueueLock)
            NetworkInputBridge.MessageQueue.Enqueue(e.Data);
    }

    protected override void OnClose(CloseEventArgs e)
    {
        Debug.Log("[Bridge] Phone disconnected: " + e.Reason);
    }

    protected override void OnError(WebSocketSharp.ErrorEventArgs e)
    {
        Debug.LogError("[Bridge] Socket error: " + e.Message);
    }
}

public class NetworkInputBridge : MonoBehaviour
{
    public int port = 7777;

    public static float RemoteThrottle = 0f;
    public static float RemoteSteering = 0f;
    public static bool RemoteFirePressed = false;
    public static bool RemoteFireHeld = false;
    public static bool RemoteLauncherToggle = false;
    public static float RemoteAimX = 0f;
    public static float RemoteAimY = 0f;
    public static bool RemoteBoost = false;
    public static bool RemoteTrackLeft = false;
    public static bool RemoteTrackRight = false;

    public static Queue<string> MessageQueue = new Queue<string>();
    public static readonly object QueueLock = new object();

    private static WebSocketServer _server;

    void Start()
    {
        Debug.Log("[Bridge] Starting on port " + port);
        try
        {
            _server = new WebSocketServer(System.Net.IPAddress.Any, port);
            _server.AddWebSocketService<TankSocket>("/tank");
            _server.Start();
            Debug.Log("[Bridge] Server running on all interfaces, port " + port);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Bridge] Failed to start: " + e.Message);
        }
    }

    void Update()
    {
        RemoteFirePressed = false;
        RemoteLauncherToggle = false;

        lock (QueueLock)
        {
            while (MessageQueue.Count > 0)
                ParseMessage(MessageQueue.Dequeue());
        }
    }

    // Called from TankMovement every frame to broadcast real fuel to phone
    public static void BroadcastFuel(float fuelPercent)
    {
        if (_server == null || !_server.IsListening) return;
        string msg = "FUEL:" + fuelPercent.ToString("F2",
            System.Globalization.CultureInfo.InvariantCulture);
        try { _server.WebSocketServices["/tank"].Sessions.Broadcast(msg); }
        catch { }
    }

    void ParseMessage(string msg)
    {
        if (msg.StartsWith("MOVE:"))
        {
            string[] p = msg.Split(':');
            if (p.Length == 3)
            {
                float.TryParse(p[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out RemoteThrottle);
                float.TryParse(p[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out RemoteSteering);
            }
        }
        else if (msg.StartsWith("AIM:"))
        {
            string[] p = msg.Split(':');
            if (p.Length == 3)
            {
                float.TryParse(p[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out RemoteAimX);
                float.TryParse(p[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out RemoteAimY);
            }
        }
        else if (msg == "FIRE_DOWN") { RemoteFirePressed = true; RemoteFireHeld = true; }
        else if (msg == "FIRE_UP") { RemoteFireHeld = false; }
        else if (msg == "FIRE") { RemoteFirePressed = true; RemoteFireHeld = true; }
        else if (msg == "LAUNCHER_TOGGLE") { RemoteLauncherToggle = true; }
        else if (msg == "BOOST_DOWN") { RemoteBoost = true; }
        else if (msg == "BOOST_UP") { RemoteBoost = false; }
        else if (msg == "TRACK_LEFT_DOWN") { RemoteTrackLeft = true; }
        else if (msg == "TRACK_LEFT_UP") { RemoteTrackLeft = false; }
        else if (msg == "TRACK_RIGHT_DOWN") { RemoteTrackRight = true; }
        else if (msg == "TRACK_RIGHT_UP") { RemoteTrackRight = false; }
    }

    void OnApplicationQuit()
    {
        Debug.Log("[Bridge] Stopping server");
        _server?.Stop();
    }
}
using Mirror;
using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;
using System;
using kcp2k;

/// <summary>
/// Runs on a host/server instance.
/// Responsible for:
/// - Configuring the game transport port
/// - Registering the host with the relay
/// - Sending signed heartbeats
/// - Notifying the relay when player count changes
/// </summary>
public class HostLogger : MonoBehaviour
{
    // ================================
    // Logging
    // ================================
    [SerializeField] private LogMode logMode = LogMode.All;
    public enum LogMode
    {
        ErrorsOnly,   // Production / live servers
        All           // Development / debugging
    }

    void Log(string msg)
    {
        if (logMode == LogMode.All)
            Debug.Log(msg);
    }

    void LogWarning(string msg)
    {
        if (logMode == LogMode.All)
            Debug.LogWarning(msg);
    }

    void LogError(string msg)
    {
        Debug.LogError(msg); // Always log errors
    }


    // ================================
    // Relay Configuration
    // ================================

    [Header("Relay")]
    [SerializeField] private string relayRegisteryIP = "127.0.0.1";
    [SerializeField] private int relayRegisteryPort = 9001;   // MUST match relay host registry port
    [SerializeField] private string hostId = "HostA"; // Unique per host instance

    // ================================
    // Game / Host Settings
    // ================================

    [Header("Game")]
    [SerializeField] private int gamePort = 9000;    // Mirror/KCP listen port
    [SerializeField] private int maxPlayers = 8;

    // ================================
    // Heartbeat Settings
    // ================================

    [Header("Heartbeat")]
    [Tooltip("How often the host sends heartbeats to the relay")]
    [SerializeField] private float heartbeatInterval = 11f;

    [Tooltip("Minimum gap between forced heartbeats")]
    [SerializeField] private float minHeartbeatGap = 0.25f;

    private float heartbeatTimer;
    private float lastHeartbeatTime;

    // ================================
    // Security
    // ================================

    // MUST match relay secret
    private const string HOST_SECRET = "CHANGE_ME_TO_SOMETHING_SECRET";

    // ================================
    // Internal State
    // ================================

    private UdpClient udp;
    private bool wasServerActive;
    private bool registered;

    // ================================
    // Unity Lifecycle
    // ================================

    /// <summary>
    /// Runs before networking starts.
    /// Sets the KCP transport port so the relay knows where to forward traffic.
    /// </summary>
    void Awake()
    {
        if (NetworkManager.singleton.transport is KcpTransport kcp)
        {
            kcp.Port = (ushort)gamePort;
            Log($"[Host] KCP port set to {gamePort}");
        }
        else
        {
            LogError("[Host] Active transport is not KCP");
        }
    }

    void OnEnable()
    {
        //NetworkServer.OnConnectedEvent += conn =>
        //    Debug.Log($"[Host] Client connected: {conn.connectionId}");

        //NetworkServer.OnDisconnectedEvent += conn =>
        //    Debug.Log($"[Host] Client disconnected: {conn.connectionId}");
        //NetworkServer.OnConnectedEvent += OnClientConnected;
        //NetworkServer.OnDisconnectedEvent += OnClientDisconnected;
    }

    void OnDisable()
    {
        // Cleanly remove callbacks (important for editor restarts)
        NetworkServer.OnConnectedEvent -= OnClientConnected;
        NetworkServer.OnDisconnectedEvent -= OnClientDisconnected;
    }

    void Start()
    {
        //Application.targetFrameRate = 1;
    }

    // ================================
    // Server State Detection
    // ================================

    void Update()
    {
        heartbeatTimer += Time.deltaTime;

        // Detect server start
        if (!wasServerActive && NetworkServer.active)
        {
            RegisterServerCallbacks();
            OnServerStarted();
        }

        // Detect server stop
        if (wasServerActive && !NetworkServer.active)
        {
            OnServerStopped();
        }

        wasServerActive = NetworkServer.active;

        // Heartbeat ticking
        if (!NetworkServer.active || udp == null)
            return;

        if (registered && heartbeatTimer >= heartbeatInterval)
        {
            SendHeartbeat();
            heartbeatTimer = 0f;
            lastHeartbeatTime = Time.time;
        }
    }

    // ================================
    // Server Callbacks
    // ================================

    void RegisterServerCallbacks()
    {
        NetworkServer.OnConnectedEvent += OnClientConnected;
        NetworkServer.OnDisconnectedEvent += OnClientDisconnected;
    }

    void OnServerStarted()
    {
        SendRegister();
    }

    void OnServerStopped()
    {
        registered = false;

        udp?.Close();
        udp = null;

        Log("[Host] Server stopped, relay registration ended");
    }

    // ================================
    // Client Count Tracking
    // ================================

    void OnClientConnected(NetworkConnectionToClient conn)
    {
        Log($"[Host] Client connected: {conn.connectionId}");
        ForceHeartbeat();
    }

    void OnClientDisconnected(NetworkConnectionToClient conn)
    {
        Log($"[Host] Client disconnected: {conn.connectionId}");
        ForceHeartbeat();
    }

    // ================================
    // Relay Communication
    // ================================

    /// <summary>
    /// Sends a signed REGISTER message to the relay.
    /// </summary>
    void SendRegister()
    {
        udp = new UdpClient();

        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string payload = $"{hostId}|{gamePort}|{maxPlayers}|{timestamp}";
        string signature = ComputeHmac(payload, HOST_SECRET);
        string msg = $"REGISTER|{payload}|{signature}";

        Send(msg);

        registered = true;
        heartbeatTimer = 0f;
        lastHeartbeatTime = Time.time;

        Log($"[Host] Registered with relay as {hostId}");
    }

    /// <summary>
    /// Sends a signed HEARTBEAT message to the relay.
    /// </summary>
    void SendHeartbeat()
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string payload = $"{hostId}|{gamePort}|{maxPlayers}|{timestamp}";
        string signature = ComputeHmac(payload, HOST_SECRET);

        string msg = $"HEARTBEAT|{payload}|{signature}";
        Send(msg);

        Log($"[Host] Heartbeat sent for {hostId}");
    }

    void Send(string msg)
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);
        udp.Send(data, data.Length, relayRegisteryIP, relayRegisteryPort);
    }

    // ================================
    // Shutdown Handling
    // ================================

    void OnApplicationQuit()
    {
        if (udp == null)
            return;

        try
        {
            // Why? it just prolongs relays removal timers
            //SendHeartbeat(); // Final signed heartbeat
            udp.Close();
        }
        catch { }
    }

    // ================================
    // HMAC Helpers
    // ================================

    static string ComputeHmac(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        // return System.Convert.ToHexString(hash); not available in older unity
    }

    // ================================
    // Heartbeat Acceleration
    // ================================

    /// <summary>
    /// Forces an immediate heartbeat when player count changes,
    /// without spamming the relay.
    /// </summary>
    void ForceHeartbeat()
    {
        if (!registered || udp == null)
            return;

        float now = Time.time;

        if (now - lastHeartbeatTime < minHeartbeatGap)
            return;

        SendHeartbeat();

        heartbeatTimer = 0f;
        lastHeartbeatTime = now;
    }

}

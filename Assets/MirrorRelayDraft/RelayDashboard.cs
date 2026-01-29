using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visual debug dashboard for the UDP relay.
/// Displays relay state, hosts, clients, and security counters.
/// Intended for development & diagnostics only.
/// </summary>
public class RelayDashboard : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || UNITY_STANDALONE

    // ================================
    // Logging
    // ================================

    public enum LogMode
    {
        ErrorsOnly, // Production / minimal logging
        All         // Development / verbose logging
    }

    [Header("Logging")]
    [SerializeField] private LogMode logMode = LogMode.All;

    void Log(string msg)
    {
        if (logMode == LogMode.All)
            Debug.Log(msg);
    }

    void LogError(string msg)
    {
        Debug.LogError(msg);
    }

    // ================================
    // References & UI State
    // ================================

    [Header("Relay Reference")]
    [Tooltip("Reference to the active MultiClientUdpRelay instance")]
    public MultiClientUdpRelay relay;

    [Header("Window Settings")]
    [Tooltip("Initial dashboard window size (currently full-screen adaptive)")]
    public Vector2 windowSize = new Vector2(400, 400);

    // Scroll position for the main dashboard window
    private Vector2 scrollPos;

    // ================================
    // Unity GUI
    // ================================

    void OnGUI()
    {
        // Relay reference missing → nothing useful to draw
        if (relay == null)
        {
            GUILayout.Label("Relay reference missing");
            return;
        }

        // Full-screen adaptive window with padding
        Rect windowRect = new Rect(
            Screen.width / 20,
            Screen.height / 20,
            Screen.width - (Screen.width / 10),
            Screen.height - (Screen.height / 10)
        );

        GUI.Window(1234, windowRect, DrawWindow, "Relay Dashboard");
    }

    /// <summary>
    /// Main window draw entry point.
    /// Handles scrolling and section layout.
    /// </summary>
    void DrawWindow(int id)
    {
        scrollPos = GUILayout.BeginScrollView(scrollPos);

        DrawRelay();
        GUILayout.Space(10);
        DrawHosts();
        GUILayout.Space(10);
        DrawClients();

        GUILayout.EndScrollView();

        // Allow dragging by top bar
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    // ================================
    // Relay Section
    // ================================

    /// <summary>
    /// Displays overall relay status, limits, and security counters.
    /// </summary>
    void DrawRelay()
    {
        GUILayout.Label("=== Relay ===", GUI.skin.box);

        GUILayout.Label($"Hosts: {relay.RegisteredHostCount} / {relay.MaxRegisteredHosts}");
        GUILayout.Label($"Clients: {relay.ConnectedClientCount} / {relay.MaxRelayClients}");

        GUILayout.BeginVertical("box");
        GUILayout.Label("Security / Limits");

        GUILayout.Label($"Pending Handshakes: {relay.PendingHandshakeCount}/{relay.MaxPendingHandshakes}");
        GUILayout.Label($"Blacklisted Endpoints: {relay.BlacklistedEndpointCount}");
        GUILayout.Label($"Rate-Limited Packets: {relay.TotalRateLimitedPackets}");

        GUILayout.Label($"Handshake Timeouts: {relay.HandshakeTimeoutCount}");
        GUILayout.Label($"Rejected Clients: {relay.RejectedClientCount}");
        GUILayout.Label($"Rejected Hosts: {relay.RejectedHostCount}");

        GUILayout.Label($"Active Reject Cooldowns: {relay.RejectCooldownCount}");
        GUILayout.Label($"Active Endpoint Blocks: {relay.EndpointBlockCount}");
        GUILayout.Label($"Rejected Host Packets: {relay.TotalRejectedHostPackets}");

        GUILayout.EndVertical();
    }

    // ================================
    // Hosts Section
    // ================================

    /// <summary>
    /// Displays registered hosts and their current state.
    /// </summary>
    void DrawHosts()
    {
        GUILayout.Label("=== Hosts ===", GUI.skin.box);

        var hosts = relay.GetRegisteredHosts();
        if (hosts.Count == 0)
        {
            GUILayout.Label("No registered hosts.");
            return;
        }

        foreach (var host in hosts)
        {
            GUILayout.BeginVertical("box");

            GUILayout.Label($"Host ID: {host.hostId}");
            GUILayout.Label($"Endpoint: {host.endpoint}");
            GUILayout.Label($"Players: {host.currentPlayers} / {host.maxPlayers}");
            GUILayout.Label($"Last heartbeat: {host.secondsSinceSeen:F1}s ago");

            GUILayout.Label($"Cooldown: {(relay.IsHostInCooldown(host.hostId) ? "YES" : "no")}");
            GUILayout.Label($"Rejected packets: {relay.HostRejectedCount(host.hostId)}");
            GUILayout.Label($"Rate-limit hits: {relay.HostRateLimitHits(host.hostId)}");

            GUILayout.EndVertical();
        }
    }

    // ================================
    // Clients Section
    // ================================

    /// <summary>
    /// Displays currently connected clients and their relay state.
    /// </summary>
    void DrawClients()
    {
        GUILayout.Label("=== Clients ===", GUI.skin.box);

        var clients = relay.ClientList();
        if (clients.Count == 0)
        {
            GUILayout.Label("No connected clients.");
            return;
        }

        foreach (var key in clients)
        {
            long sent = relay.BytesFromClient(key);
            long received = relay.BytesToClient(key);
            double remaining = relay.ClientTimeoutRemaining(key);
            bool handshake = relay.IsHandshake(key);
            string hostId = relay.GetClientHost(key);

            GUILayout.BeginVertical("box");

            GUILayout.Label($"Client: {key}");
            GUILayout.Label($"Host: {hostId}");
            GUILayout.Label($"Bytes → host: {sent}");
            GUILayout.Label($"Bytes ← host: {received}");

            if (handshake)
            {
                GUILayout.Label("Status: Handshaking");
            }
            else
            {
                GUILayout.Label($"Timeout in: {remaining:F1}s");
            }

            GUILayout.EndVertical();
        }
    }

#endif
}

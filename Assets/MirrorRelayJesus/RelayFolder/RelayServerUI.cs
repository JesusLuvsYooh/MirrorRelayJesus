using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OPTIONAL Visual debug dashboard for the relay.
/// Displays relay state, hosts, clients, and security counters.
/// Intended for development & diagnostics only.
/// </summary>
public class RelayServerUI : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || UNITY_STANDALONE

    public RelayServer relayServer;

    private Vector2 windowSize = new Vector2(0, 0); // sets to max window height and width
    private Vector2 scrollPos; // Scroll position for the main dashboard window

    void OnGUI()
    {
        // Relay reference missing → nothing useful to draw
        if (relayServer == null)
        {
            GUILayout.Label("Relay reference missing");
            return;
        }

        // Full-screen adaptive window with padding
        Rect windowRect = new Rect( 0, 0, Screen.width, Screen.height );
        //Rect windowRect = new Rect(
        //    Screen.width / 20,
        //    Screen.height / 20,
        //    Screen.width - (Screen.width / 10),
        //    Screen.height - (Screen.height / 10)
        //);

        GUI.Window(1234, windowRect, DrawWindow, "Relay Server UI");
    }

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
       // GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    void DrawRelay()
    {
        GUILayout.Label("=== Relay ===", GUI.skin.box);
        GUILayout.Label($"Hosts: {relayServer.relayServerHost.registeredHostInfo.Count} / {RelaySettings.maxRegisteredHosts}");
        GUILayout.Label($"Clients: {relayServer.relayServerClient.clientToHostMap.Count} / {RelaySettings.maxRelayClients}");

        GUILayout.Label($"Blocked Host IPs: {relayServer.relayServerHost.ipBlockedUntil.Count}");
        GUILayout.Label($"Blocked Client IPs: {relayServer.relayServerClient.ipBlockedUntil.Count}");

        GUILayout.Label($"Total Client strikes: {relayServer.relayServerClient.totalPacketsStrikes}");
    }

    void DrawHosts()
    {
        GUILayout.Label("=== Hosts ===", GUI.skin.box);

        var hostList = relayServer.relayServerHost.GetHostList();
        if (hostList.Count == 0)
        {
            GUILayout.Label("No registered hosts.");
            return;
        }

        foreach (var host in hostList)
        {
            GUILayout.BeginVertical("box");

            GUILayout.Label($"Host UID: {host.hostUID}");
            GUILayout.Label($"Endpoint: {host.hostIPEndpoint}");
            GUILayout.Label($"Players: {host.hostCurrentPlayers} / {host.hostMaxPlayers}");
            GUILayout.Label($"Last seen: {host.hostLastSeen}");

            GUILayout.EndVertical();
        }
    }

    void DrawClients()
    {
        GUILayout.Label("=== Clients ===", GUI.skin.box);

        var clients = relayServer.relayServerClient.ClientList();
        if (clients.Count == 0)
        {
            GUILayout.Label("No connected clients.");
            return;
        }

        foreach (var key in clients)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"Endpoint: {key}");
            GUILayout.EndVertical();
        }
    }

#endif
}

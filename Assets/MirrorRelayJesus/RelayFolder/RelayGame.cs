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
public class RelayGame : MonoBehaviour
{
    private UdpClient udp;
    private bool hostRegistered;
    private float lastHeartbeatTime = -999f;
    private string hostUID;
    private string payload;
    private string countryCode;

    /// Sets the KCP transport port so the relay knows where to forward traffic.
    void Awake()
    {
        if (NetworkManager.singleton.transport is KcpTransport kcp)
        {
            kcp.Port = RelaySettingsGame.gamePort;
            RelaySettingsShared.Log($"[Host] KCP port set to {RelaySettingsGame.gamePort}");
        }
        else
        {
            RelaySettingsShared.LogError("[Host] Active transport is not KCP");
        }

        hostUID = SystemInfo.deviceUniqueIdentifier.ToString();
        if (string.IsNullOrEmpty(hostUID) || hostUID.Contains("00000"))
        {
            // blank or all 0's likely means no permission or not supported, to use the identifier, so we set our own
            hostUID = new Guid().ToString();
            //save/load in player prefs so its same everytime?
        }

        // so we can pair players and hosts together, and give them minimal ping/latency for maximum player experience
        string countryCode = RelayCountryDetector.DetectCountryCode();
        print("Country Code: " + countryCode);
    }

    public void OnServerStarted()
    {
        udp = new UdpClient();
        SendRegister();
        InvokeRepeating(nameof(SendRegister), RelaySettingsGame.heartbeatInterval, RelaySettingsGame.heartbeatInterval);
    }

    public void OnServerStopped()
    {
        hostRegistered = false;
        CancelInvoke(nameof(SendRegister));
        udp?.Close();
        udp = null;
    }

    public void SendRegister()
    {
        if (NetworkServer.active == false)
        {
            RelaySettingsShared.LogError($"[Host] Server not started.");
            return;
        }

        if (hostRegistered == false)
        {
            hostRegistered = true;
            payload = "REGISTER|";
        }
        else
        {
            if (Time.time - lastHeartbeatTime < RelaySettingsGame.heartbeatGap)
            {
                RelaySettingsShared.Log($"[Host] Heartbeat sent too soon, skip it.");
                return;
            }
            lastHeartbeatTime = Time.time;
            payload = "HEARTBEAT|";
        }

        payload += $"{hostUID}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}|{RelaySettingsGame.gamePort}|{RelaySettingsGame.maxPlayers}";
        payload = RelaySettingsShared.Encrypt(payload, RelaySettingsShared.hostRegisterSecret);
        Send(payload);

        RelaySettingsShared.Log($"[Host] Sent register message.");
    }

    void Send(string msg)
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);
        udp.Send(data, data.Length, RelaySettingsGame.relayAddress, (int)RelaySettingsShared.hostRegisterPort);
    }

    void OnApplicationQuit()
    {
        if (udp == null)
            return;

        try
        {
            // Final signed heartbeat, if we want to instantly remove host rather than wait for removal timers
            //SendHeartbeat();
            udp.Close();
        }
        catch { }
    }
}

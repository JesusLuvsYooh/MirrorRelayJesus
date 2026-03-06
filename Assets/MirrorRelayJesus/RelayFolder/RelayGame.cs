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
    private bool hostRegistered; // if registration data changes, reset this, heartbeats to keep alive, not update database
    private float lastHeartbeatTime = -999f;
    //private string hostUID;
    private string payload;
    //private string countryCode;

    public long hostTimestamp = 0;
    // public IPEndPoint hostIPEndpoint;
    public string hostUID = ""; // used for extra security, identify bad users, backup of endpoint as id, for relay use only, not client
    public short hostCurrentPlayers = 0;
    public short hostMaxPlayers = 0;
    // public double hostLastSeen;
    public string hostCountryCode = "";
    public short hostPort = 0;
    public float hostVersion = 0;
    public string hostExtras = "";

    //[System.Serializable]
    //public class HostRegisterPayload
    //{
    //    public string hostId;
    //    public int gamePort;
    //    public int maxPlayers;
    //    public string extrasJson;
    //    public long timestamp;
    //}

    //public struct RegisteredHostInfo
    //{
    //    public long hostTimestamp;
    //    // public IPEndPoint hostIPEndpoint;
    //    public string hostUID; // used for extra security, identify bad users, backup of endpoint as id, for relay use only, not client
    //    public short hostCurrentPlayers;
    //    public short hostMaxPlayers;
    //   // public double hostLastSeen;
    //    public string hostCountryCode;
    //    public short hostPort;
    //    public float hostVersion;
    //    public string hostExtras;
    //}

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
        hostCountryCode = RelayCountryDetector.DetectCountryCode();
       // print("Country Code: " + hostCountryCode);
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

       // payload += $"{hostUID}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}|{RelaySettingsGame.gamePort}|{RelaySettingsGame.maxPlayers}";
        payload = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + "|" + hostUID + "|" + hostCurrentPlayers + "|" + RelaySettingsGame.maxPlayers + "|" + hostCountryCode + "|" + RelaySettingsGame.gamePort + "|" + hostVersion + "|" + hostExtras;
       // REGISTER | FEE00F36 - 8151 - 5001 - AC38 - AB6242AA1B6F | 1772788967 | 7777 | 8
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

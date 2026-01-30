using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RelayGameSettings : MonoBehaviour
{
    [Header("Relay")]
    //[Tooltip("If false, bypasses relay and connects directly to the server")]
    static public bool useRelay = true;

    //[Tooltip("Relay IP or hostname")]
    static public string relayAddress = "127.0.0.1";
    static public int gamePort = 9000;    // Mirror/KCP listen port, the in-game network manager/transport port
    [Header("Relay")]
    static public string relayRegisteryIP = "127.0.0.1";
    static public string hostId = "HostA"; // Unique per host instance

    [Header("Game")]
    static public int maxPlayers = 8;

    [Header("Heartbeat")]
    [Tooltip("How often the host sends heartbeats to the relay")]
    static public float heartbeatInterval = 11f;

    [Tooltip("Minimum gap between forced heartbeats")]
    static public float minHeartbeatGap = 0.25f;

}

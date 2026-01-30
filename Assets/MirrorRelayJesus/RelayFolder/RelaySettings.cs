using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RelaySettings : MonoBehaviour
{
    [Header("Relay Settings")]
    static public float handshakeGrace = 1f;
    public int frameRate = 30;

    [Header("Relay Capacity Limits")]
    static public int maxRelayClients = 200;
    static public int maxRegisteredHosts = 50;

    [Header("Handshake Limits")]
    static public int maxPendingHandshakes = 64;
    static public float handshakeTimeout = 3f;

    [Header("Rate Limiting")]
    static public int maxPacketsPerSecondPerIp = 60;
    static public float ipBlacklistDuration = 86400; // 24h

    static public float clientTimeout = 15f;

    static public float hostTimeout = 15f;
    static public float hostRegisterVerifyTimeout = 30f;
    static public int maxPlayersPerHostOverwrite = 200;


    static public float authGraceWindow = 5f;
    static public float rejectTime = 11f;
    static public float endpointBlockTime = 11f;
    static public int maxStrikesPerIp = 5;

    public const int MAX_HOST_PACKETS_PER_SEC = 5;
    public const float HOST_COOLDOWN_TIME = 3f;
    public const float MIN_HEARTBEAT_INTERVAL = 0.5f;

}

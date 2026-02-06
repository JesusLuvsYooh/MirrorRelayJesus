using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RelaySettings : MonoBehaviour
{
    [Header("Relay Settings")]
    static public float handshakeGrace = 1f;
    static public int frameRate = 30;

    [Header("Relay Capacity Limits")]
    static public int maxRelayClients = 200;
    static public int maxRegisteredHosts = 50;

    [Header("Handshake Limits")]
    static public int maxPendingHandshakes = 64;
    static public float handshakeTimeout = 3f;

    [Header("Rate Limiting")]
    // higher dicts require more ram, eventually could slow down code lookups too, trims whilst leaveing 10% of old dict values
    static public int trimDictionaryAtAmount = 1000;
    static public int maxPacketsPerSecondPerIp = 60;
    // 24h, ip block, not endpoint, this is quite strict and may block legit people in a bad network, however also feel free to set it to increase value if people abuse default cooldown 
    static public float ipBlacklistDuration = 86400; 
    static public float clientTimeout = 15f;

    static public float hostLastSeenTimeout = 15f; // remove host if no heartbeat, should be higher value than hosts heartbeatInterval
    static public float hostRegisterVerifyTimeout = 30f;
    static public int maxPlayersPerHostOverride = 100; // to prevent hosts allowing more connections in than we want, cant trust their sent variable to be honest

    static public int maxHostRejectStrikes = 3;
    static public int hostBlocklistDuration = 86400; // 86400 = 24h

    static public float authGraceWindow = 5f;
    static public float rejectTime = 11f;
    static public float endpointBlockTime = 11f;
    static public int maxStrikesPerIp = 5;

    
    static public int MAX_HOST_PACKETS_PER_SEC = 5;
    static public float hostCooldownAmount = 3f;
    static public float MIN_HEARTBEAT_INTERVAL = 0.5f;

    /// Trims a dictionary down by removing the oldest entries, if it exceeds the trim threshold.
    public static void TrimIfTooLarge<TKey, TValue>(
        Dictionary<TKey, TValue> dict)
    {
        if (dict == null) return;

        if (dict.Count >= RelaySettings.trimDictionaryAtAmount)
        {
           // int removeCount = dict.Count / 10;
            int removeCount = dict.Count - (dict.Count / 10);

            // Snapshot keys (cannot modify dictionary while iterating)
            var keys = new List<TKey>(dict.Keys);

            for (int i = 0; i < removeCount; i++)
            {
                dict.Remove(keys[i]);
            }
        }
    }

}

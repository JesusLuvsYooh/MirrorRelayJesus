using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RelaySettingsGame : MonoBehaviour
{
    // If false, bypasses relay and connects directly to the server
    static public bool useRelay = true;

    // Relay IP or hostname
    static public string relayAddress = "127.0.0.1";
    static public ushort gamePort = 7777;    // Mirror/KCP listen port, the in-game network manager/transport port //7777

    //static public string hostID = "HostA"; // Unique per host instance


    static public int maxPlayers = 8;

    // How often the host sends heartbeats to the relay, mirrors default timeout is 10s, so ive put 11s here.
    // note, players joining/leaving etc also call heartbeat
    static public float heartbeatInterval = 11f;

    // Minimum gap between heartbeats, prevents spam if 10 players join at same time
    // this is a host user side check, relay server will have its own spam check too
    static public float heartbeatGap = 0.25f;

}

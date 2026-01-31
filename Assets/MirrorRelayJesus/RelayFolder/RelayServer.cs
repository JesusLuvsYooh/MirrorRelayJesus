using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System;
using System.Collections;
using System.Security.Cryptography;

/// <summary>
/// UDP mirror relay that:
/// - Verify and accept hosters
/// - Register hosts for host list or matchmaking
/// - Accepts client connections
/// - Routes clients to registered hosts
/// - Applies rate limiting, cooldowns, and abuse prevention
/// - Provides stats for a live dashboard
/// </summary>
public class RelayServer : MonoBehaviour
{
    // we split up the major relay sections for readability
    public RelayServerHost relayServerHost;
    public RelayServerClient relayServerClient;

    private void Awake()
    {
        relayServerHost = new RelayServerHost();
        relayServerClient = new RelayServerClient();
    }

    void Start()
    {
        // make sure relay does not run uncapped framerate, vsync unlikely works on vps
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = RelaySettings.frameRate;

        // manually call setup as Start does not auto run on those scripts
        relayServerHost.Setup();
    }


}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;


public class RelayNetworkManager : NetworkManager
{
    private RelayGame relayGame;

    /// <summary>
    /// Runs on both Server and Client
    /// Networking is NOT initialized when this fires
    /// </summary>
    public override void Awake()
    {
        relayGame = this.GetComponent<RelayGame>();
        if (relayGame == null)
        {
            RelaySettingsShared.LogError("[NetworkManager] RelayGame script not found, add one to NetworkManager.");
        }

        base.Awake();
    }

    /// <summary>
    /// This is invoked when a server is started - including when a host is started.
    /// <para>StartServer has multiple signatures, but they all cause this hook to be called.</para>
    /// </summary>
    public override void OnStartServer()
    {
        RelaySettingsShared.Log("[NetworkManager] OnStartServer");
        relayGame.OnServerStarted();
    }

    /// <summary>
    /// This is called when a server is stopped - including when a host is stopped.
    /// </summary>
    public override void OnStopServer()
    {
        RelaySettingsShared.Log("[NetworkManager] OnStopServer");
        relayGame.OnServerStopped();
    }

    /// <summary>
    /// Called on the server when a client disconnects.
    /// <para>This is called on the Server when a Client disconnects from the Server. Use an override to decide what should happen when a disconnection is detected.</para>
    /// </summary>
    /// <param name="conn">Connection from client.</param>
    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        RelaySettingsShared.Log("[NetworkManager] OnServerDisconnect");
        base.OnServerDisconnect(conn);
    }

    /// <summary>
    /// Called on the server when a new client connects.
    /// <para>Unity calls this on the Server when a Client connects to the Server. Use an override to tell the NetworkManager what to do when a client connects to the server.</para>
    /// </summary>
    /// <param name="conn">Connection from client.</param>
    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        RelaySettingsShared.Log("[NetworkManager] OnServerConnect");
    }

    

}

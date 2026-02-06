using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System;
using System.Collections;
using System.Security.Cryptography;

public class RelayServerClient
{
    public RelayServer relayServer;
    public UdpClient clientListener;
    private string payload;
    private double nowTimestamp;
    private static readonly System.Random randomValue = new();
    public Dictionary<IPEndPoint, UdpClient> clientToHostMap = new();

    public void Setup()
    {
        clientListener = new UdpClient(RelaySettingsShared.relayClientPort);
        clientListener.BeginReceive(OnClientReceive, null); 
    }

    void OnClientReceive(IAsyncResult ar)
    {
        IPEndPoint clientIPEndPoint = new IPEndPoint(IPAddress.Any, 0);
        byte[] byteData = clientListener.EndReceive(ar, ref clientIPEndPoint);
        RelaySettingsShared.Log($"[Relay Client] OnClientReceive: {clientIPEndPoint}");
       // UdpClient udpClient = new UdpClient();

        if (!clientToHostMap.TryGetValue(clientIPEndPoint, out var udpClient))
        {
            udpClient = new UdpClient();
            clientToHostMap[clientIPEndPoint] = udpClient;
        }

        var keys = new List<IPEndPoint>(relayServer.relayServerHost.registeredHostInfo.Keys);
        IPEndPoint randomEndpoint = keys[randomValue.Next(keys.Count)];

        //IPEndPoint hostIPEndPoint = new IPEndPoint(123123123123,123);
        //IPEndPoint hostIPEndPoint = relayServer.relayServerHost.registeredHostInfo[randomValue.Next(relayServer.relayServerHost.registeredHostInfo.Count)];
        RelaySettingsShared.Log($"[Relay Client] Connect to random host: {randomEndpoint}");
        udpClient.Connect(new IPEndPoint(randomEndpoint.Address, 9000));
        udpClient.BeginReceive(a => relayServer.relayServerHost.OnHostReceive(a, clientIPEndPoint), null);
        ///udpClient.BeginReceive(a => relayServer.relayServerHost.OnHostReceive(a, udpClient, clientIPEndPoint), null);
        clientToHostMap[clientIPEndPoint] = udpClient;
        //Log($"[Relay Client] Client {clientKey} → host {hostId}");
        clientToHostMap[clientIPEndPoint].Send(byteData, byteData.Length);

        clientListener.BeginReceive(OnClientReceive, null);
    }
}

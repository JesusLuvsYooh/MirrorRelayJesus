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
    private Dictionary<IPEndPoint, double> clientLastSeen = new();

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

        if (!clientToHostMap.TryGetValue(clientIPEndPoint, out var udpClient))
        {
            udpClient = new UdpClient();
            var keys = new List<IPEndPoint>(relayServer.relayServerHost.registeredHostInfo.Keys);
            IPEndPoint randomEndpoint = keys[randomValue.Next(keys.Count)];
            RelaySettingsShared.Log($"[Relay Client] Connect to random host: {randomEndpoint}");
            udpClient.Connect(new IPEndPoint(randomEndpoint.Address, 7777));
           // udpClient.Connect("localhost", 9000);
            udpClient.BeginReceive(a => relayServer.relayServerHost.OnHostReceive(a, clientIPEndPoint), null);
            clientToHostMap[clientIPEndPoint] = udpClient;

        }

        nowTimestamp = RelaySettingsShared.nowTimestamp();
        clientLastSeen[clientIPEndPoint] = nowTimestamp;

        udpClient.Send(byteData, byteData.Length);
        clientListener.BeginReceive(OnClientReceive, null);
    }

    public void Cleanup()
    {
        //RelaySettingsShared.LogWarning($"[Relay Host] Cleanup!");

        nowTimestamp = RelaySettingsShared.nowTimestamp();

        // remove expired
        //foreach (var ep in new List<IPEndPoint>(hostCooldownUntil.Keys))
        //{
        //    if (hostCooldownUntil[ep] <= nowTimestamp)
        //        hostCooldownUntil.Remove(ep);
        //}

        //foreach (var ep in new List<IPAddress>(ipBlockedUntil.Keys))
        //{
        //    if (ipBlockedUntil[ep] <= nowTimestamp)
        //        ipBlockedUntil.Remove(ep);
        //}

        foreach (var ep in new List<IPEndPoint>(clientLastSeen.Keys))
        {
            if (nowTimestamp - clientLastSeen[ep] > RelaySettings.clientLastSeenTimeout)
            {
                clientToHostMap.Remove(ep);
                clientLastSeen.Remove(ep);
            }
        }

        // emergency cleaners to keep server ram and cpu usage down
        //RelaySettings.TrimIfTooLarge(clientLastSeen);
        //RelaySettings.TrimIfTooLarge(clientToHostMap);
    }
}

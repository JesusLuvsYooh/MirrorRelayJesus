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
    private Dictionary<IPEndPoint, double> clientCooldownUntil = new();
    public Dictionary<IPAddress, double> ipBlockedUntil = new();
    private Dictionary<IPEndPoint, int> clientRejectedCounter = new();
    private Dictionary<IPEndPoint, int> clientPacketCount = new();
    private Dictionary<IPEndPoint, double> clientWindowStart = new();
    Dictionary<IPEndPoint, bool> clientStruckThisWindow = new();
    Dictionary<IPEndPoint, double> clientLastPpsLogTime = new();

    public int totalPacketsStrikes;

    public void Setup()
    {
        clientListener = new UdpClient(RelaySettingsShared.relayClientPort);
        clientListener.BeginReceive(OnClientReceive, null); 
    }

    void OnClientReceive(IAsyncResult ar)
    {
        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 0);
        byte[] byteData = clientListener.EndReceive(ar, ref ipEndPoint);  // can this go later?
       // RelaySettingsShared.Log($"[Relay Client] OnClientReceive: {ipEndPoint}");

        if (!IsClientAllowed(ipEndPoint))
        {
            RelaySettingsShared.LogWarning("[Relay Client] Client rejected.");
            clientListener.BeginReceive(OnClientReceive, null);
            return;
        }



        if (!clientToHostMap.TryGetValue(ipEndPoint, out var udpClient))
        {
            udpClient = new UdpClient();
            var keys = new List<IPEndPoint>(relayServer.relayServerHost.registeredHostInfo.Keys);
            IPEndPoint randomEndpoint = keys[randomValue.Next(keys.Count)];
            RelaySettingsShared.Log($"[Relay Client] Connect to random host: {randomEndpoint}");
            udpClient.Connect(new IPEndPoint(randomEndpoint.Address, 7777));
           // udpClient.Connect("localhost", 9000);
            udpClient.BeginReceive(a => relayServer.relayServerHost.OnHostReceive(a, ipEndPoint), null);
            clientToHostMap[ipEndPoint] = udpClient;

        }

        nowTimestamp = RelaySettingsShared.nowTimestamp();
        clientLastSeen[ipEndPoint] = nowTimestamp;

        udpClient.Send(byteData, byteData.Length);
        clientListener.BeginReceive(OnClientReceive, null);
    }

    bool IsClientAllowed(IPEndPoint ipEndPoint)
    {
        nowTimestamp = RelaySettingsShared.nowTimestamp();

        if (clientRejectedCounter.Count > 0)
        {
            totalPacketsStrikes = clientRejectedCounter[ipEndPoint];
        }

        if (ipBlockedUntil.TryGetValue(ipEndPoint.Address, out var untilIp) && nowTimestamp < untilIp)
        {
            RelaySettingsShared.LogWarning($"[Relay Client] IP in blocklist: {ipEndPoint.Address}");
            return false;
        }

        // add client to an ip blocklist if malacious counter is trigger X times
        // we do address and not address+port/endpoint, for extra security, take no prisoners (from experience)
        //if (clientRejectedCounter.ContainsKey(ipEndPoint) && clientRejectedCounter[ipEndPoint] >= RelaySettings.maxClientStrikes)
        if (clientRejectedCounter.TryGetValue(ipEndPoint, out int strikes) &&
    strikes >= RelaySettings.maxClientStrikes)
        {
                RelaySettingsShared.LogWarning($"[Relay Client] Client {ipEndPoint} triggered security more than {RelaySettings.maxClientStrikes} times, add to blocklist!");
            ipBlockedUntil[ipEndPoint.Address] = nowTimestamp + RelaySettings.clientBlocklistDuration;
            clientRejectedCounter.Remove(ipEndPoint); // remove client counter if host now in blocklist
            return false;
        }

        // fixed 1-second rate-limit window reset.
        if (!clientWindowStart.TryGetValue(ipEndPoint, out var windowStart) ||
            nowTimestamp - windowStart >= 1.0)
        {
            clientWindowStart[ipEndPoint] = nowTimestamp;
            clientPacketCount[ipEndPoint] = 0;
            clientStruckThisWindow[ipEndPoint] = false;
            //if (clientPacketCount.ContainsKey(ipEndPoint))
            //{
                // RelaySettingsShared.Log($"[Relay Client] Client rate window limit reset. {clientIPEndPoint}");
            //}
            //return true;
        }

        // device or network lag may make client send/arrive closer together than expected
        // and not necessary malacious, a counter should trigger a block if repeated
        int count = ++clientPacketCount[ipEndPoint];
        if (count > RelaySettings.maxPacketsPerSecondPerClient)
        {
            if (!clientStruckThisWindow[ipEndPoint])
            {
                RelaySettingsShared.LogWarning($"[Relay Client] Client rate limit triggered!. {ipEndPoint}");
                clientRejectedCounter.TryAdd(ipEndPoint, 0);   // create entry if it doesn't exist
                clientRejectedCounter[ipEndPoint]++;          // increment
                                                              //totalPacketsStrikes++;
                clientStruckThisWindow[ipEndPoint] = true;
            }
            // continue and do not ignore data like we do with host register, could break gameplay.
        }

        // --- DEBUG: log packets per second (once per second) ---
        totalPpsThisSecond++;

        return true;
    }
    int totalPpsThisSecond;

    public void Cleanup()
    {
        RelaySettingsShared.Log($"[Relay] Total client PPS: {totalPpsThisSecond}");
        totalPpsThisSecond = 0;
        //RelaySettingsShared.LogWarning($"[Relay Client] Cleanup!");

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
        RelaySettings.TrimIfTooLarge(clientRejectedCounter);
        RelaySettings.TrimIfTooLarge(ipBlockedUntil);
        RelaySettings.TrimIfTooLarge(clientCooldownUntil);

        // we should not need these, client limit and timeouts should remove them naturally before dictionary gets too big
        //RelaySettings.TrimIfTooLarge(clientLastSeen); 
        //RelaySettings.TrimIfTooLarge(clientToHostMap);
    }
}

using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System;
using System.Collections;
using System.Security.Cryptography;

public class RelayServerHost
{
    public RelayServer relayServer;
    public UdpClient hostRegisterListener;
    private string payload;
    private double nowTimestamp;

    public Dictionary<IPEndPoint, RegisteredHostInfo> registeredHostInfo = new();
    private Dictionary<IPEndPoint, double> hostCooldownUntil = new();
    public Dictionary<IPAddress, double> ipBlockedUntil = new();
    private Dictionary<string, int> hostRejectedCounter = new();

    // basic host info, you may need to add your own, such as gamemodes, map, skill level, for matchmaking/host list.
    public struct RegisteredHostInfo
    {
        public IPEndPoint hostIPEndpoint;
        public string hostUID; // used for extra security, identify bad users, backup of endpoint as id, for relay use only, not client
        public int hostCurrentPlayers;
        public int hostMaxPlayers;
        public double hostLastSeen;
    }


    public void Setup()
    {
        hostRegisterListener = new UdpClient(RelaySettingsShared.hostRegisterPort);
        hostRegisterListener.BeginReceive(OnHostRegister, null);
    }

    public void OnHostReceive(IAsyncResult ar,  IPEndPoint clientIPEndPoint) //UdpClient hostSocket,
    {
        // we shouldnt need to do many host checks, as the register verification should do that
        // then here we just check if host data is from a registered host
        // we also use the hosts registration message as the "is still alive"/hostLastSeen tracker, so less calls are made here where it could get busy

        //RelaySettingsShared.Log($"[Relay Host] OnHostReceive called. {clientIPEndPoint}");
        if (!relayServer.relayServerClient.clientToHostMap.TryGetValue(clientIPEndPoint, out var host))
        {
            RelaySettingsShared.LogWarning("[Relay Host] No client to host mapping found.");
            return;
        }
        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 0);
        byte[] byteData;
        try
        {
            byteData = host.EndReceive(ar, ref ipEndPoint);
            //byteData = hostSocket.EndReceive(ar, ref ipEndPoint);
        }
        catch
        {
            RelaySettingsShared.LogWarning("[Relay Host] Error with data or receiver.");
            return;
        }

        relayServer.relayServerClient.clientListener.Send(byteData, byteData.Length, clientIPEndPoint);
        host.BeginReceive(a => OnHostReceive(a, clientIPEndPoint), null);
       // hostSocket.BeginReceive(a => OnHostReceive(a, hostSocket, clientIPEndPoint), null);
    }

    void OnHostRegister(IAsyncResult ar)
    {
        IPEndPoint hostIPEndPoint = new IPEndPoint(IPAddress.Any, 0);
        byte[] hostRegisterData = hostRegisterListener.EndReceive(ar, ref hostIPEndPoint);
        //RelaySettingsShared.Log($"[Relay Host] OnHostRegister called. {hostIPEndPoint}");

        if (!IsHostAllowed(hostIPEndPoint))
        {
            //RelaySettingsShared.LogWarning("[Relay Host] Host rejected.");
            hostRegisterListener.BeginReceive(OnHostRegister, null);
            return;
        }
        
        payload = Encoding.UTF8.GetString(hostRegisterData);
        try
        {
            payload = RelaySettingsShared.Decrypt(payload, RelaySettingsShared.hostRegisterSecret);
        }
        catch (Exception e)
        {
            RelaySettingsShared.LogWarning($"[Relay Host] Host data tampered with or wrong secret! {e.Message}");
            hostRegisterListener.BeginReceive(OnHostRegister, null);
            return;
        }

        nowTimestamp = RelaySettingsShared.nowTimestamp();

        hostCooldownUntil[hostIPEndPoint] = nowTimestamp + RelaySettings.hostCooldownAmount;


        // string split
        // split length and content checkers
        // apply max players overwrite, requires split
        // hostRegisterVerifyTimeout, requires split
        // see if heartbeat or register, requires split
        // ignore heartbeat if register info already removed from timeouts?

        if (registeredHostInfo.ContainsKey(hostIPEndPoint))
        {
            // update existing host
            if (registeredHostInfo.TryGetValue(hostIPEndPoint, out var existingHostInfo))
            {
                existingHostInfo.hostLastSeen = nowTimestamp;
                registeredHostInfo[hostIPEndPoint] = existingHostInfo;
            }
            //hostMaxPlayers[hostId] = Mathf.Clamp(maxPlayers, 0, maxPlayersPerHostOverride);
        }
        else
        {
            // Enforce relay host capacity (only for NEW hosts)
            if (registeredHostInfo.Count >= RelaySettings.maxRegisteredHosts)
            {
                RelaySettingsShared.LogWarning($"[Relay Host] Host registry full, rejecting host {hostIPEndPoint}");
                hostRegisterListener.BeginReceive(OnHostRegister, null);
                return;
            }
            // add new host
            RegisteredHostInfo newHostInfo = new RegisteredHostInfo
            {
                hostIPEndpoint = new IPEndPoint(hostIPEndPoint.Address, 9000),// hostIPEndPoint,
                hostUID = "uid",
                hostCurrentPlayers = 0,
                hostMaxPlayers = 0,
                hostLastSeen = nowTimestamp
            };
            registeredHostInfo.Add(hostIPEndPoint, newHostInfo);
            RelaySettingsShared.LogWarning($"[Relay Host] New registered host: {hostIPEndPoint}");
        }

        hostRegisterListener.BeginReceive(OnHostRegister, null);
    }

    bool IsHostAllowed(IPEndPoint ipEndPoint)
    {
        string endpointString = ipEndPoint.ToString();
        nowTimestamp = RelaySettingsShared.nowTimestamp();

        if (ipBlockedUntil.TryGetValue(ipEndPoint.Address, out var untilIp) && nowTimestamp < untilIp)
        {
            RelaySettingsShared.LogWarning($"[Relay Host] IP in blocklist: {ipEndPoint.Address}");
            return false;
        }

        // add host to an ip blocklist if malacious counter is trigger X times
        // we do address and not address+port/endpoint, for extra security, take no prisoners (from experience)
        if (hostRejectedCounter.ContainsKey(endpointString) && hostRejectedCounter[endpointString] >= RelaySettings.maxHostRejectStrikes)
        {
            RelaySettingsShared.LogWarning($"[Relay Host] Host {endpointString} rejected more than {RelaySettings.maxHostRejectStrikes} times, add to blocklist!");
            ipBlockedUntil[ipEndPoint.Address] = nowTimestamp + RelaySettings.hostBlocklistDuration;
            hostRejectedCounter.Remove(endpointString); // remove host counter if host now in blocklist
            return false;
        }

        // device or network lag may make host register send/arrive closer together than expected
        // and not necessary malacious, a counter should trigger a block if repeated
        if (hostCooldownUntil.TryGetValue(ipEndPoint, out var until) && nowTimestamp < until)
        {
            hostRejectedCounter.TryAdd(endpointString, 0);   // create entry if it doesn't exist
            hostRejectedCounter[endpointString]++;          // increment
            RelaySettingsShared.LogWarning($"[Relay Host] Host register data too frequent, ignore! {ipEndPoint}");
            return false;
        }

        return true;
    }

    public void Cleanup()
    {
        //RelaySettingsShared.LogWarning($"[Relay Host] Cleanup!");

        nowTimestamp = RelaySettingsShared.nowTimestamp();

        // remove expired
        foreach (var ep in new List<IPEndPoint>(hostCooldownUntil.Keys))
        {
            if (hostCooldownUntil[ep] <= nowTimestamp)
                hostCooldownUntil.Remove(ep);
        }

        foreach (var ep in new List<IPAddress>(ipBlockedUntil.Keys))
        {
            if (ipBlockedUntil[ep] <= nowTimestamp)
                ipBlockedUntil.Remove(ep);
        }

        foreach (var ep in new List<IPEndPoint>(registeredHostInfo.Keys))
        {
            if (registeredHostInfo.TryGetValue(ep, out RegisteredHostInfo info))
            {
                if (nowTimestamp - info.hostLastSeen > RelaySettings.hostLastSeenTimeout)
                {
                    registeredHostInfo.Remove(ep);
                }
            }
        }

        // emergency cleaners to keep server ram and cpu usage down
        RelaySettings.TrimIfTooLarge(hostRejectedCounter);
        RelaySettings.TrimIfTooLarge(ipBlockedUntil);
        RelaySettings.TrimIfTooLarge(hostCooldownUntil);
    }
}

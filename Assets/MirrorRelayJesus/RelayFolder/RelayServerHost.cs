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
    private UdpClient hostRegisterListener;
    private string payload;
    private double nowTimestamp;
    private Dictionary<IPEndPoint, double> hostCooldownUntil = new();
    private Dictionary<IPAddress, double> ipBlockedUntil = new();
    private Dictionary<string, int> hostRejectedCounter = new();
    

    public void Setup()
    {
        hostRegisterListener = new UdpClient(RelaySettingsShared.hostRegisterPort);
        hostRegisterListener.BeginReceive(OnHostRegister, null);
    }

    void OnHostRegister(IAsyncResult ar)
    {
        RelaySettingsShared.Log("[Relay] OnHostRegister called.");

        IPEndPoint hostIPEndPoint = new IPEndPoint(IPAddress.Any, 0);
        byte[] hostRegisterData = hostRegisterListener.EndReceive(ar, ref hostIPEndPoint);

        if (!IsHostAllowed(hostIPEndPoint))
        {
            //RelaySettingsShared.LogWarning("[Relay] Host rejected.");
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
            RelaySettingsShared.LogWarning($"[Relay] Host data tampered with or wrong secret! {e.Message}");
        }

        nowTimestamp = RelaySettingsShared.nowTimestamp();

        hostCooldownUntil[hostIPEndPoint] = nowTimestamp + RelaySettings.hostCooldownAmount;

        hostRegisterListener.BeginReceive(OnHostRegister, null);
    }

    bool IsHostAllowed(IPEndPoint _IPEndPoint)
    {
        string endpoint = _IPEndPoint.ToString();
        nowTimestamp = RelaySettingsShared.nowTimestamp();

        if (ipBlockedUntil.TryGetValue(_IPEndPoint.Address, out var untilIp) && nowTimestamp < untilIp)
        {
            RelaySettingsShared.LogWarning($"[Relay] IP in blocklist: {_IPEndPoint.Address}");
            return false;
        }

        // add host to an ip blocklist if malacious counter is trigger X times
        // we do address and not address+port/endpoint, for extra security, take no prisoners (from experience)
        if (hostRejectedCounter.ContainsKey(endpoint) && hostRejectedCounter[endpoint] >= RelaySettings.maxHostRejectStrikes)
        {
            RelaySettingsShared.LogWarning($"[Relay] Host {endpoint} rejected more than {RelaySettings.maxHostRejectStrikes} times, add to blocklist!");
            ipBlockedUntil[_IPEndPoint.Address] = nowTimestamp + RelaySettings.hostBlocklistDuration;
            hostRejectedCounter.Remove(endpoint); // remove host counter if host now in blocklist
            return false;
        }

        // device or network lag may make host register send/arrive closer together than expected
        // and not necessary malacious, a counter should trigger a block if repeated
        if (hostCooldownUntil.TryGetValue(_IPEndPoint, out var until) && nowTimestamp < until)
        {
            hostRejectedCounter.TryAdd(endpoint, 0);   // create entry if it doesn't exist
            hostRejectedCounter[endpoint]++;          // increment
            RelaySettingsShared.LogWarning($"[Relay] Host register data too frequent, ignore! {_IPEndPoint}");
            return false;
        }

        return true;
    }

    public void Cleanup()
    {
        RelaySettingsShared.LogWarning($"[Relay] Cleanup!");

        //hostRejectedCounter ipBlockedUntil hostCooldownUntil
    }
}

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class RelayServerHost
{
    private UdpClient hostRegisterListener;

    public void Setup()
    {
        hostRegisterListener = new UdpClient(RelaySettingsShared.hostRegisterPort);
        hostRegisterListener.BeginReceive(OnHostRegister, null);
    }

    void OnHostRegister(IAsyncResult ar)
    {
        RelaySettingsShared.Log("[Relay] OnHostRegister called.");

        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 0);
        byte[] hostRegisterData = hostRegisterListener.EndReceive(ar, ref ipEndPoint);
        string secure = Encoding.UTF8.GetString(hostRegisterData);
        RelaySettingsShared.Log($"[Relay] Host Register Data Secure: {Encoding.UTF8.GetString(hostRegisterData)}");

        try
        {
            string decrypted = RelaySettingsShared.Decrypt(secure, RelaySettingsShared.hostRegisterSecret);
            RelaySettingsShared.Log($"[Relay] Host Register Data: {decrypted}");
        }
        catch (Exception e)
        {
            RelaySettingsShared.LogWarning($"[Relay] Host data unsecure: {e.Message}");
        }

        hostRegisterListener.BeginReceive(OnHostRegister, null);
    }
}

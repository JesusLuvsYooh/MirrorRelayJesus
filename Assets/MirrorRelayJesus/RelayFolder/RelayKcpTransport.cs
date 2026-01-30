using Mirror;
using kcp2k;
using UnityEngine;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// Custom KCP transport that routes client connections through a UDP relay.
/// Optionally sends a lightweight authentication token to the relay
/// before initiating the KCP connection.
/// </summary>
public class RelayKcpTransport : KcpTransport
{
    public override void ClientConnect(string address)
    {
        // Relay disabled → behave exactly like normal KCP transport
        if (!RelayGameSettings.useRelay)
        {
            RelaySharedSettings.Log("[Client] Relay disabled, connecting directly");
            base.ClientConnect(address);
            return;
        }

        // Send authentication token

        if (RelaySharedSettings.useTokenAuth)
        {
            try
            {
                // Fire-and-forget UDP message to relay
                using (UdpClient udp = new UdpClient())
                {
                    udp.Connect(RelayGameSettings.relayAddress, RelaySharedSettings.relayPort);

                    // Simple auth payload: AUTH|<token>
                    byte[] tokenPayload =
                        Encoding.UTF8.GetBytes("AUTH|" + RelaySharedSettings.tokenSecret);

                    udp.Send(tokenPayload, tokenPayload.Length);

                    RelaySharedSettings.Log($"[Client] Sent AUTH token to relay {RelayGameSettings.relayAddress}:{RelaySharedSettings.relayPort}");
                }
            }
            catch (System.Exception ex)
            {
                // Token send failure does NOT stop connection attempt
                // Relay is responsible for rejecting invalid clients
                RelaySharedSettings.LogError($"[Client] Failed to send AUTH token: {ex}");
            }
        }

        Port = (ushort)RelaySharedSettings.relayPort;

        RelaySharedSettings.Log($"[Client] Connecting via relay {RelayGameSettings.relayAddress}:{RelaySharedSettings.relayPort}");

        // Connect to relay instead of direct server
        base.ClientConnect(RelayGameSettings.relayAddress);
    }
}

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
        if (!RelaySettingsGame.useRelay)
        {
            RelaySettingsShared.Log("[Client] Relay disabled, connecting directly");
            base.ClientConnect(address);
            return;
        }

        // Send authentication token

        if (RelaySettingsShared.useTokenAuth)
        {
            try
            {
                // Fire-and-forget UDP message to relay
                using (UdpClient udp = new UdpClient())
                {
                    udp.Connect(RelaySettingsGame.relayAddress, RelaySettingsShared.relayClientPort);

                    // Simple auth payload: AUTH|<token>
                    byte[] tokenPayload =
                        Encoding.UTF8.GetBytes("AUTH|" + RelaySettingsShared.tokenSecret);

                    udp.Send(tokenPayload, tokenPayload.Length);

                    RelaySettingsShared.Log($"[Client] Sent AUTH token to relay {RelaySettingsGame.relayAddress}:{RelaySettingsShared.relayClientPort}");
                }
            }
            catch (System.Exception ex)
            {
                // Token send failure does NOT stop connection attempt
                // Relay is responsible for rejecting invalid clients
                RelaySettingsShared.LogError($"[Client] Failed to send AUTH token: {ex}");
            }
        }

        Port = (ushort)RelaySettingsShared.relayClientPort;

        RelaySettingsShared.Log($"[Client] Connecting via relay {RelaySettingsGame.relayAddress}:{RelaySettingsShared.relayClientPort}");

        // Connect to relay instead of direct server
        base.ClientConnect(RelaySettingsGame.relayAddress);
    }
}

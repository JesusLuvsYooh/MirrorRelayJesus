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
    // ================================
    // Logging
    // ================================

    public enum LogMode
    {
        ErrorsOnly, // Minimal logging (recommended for release)
        All         // Verbose logging (development / debugging)
    }

    [Header("Logging")]
    [SerializeField] private LogMode logMode = LogMode.All;

    void Log(string msg)
    {
        if (logMode == LogMode.All)
            Debug.Log(msg);
    }

    void LogError(string msg)
    {
        Debug.LogError(msg);
    }

    // ================================
    // Relay Settings
    // ================================

    [Header("Relay")]
    [Tooltip("If false, bypasses relay and connects directly to the server")]
    [SerializeField] private bool useRelay = true;

    [Tooltip("Relay IP or hostname")]
    [SerializeField] private string relayAddress = "127.0.0.1";

    [Tooltip("Relay UDP/KCP port")]
    [SerializeField] private ushort relayPort = 7778;

    // ================================
    // Security
    // ================================

    [Header("Security")]
    [Tooltip("Send a pre-connection authentication token to the relay")]
    [SerializeField] private bool useTokenAuth = true;

    [Tooltip("Shared secret token expected by the relay")]
    [SerializeField] private string tokenSecret = "MY_SECRET_TOKEN";

    // ================================
    // Client Connection Override
    // ================================

    /// <summary>
    /// Overrides the default Mirror client connect flow.
    /// When relay is enabled:
    /// 1) Optionally sends an AUTH token via UDP
    /// 2) Redirects KCP connection to the relay instead of the game server
    /// </summary>
    public override void ClientConnect(string address)
    {
        // Relay disabled → behave exactly like normal KCP transport
        if (!useRelay)
        {
            Log("[Client] Relay disabled, connecting directly");
            base.ClientConnect(address);
            return;
        }

        // ================================
        // Step 1: Send authentication token
        // ================================

        if (useTokenAuth)
        {
            try
            {
                // Fire-and-forget UDP message to relay
                using (UdpClient udp = new UdpClient())
                {
                    udp.Connect(relayAddress, relayPort);

                    // Simple auth payload: AUTH|<token>
                    byte[] tokenPayload =
                        Encoding.UTF8.GetBytes("AUTH|" + tokenSecret);

                    udp.Send(tokenPayload, tokenPayload.Length);

                    Log($"[Client] Sent AUTH token to relay {relayAddress}:{relayPort}");
                }
            }
            catch (System.Exception ex)
            {
                // Token send failure does NOT stop connection attempt
                // Relay is responsible for rejecting invalid clients
                LogError($"[Client] Failed to send AUTH token: {ex}");
            }
        }

        // ================================
        // Step 2: Redirect KCP connection
        // ================================

        // Save original game server port
        ushort originalPort = Port;

        // Temporarily replace port with relay port
        Port = relayPort;

        Log($"[Client] Connecting via relay {relayAddress}:{relayPort}");

        // Connect to relay instead of direct server
        base.ClientConnect(relayAddress);

        // Restore original port for future reconnects
        Port = originalPort;
    }
}

using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System;
using System.Collections;
using System.Security.Cryptography;

/// <summary>
/// UDP relay that:
/// - Accepts client connections
/// - Routes clients to registered hosts
/// - Applies rate limiting, cooldowns, and abuse prevention
/// - Provides stats for a live dashboard
/// </summary>
public class MultiClientUdpRelay : MonoBehaviour
{
    // =====================================================
    // Logging
    // =====================================================

    public enum LogMode
    {
        ErrorsOnly, // Production-safe
        All         // Verbose debugging
    }

    [Header("Logging")]
    [SerializeField] private LogMode logMode = LogMode.All;

    void Log(string msg)
    {
        if (logMode == LogMode.All)
            Debug.Log(msg);
    }

    void LogWarning(string msg)
    {
        if (logMode == LogMode.All)
            Debug.LogWarning(msg);
    }

    void LogError(string msg)
    {
        Debug.LogError(msg);
    }

    // =====================================================
    // Public Host Info (for dashboard)
    // =====================================================

    public struct HostInfo
    {
        public string hostId;
        public IPEndPoint endpoint;
        public int maxPlayers;
        public int currentPlayers;
        public double secondsSinceSeen;
    }

    // =====================================================
    // Relay Settings
    // =====================================================


    [Header("Relay Settings")]
    private float handshakeGrace = 1f;
    private int frameRate = 30;

    [Header("Relay Capacity Limits")]
    private int maxRelayClients = 200;
    private int maxRegisteredHosts = 50;

    // =====================================================
    // Handshake Limits
    // =====================================================

    [Header("Handshake Limits")]
    private int maxPendingHandshakes = 64;
    private float handshakeTimeout = 3f;

    // =====================================================
    // Rate Limiting / Abuse Prevention
    // =====================================================

    [Header("Rate Limiting")]
    private int maxPacketsPerSecondPerIp = 60;
    private float ipBlacklistDuration = 86400; // 24h

    // =====================================================
    // Client Relay
    // =====================================================

    [Header("Client Relay")]
    private int listenPort = 7778;
    private float clientTimeout = 15f;

    // =====================================================
    // Host Registry
    // =====================================================

    [Header("Host Registry")]
    private int hostRegisterPort = 9001;
    private float hostTimeout = 15f;
    private float hostRegisterVerifyTimeout = 30f;
    private const string HOST_SECRET = "CHANGE_ME_TO_SOMETHING_SECRET";
    private int maxPlayersPerHostOverwrite = 200;

    // =====================================================
    // Security
    // =====================================================

    [Header("Security")]
    private bool useTokenAuth = true;
    private string validToken = "MY_SECRET_TOKEN";

    // =====================================================
    // Networking
    // =====================================================

    private UdpClient clientListener;
    private UdpClient hostRegistryListener;

    // =====================================================
    // Time Helpers
    // =====================================================

    private double Now() =>
        System.Diagnostics.Stopwatch.GetTimestamp() /
        (double)System.Diagnostics.Stopwatch.Frequency;

    private static readonly System.Random rng = new();

    // =====================================================
    // Client State
    // =====================================================

    // clientKey → host socket
    public List<string> ClientList() => new List<string>(clientToHostMap.Keys);
    private Dictionary<string, UdpClient> clientToHostMap = new();
    private Dictionary<string, string> clientToHostId = new();
    private Dictionary<string, bool> alive = new();

    // handshake tracking
    private HashSet<string> handshakePhase = new();
    private Dictionary<string, double> handshakeStartTime = new();

    // traffic stats
    private Dictionary<string, long> bytesFromClient = new();
    private Dictionary<string, long> bytesToClient = new();
    private Dictionary<string, double> lastActivity = new();

    // =====================================================
    // Host Registry State
    // =====================================================

    private Dictionary<string, IPEndPoint> registeredHosts = new();
    private Dictionary<string, double> hostLastSeen = new();
    private Dictionary<string, int> hostMaxPlayers = new();
    private Dictionary<string, int> hostClientCount = new();
    private List<string> hostKeys = new();

    // =====================================================
    // Security / Authorization
    // =====================================================

    private Dictionary<IPAddress, double> ipAuthorizedUntil = new();
    private float authGraceWindow = 5f;
    private float rejectTime = 11f;
    private float endpointBlockTime = 11f;

    // =====================================================
    // Rate Limiting (Endpoint-based)
    // =====================================================

    private Dictionary<IPEndPoint, int> endpointPacketCount = new();
    private Dictionary<IPEndPoint, double> endpointWindowStart = new();
    private Dictionary<IPEndPoint, double> endpointBlockedUntil = new();
    private Dictionary<IPEndPoint, double> rejectUntil = new();

    private Dictionary<IPAddress, double> ipBlockedUntil = new();
    private Dictionary<IPAddress, int> ipStrikeCount = new();
    private int maxStrikesPerIp = 5;

    // =====================================================
    // Host Abuse Protection
    // =====================================================

    private Dictionary<IPEndPoint, double> hostCooldownUntil = new();
    private Dictionary<IPEndPoint, int> hostPacketCount = new();
    private Dictionary<IPEndPoint, double> hostPacketWindowStart = new();
    private Dictionary<string, double> hostLastHeartbeatTime = new();

    private const int MAX_HOST_PACKETS_PER_SEC = 5;
    private const float HOST_COOLDOWN_TIME = 3f;
    private const float MIN_HEARTBEAT_INTERVAL = 0.5f;

    // host abuse stats
    private Dictionary<string, int> hostRejectedPackets = new();
    private Dictionary<string, int> hostRateLimitHits = new();
    private HashSet<string> hostInCooldown = new();
    private Dictionary<IPEndPoint, string> endpointToHostId = new();

    private int totalRejectedHostPackets;

    // =====================================================
    // Stats (Dashboard)
    // =====================================================

    private int totalRateLimitedPackets;
    private int handshakeTimeoutCount;
    private int rejectedClientCount;
    private int rejectedHostCount;

    // =====================================================
    // Public Stats API
    // =====================================================

    public int ConnectedClientCount => clientToHostMap.Count;
    public int RegisteredHostCount => registeredHosts.Count;
    public int MaxRelayClients => maxRelayClients;
    public int MaxRegisteredHosts => maxRegisteredHosts;

    public int PendingHandshakeCount => handshakePhase.Count;
    public int MaxPendingHandshakes => maxPendingHandshakes;

    public int BlacklistedEndpointCount => endpointBlockedUntil.Count;
    public int TotalRateLimitedPackets => totalRateLimitedPackets;

    public int HandshakeTimeoutCount => handshakeTimeoutCount;
    public int RejectedClientCount => rejectedClientCount;
    public int RejectedHostCount => rejectedHostCount;
    public int RejectCooldownCount => rejectUntil.Count;
    public int EndpointBlockCount => endpointBlockedUntil.Count;
    public int TotalRejectedHostPackets => totalRejectedHostPackets;

    void Start()
    {
        Application.targetFrameRate = frameRate;

        clientListener = new UdpClient(listenPort);
        clientListener.BeginReceive(OnClientReceive, null);

        Log($"[Relay] Client listener on port {listenPort}");

        StartCoroutine(StartHostRegistry());
    }

    IEnumerator StartHostRegistry()
    {
        yield return null;

        hostRegistryListener = new UdpClient(hostRegisterPort);
        hostRegistryListener.BeginReceive(OnHostRegister, null);

        Log($"[Relay] Host registry listening on port {hostRegisterPort}");
    }

    void Update()
    {
        //if (Input.GetKeyDown(KeyCode.Alpha1))
        //{
            // Debug.Log("[Relay] Test Button Pressed");
        //}

        CleanUp();
    }

    void OnHostRegister(IAsyncResult ar)
    {
        Log("[Relay] OnHostRegister called.");
        IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
        byte[] data = hostRegistryListener.EndReceive(ar, ref ep);

        if (!IsHostEndpointAllowed(ep))
        {
            hostRegistryListener.BeginReceive(OnHostRegister, null);
            LogWarning("[Relay] Host rejected.");
            return;
        }

        string msg = Encoding.UTF8.GetString(data);
        var parts = msg.Split('|');
        if (parts.Length != 6)
        {
            LogWarning("[Relay] Invalid host registry message");
            hostRegistryListener.BeginReceive(OnHostRegister, null);
            return;
        }

        string cmd = parts[0];
        string hostId = parts[1];
        int gamePort = int.Parse(parts[2]);
        int maxPlayers = int.Parse(parts[3]);
        long timestamp = long.Parse(parts[4]);
        string signature = parts[5];

        if (cmd == "REGISTER" || cmd == "HEARTBEAT")
        {
            double nowTime = Now();

            if (hostLastHeartbeatTime.TryGetValue(hostId, out var last))
            {
                if (nowTime - last < MIN_HEARTBEAT_INTERVAL)
                {
                    LogWarning($"[Relay] Host {hostId} heartbeat too fast");
                    hostCooldownUntil[ep] = nowTime + HOST_COOLDOWN_TIME;
                    TrackHostReject(hostId);
                    return;
                }
            }

            hostLastHeartbeatTime[hostId] = nowTime;
        }
        else
        {
            LogWarning($"[Relay] Host {hostId} data not valid.");
            TrackHostReject(hostId);
            return;
        }

        if (!VerifyHostSignature(hostId, gamePort, maxPlayers, timestamp, signature))
        {
            LogWarning($"[Relay] Host {hostId} failed signature verification");
            TrackHostReject(hostId);
            hostRegistryListener.BeginReceive(OnHostRegister, null);
            return;
        }

        // Reject stale replays (example: 30s window)
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - timestamp) > hostRegisterVerifyTimeout)
        {
            LogWarning($"[Relay] Host {hostId} timestamp expired");
            TrackHostReject(hostId);
            return;
        }

        // Enforce relay host capacity (only for NEW hosts)
        if (!registeredHosts.ContainsKey(hostId) &&
            registeredHosts.Count >= maxRegisteredHosts)
        {
            rejectedHostCount++;
            LogWarning($"[Relay] Host registry full, rejecting host {hostId}");
            TrackHostReject(hostId);
            //hostRegistryListener.BeginReceive(OnHostRegister, null);
            return;
        }

        if (registeredHosts.ContainsKey(hostId) == false &&
    parts[0] == "HEARTBEAT")
        {
            // ignore heartbeat for unknown host
            LogWarning($"[Relay] Ignore heartbeat for unknown host {hostId}");
            return;
        }

        registeredHosts[hostId] = new IPEndPoint(ep.Address, gamePort);
        hostLastSeen[hostId] = Now();
        hostMaxPlayers[hostId] = Mathf.Clamp(maxPlayers, 0, maxPlayersPerHostOverwrite);

        if (!hostClientCount.ContainsKey(hostId))
            hostClientCount[hostId] = 0;

        if (!hostKeys.Contains(hostId))
        {
            hostKeys.Add(hostId);
            Log($"[Relay] Host registered {hostId} cap={maxPlayers}");
            endpointToHostId[ep] = hostId;
        }

        hostRegistryListener.BeginReceive(OnHostRegister, null);
    }

    void TempRejectClient(IPEndPoint _clientEP)
    {
        LogWarning($"[Relay] Temporarily rejecting {_clientEP}");
        double now = Now();
        rejectUntil[_clientEP] = now + rejectTime;
        rejectedClientCount++;

        string key = _clientEP.ToString();
        // Kill any partial handshake
        if (handshakePhase.Contains(key))
            RemoveClient(key);
        clientListener.BeginReceive(OnClientReceive, null);
        return;
    }

    void BlacklistIP(IPEndPoint _clientEP)
    {
        double now = Now();

        var ip = _clientEP.Address;
        ipStrikeCount[ip] = ipStrikeCount.TryGetValue(ip, out var s) ? s + 1 : 1;

        ipBlockedUntil[ip] = now + ipBlacklistDuration;
        LogWarning($"[Relay] IP added to blacklist: {ip}");
    }

    void OnClientReceive(IAsyncResult ar)
    {
        IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);
        
        byte[] data = clientListener.EndReceive(ar, ref clientEP);
        Log($"[Relay] OnClientReceive: {clientEP.ToString()}");

        if (!IsEndpointAllowed(clientEP, isHostTraffic: false))
        {
            clientListener.BeginReceive(OnClientReceive, null);
            return;
        }

        string clientKey = clientEP.ToString();

        if (!clientToHostMap.ContainsKey(clientKey))
        {
            if (clientToHostMap.Count >= maxRelayClients)
            {
                LogWarning("[Relay] Max relay clients reached");
                TempRejectClient(clientEP);
            }
            else if (handshakePhase.Count >= maxPendingHandshakes)
            {
                LogWarning("[Relay] Max pending handshakes reached");
                TempRejectClient(clientEP);
            }

            if (useTokenAuth)
            {
                string msg = Encoding.UTF8.GetString(data);

                if (msg.StartsWith("AUTH|"))
                {
                    string token = msg.Substring(5);

                    if (token == validToken)
                    {
                        ipAuthorizedUntil[clientEP.Address] = Now() + authGraceWindow;
                        Log($"[Relay] Authorized IP {clientEP.Address}");
                    }
                    else
                    {
                        LogWarning($"[Relay] Invalid token from {clientEP}.");
                        TempRejectClient(clientEP);
                        BlacklistIP(clientEP);
                    }
                }

                if (!clientToHostMap.ContainsKey(clientKey))
                {
                    if (!ipAuthorizedUntil.TryGetValue(clientEP.Address, out var until) ||
                        Now() > until)
                    {
                        LogWarning($"[Relay] Unauthorized client {clientEP.Address}");
                        TempRejectClient(clientEP);
                    }
                }
            }


            var eligibleHosts = new List<string>();
            foreach (var h in hostKeys)
                if (hostClientCount[h] < hostMaxPlayers[h])
                    eligibleHosts.Add(h);

            if (eligibleHosts.Count == 0)
            {
                LogWarning("[Relay] No hosts with free slots");
                TempRejectClient(clientEP);
            }
            //Debug.Log($"[Relay] NEW registeredHosts[hostKeys[0] {registeredHosts[hostKeys[0]]}");
            string hostId = eligibleHosts[rng.Next(eligibleHosts.Count)];
            //Debug.Log($"[Relay] NEW hostId {hostId}");
            UdpClient hostClient = new UdpClient();
            hostClient.Connect(registeredHosts[hostId]);
            Log($"[Relay] NEW registeredHosts[hostId] {registeredHosts[hostId]}");
            hostClient.BeginReceive(a => OnHostReceive(a, clientKey), null);
            clientToHostMap[clientKey] = hostClient;
            clientToHostId[clientKey] = hostId;
            hostClientCount[hostId]++;
            handshakePhase.Add(clientKey);
            handshakeStartTime[clientKey] = Now();
            alive[clientKey] = true;
            bytesFromClient[clientKey] = 0;
            bytesToClient[clientKey] = 0;
            lastActivity[clientKey] = Now();

            Log($"[Relay] Client {clientKey} → host {hostId}");
        }

        if (!alive.ContainsKey(clientKey))
        {
            Log($"[Relay] {clientKey} !alive");
            TempRejectClient(clientEP);
        }

        clientToHostMap[clientKey].Send(data, data.Length);
        bytesFromClient[clientKey] += data.Length;

        // Only update lastActivity if handshake already complete
        if (!handshakePhase.Contains(clientKey))
        {
            //lastActivity[clientKey] = Time.time;
            lastActivity[clientKey] = Now();
           // Debug.Log($"[Relay] Updated lastActivity for active client {clientKey}");
        }
        else
        {
            Log($"[Relay] Client {clientKey} still in handshake phase, not updating lastActivity yet.");
        }

        clientListener.BeginReceive(OnClientReceive, null);
    }
   
    void OnHostReceive(IAsyncResult ar, string clientKey)
    {
        Log($"[Relay] OnHostReceive.");
        if (!alive.ContainsKey(clientKey)) return;
        if (!clientToHostMap.TryGetValue(clientKey, out var host)) return;

        IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
        byte[] data;
        try { data = host.EndReceive(ar, ref ep); }
        catch { return; }

        clientListener.Send(data, data.Length, ParseIPEndPoint(clientKey));
        bytesToClient[clientKey] += data.Length;

        handshakePhase.Remove(clientKey);
        handshakeStartTime.Remove(clientKey);
       // handshakeStartTime[clientKey] = Now();
        lastActivity[clientKey] = Now();

        host.BeginReceive(a => OnHostReceive(a, clientKey), null);
    }

    void CleanUp()
    {
        double now = Now();

        //foreach (var key in new List<string>(handshakePhase))
        //    if (now - handshakeStartTime[key] > handshakeTimeout)
        //        RemoveClient(key);

        foreach (var key in new List<string>(handshakePhase))
        {
            if (!handshakeStartTime.TryGetValue(key, out var start))
                continue;

            if (now - start > handshakeTimeout)
            {
                handshakeTimeoutCount++;
                LogWarning($"[Relay] Handshake timeout for {key}");
                RemoveClient(key);
            }
        }

        foreach (var c in new List<string>(lastActivity.Keys))
            if (now - lastActivity[c] > clientTimeout)
                RemoveClient(c);

        foreach (var h in new List<string>(hostLastSeen.Keys))
            if (now - hostLastSeen[h] > hostTimeout)
            {
                registeredHosts.Remove(h);
                hostLastSeen.Remove(h);
                hostMaxPlayers.Remove(h);
                hostClientCount.Remove(h);
                hostKeys.Remove(h);
                hostLastHeartbeatTime.Remove(h);

                hostInCooldown.Remove(h);
                hostRejectedPackets.Remove(h);
                hostRateLimitHits.Remove(h);
                hostLastHeartbeatTime.Remove(h);
                RemoveHostEndpointMappings(h);
                Log($"[Relay] Host {h} timed out");
            }

        foreach (var ep in new List<IPEndPoint>(rejectUntil.Keys))
        {
            if (rejectUntil[ep] <= now)
                rejectUntil.Remove(ep);
        }

        foreach (var ep in new List<IPEndPoint>(endpointBlockedUntil.Keys))
        {
            if (endpointBlockedUntil[ep] <= now)
                endpointBlockedUntil.Remove(ep);
        }

        foreach (var ep in new List<IPEndPoint>(endpointWindowStart.Keys))
        {
            if (now - endpointWindowStart[ep] > 2.0)
            {
                endpointWindowStart.Remove(ep);
                endpointPacketCount.Remove(ep);
            }
        }

        foreach (var ep in new List<IPEndPoint>(hostCooldownUntil.Keys))
        {
            if (Now() >= hostCooldownUntil[ep])
                hostCooldownUntil.Remove(ep);
        }
    }

    void RemoveClient(string clientKey)
    {
        alive.Remove(clientKey);

        if (clientToHostId.TryGetValue(clientKey, out var hostId))
            if (hostClientCount.ContainsKey(hostId))
                hostClientCount[hostId]--;

        if (clientToHostMap.TryGetValue(clientKey, out var host))
            host.Close();

        clientToHostMap.Remove(clientKey);
        clientToHostId.Remove(clientKey);
        lastActivity.Remove(clientKey);
        handshakePhase.Remove(clientKey);
        handshakeStartTime.Remove(clientKey);
        bytesFromClient.Remove(clientKey);
        bytesToClient.Remove(clientKey);

        Log($"[Relay] Client {clientKey} removed");
    }

    private IPEndPoint ParseIPEndPoint(string str)
    {
        var p = str.Split(':');
        return new IPEndPoint(IPAddress.Parse(p[0]), int.Parse(p[1]));
    }
    bool VerifyHostSignature(string hostId, int gamePort, int maxPlayers, long timestamp, string signature)
    {
        string payload = $"{hostId}|{gamePort}|{maxPlayers}|{timestamp}";
        string expected = ComputeHmac(payload, HOST_SECRET);
        return CryptographicEquals(expected, signature);
    }

    static string ComputeHmac(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        //return Convert.ToHexString(hash);
    }

    static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;

        int diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];

        return diff == 0;
    }

    public List<HostInfo> GetRegisteredHosts()
    {
        var list = new List<HostInfo>(hostKeys.Count);
        double now = Now();

        for (int i = 0; i < hostKeys.Count; i++)
        {
            string hostId = hostKeys[i];

            if (!registeredHosts.TryGetValue(hostId, out var ep))
                continue;

            int currentPlayers = 0;

            // Count clients routed to this host
            foreach (var kv in clientToHostMap)
            {
                if (!alive.ContainsKey(kv.Key))
                    continue;

                if (kv.Value != null &&
                    registeredHosts.TryGetValue(hostId, out var hostEp) &&
                    kv.Value.Client.RemoteEndPoint is IPEndPoint rep &&
                    rep.Address.Equals(hostEp.Address) &&
                    rep.Port == hostEp.Port)
                {
                    currentPlayers++;
                }
            }

            list.Add(new HostInfo
            {
                hostId = hostId,
                endpoint = ep,
                //maxPlayers = GetHostMaxPlayers(hostId),
                maxPlayers = hostMaxPlayers.TryGetValue(hostId, out var cap) ? cap : 0,
                currentPlayers = currentPlayers,
                secondsSinceSeen = now - hostLastSeen[hostId]
            });
        }

        return list;
    }

    public string GetClientHost(string clientKey)
    {
        if (!clientToHostMap.TryGetValue(clientKey, out var udp))
            return "<none>";

        if (udp?.Client?.RemoteEndPoint is not IPEndPoint rep)
            return "<unknown>";

        foreach (var kv in registeredHosts)
        {
            if (kv.Value.Address.Equals(rep.Address) &&
                kv.Value.Port == rep.Port)
                return kv.Key;
        }

        return "<unknown>";
    }

    public long BytesFromClient(string clientKey)
    {
        return bytesFromClient.TryGetValue(clientKey, out var value)
            ? value
            : 0;
    }

    public long BytesToClient(string clientKey)
    {
        return bytesToClient.TryGetValue(clientKey, out var value)
            ? value
            : 0;
    }

    public double LastActivity(string clientKey)
    {
        if (!lastActivity.TryGetValue(clientKey, out var last))
            return -handshakeGrace;

        return Now() - last;
    }

    public bool IsHandshake(string clientKey)
    {
        return handshakePhase.Contains(clientKey);
    }

    public double ClientTimeoutRemaining(string clientKey)
    {
        if (!lastActivity.TryGetValue(clientKey, out var t))
            return -1;

        return Math.Max(0, clientTimeout - (Now() - t));
    }


    public bool IsHostInCooldown(string hostId)
    {
        return hostInCooldown.Contains(hostId);
    }

    public int HostRejectedCount(string hostId)
    {
        return hostRejectedPackets.TryGetValue(hostId, out var v) ? v : 0;
    }

    public int HostRateLimitHits(string hostId)
    {
        return hostRateLimitHits.TryGetValue(hostId, out var v) ? v : 0;
    }

    bool IsEndpointAllowed(IPEndPoint ep, bool isHostTraffic)
    {
        Log($"[Relay] IsEndpointAllowed Check: {ep}");
        // Hosts are signed + trusted → never blacklisted
        if (isHostTraffic)
            return true;

        double now = Now();

        if (ipBlockedUntil.TryGetValue(ep.Address, out var untilIp) && now < untilIp)
        {
            Log($"[Relay] IP in blocklist: {ep}");
            return false;
        }

        if (rejectUntil.TryGetValue(ep, out var until1) && now < until1)
        {
            Log($"[Relay] EP cooldown: {ep}");
            return false;
        }

        if (endpointBlockedUntil.TryGetValue(ep, out var until))
        {
            if (now < until)
            {
                Log($"[Relay] Blocked until: {ep}");
                return false;
            }

            endpointBlockedUntil.Remove(ep);
        }

        // fixed 1-second rate-limit window reset.
        if (!endpointWindowStart.TryGetValue(ep, out var window) ||
            now - window >= 1.0)
        {
            endpointWindowStart[ep] = now;
            endpointPacketCount[ep] = 1;
            if (endpointPacketCount.ContainsKey(ep))
                Log($"[Relay] Rate limit window reset for {ep}");
            return true;
        }

        int count = ++endpointPacketCount[ep];
        if (count > maxPacketsPerSecondPerIp)
        {
            Log($"[Relay] Rate limit triggered: {ep}");
            endpointBlockedUntil[ep] = now + endpointBlockTime; // short endpoint cooldown
            totalRateLimitedPackets++;

            BlacklistIP(ep);

            return false;
        }

        return true;
    }

    bool IsHostEndpointAllowed(IPEndPoint ep)
    {
        double now = Now();

        if (hostCooldownUntil.TryGetValue(ep, out var until) && now < until)
            return false;

        if (!hostPacketWindowStart.TryGetValue(ep, out var window) || now - window >= 1.0)
        {
            hostPacketWindowStart[ep] = now;
            hostPacketCount[ep] = 1;
            return true;
        }

        

        if (++hostPacketCount[ep] > MAX_HOST_PACKETS_PER_SEC)
        {
            hostCooldownUntil[ep] = now + HOST_COOLDOWN_TIME;
            totalRejectedHostPackets++;

            if (endpointToHostId.TryGetValue(ep, out var hostId))
            {
                hostInCooldown.Add(hostId);
                hostRateLimitHits[hostId]++;
            }

            LogWarning($"[Relay] Host endpoint rate-limited: {ep}");
            return false;
        }

        return true;
    }


    void TrackHostReject(string hostId)
    {
        if (!hostRejectedPackets.ContainsKey(hostId))
            hostRejectedPackets[hostId] = 0;

        hostRejectedPackets[hostId]++;
        totalRejectedHostPackets++;
    }

    void RemoveHostEndpointMappings(string hostId)
    {
        var toRemove = new List<IPEndPoint>();

        foreach (var kv in endpointToHostId)
            if (kv.Value == hostId)
                toRemove.Add(kv.Key);

        for (int i = 0; i < toRemove.Count; i++)
            endpointToHostId.Remove(toRemove[i]);
    }


}
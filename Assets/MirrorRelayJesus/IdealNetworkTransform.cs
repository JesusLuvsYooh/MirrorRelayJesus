using Mirror;
using UnityEngine;
using System.Collections.Generic;

public class IdealNetworkTransform : NetworkBehaviour
{
    [Header("Send Settings")]
    [SerializeField] float sendRate = 15f;
    [SerializeField] float positionThreshold = 0.02f;
    [SerializeField] float rotationThreshold = 1f;
    [SerializeField] bool syncRotation = true;

    [Header("Interpolation")]
    [SerializeField] float interpolationBackTime = 0.1f;
    [SerializeField] float singleSnapshotMoveSpeed = 5f;
    [SerializeField] float teleportDistance = 5f;
    [SerializeField] int snapshotBufferSize = 3;
    //2 ultra low latency but jitter risk
    //3 best compromise
    //4 very smooth but slightly delayed

    const float POSITION_SCALE = 100f;

    float nextSendTime;

    Vector3 lastSentPosition;
    float lastSentYaw;

    // Flag to track first interpolation after single-snapshot fallback
    bool justGotMultipleSnapshots = false;

    struct Snapshot
    {
        public double time;
        public Vector3 position;
        public float yaw;
    }

    //readonly Queue<Snapshot> buffer = new Queue<Snapshot>(snapshotBufferSize);
    Queue<Snapshot> buffer;

    #region Sending

    void Awake()
    {
        buffer = new Queue<Snapshot>(snapshotBufferSize);
    }

    void Update()
    {
        if (!isOwned) return;
        if (Time.time < nextSendTime) return;

        TrySend();
        nextSendTime = Time.time + 1f / sendRate;
    }

    void TrySend()
    {
        Vector3 pos = transform.position;
        float yaw = transform.eulerAngles.y;

        bool posChanged = (pos - lastSentPosition).sqrMagnitude > positionThreshold * positionThreshold;
        bool rotChanged = syncRotation && Mathf.Abs(Mathf.DeltaAngle(yaw, lastSentYaw)) > rotationThreshold;

        if (!posChanged && !rotChanged)
            return;

        if (posChanged && rotChanged)
        {
            CmdSendPositionRotation(
                Quantize(pos.x),
                Quantize(pos.y),
                Quantize(pos.z),
                QuantizeAngle(yaw)
            );
        }
        else if (posChanged)
        {
            CmdSendPosition(
                Quantize(pos.x),
                Quantize(pos.y),
                Quantize(pos.z)
            );
        }
        else if (rotChanged)
        {
            CmdSendRotation(
                QuantizeAngle(yaw)
            );
        }

        if (posChanged) lastSentPosition = pos;
        if (rotChanged) lastSentYaw = yaw;
    }
    //    [Command(channel = Channels.Unreliable)]
    //void CmdSendTransform(byte flags, short x, short y, short z, short yaw)
    //{
    //    RpcReceiveTransform(flags, x, y, z, yaw);
    //}

    #endregion

    #region Receiving

    //[ClientRpc(channel = Channels.Unreliable)]
    //void RpcReceiveTransform(byte flags, short x, short y, short z, short yaw)
    //{
    //    if (isOwned) return;

    //    Snapshot s = new Snapshot
    //    {
    //        time = NetworkTime.time,
    //        position = (flags & 1) != 0
    //            ? new Vector3(Dequantize(x), Dequantize(y), Dequantize(z))
    //            : transform.position,
    //        yaw = (flags & 2) != 0
    //            ? DequantizeAngle(yaw)
    //            : transform.eulerAngles.y
    //    };

    //    buffer.Enqueue(s);
    //    while (buffer.Count > 4)
    //        buffer.Dequeue();
    //}

    [Command(channel = Channels.Unreliable)]
    void CmdSendPositionRotation(short x, short y, short z, short yaw)
    {
        RpcReceivePositionRotation(x, y, z, yaw);
    }

    [Command(channel = Channels.Unreliable)]
    void CmdSendPosition(short x, short y, short z)
    {
        RpcReceivePosition(x, y, z);
    }

    [Command(channel = Channels.Unreliable)]
    void CmdSendRotation(short yaw)
    {
        RpcReceiveRotation(yaw);
    }

    [ClientRpc(channel = Channels.Unreliable)]
    void RpcReceivePositionRotation(short x, short y, short z, short yaw)
    {
        if (isOwned) return;

        AddSnapshot(
            new Vector3(Dequantize(x), Dequantize(y), Dequantize(z)),
            DequantizeAngle(yaw)
        );
    }

    [ClientRpc(channel = Channels.Unreliable)]
    void RpcReceivePosition(short x, short y, short z)
    {
        if (isOwned) return;

        AddSnapshot(
            new Vector3(Dequantize(x), Dequantize(y), Dequantize(z)),
            transform.eulerAngles.y
        );
    }

    [ClientRpc(channel = Channels.Unreliable)]
    void RpcReceiveRotation(short yaw)
    {
        if (isOwned) return;

        AddSnapshot(
            transform.position,
            DequantizeAngle(yaw)
        );
    }

    void LateUpdate()
    {
        if (isOwned) return;
        int count = buffer.Count;
        if (count == 0) return;

        Snapshot[] snaps = buffer.ToArray();
        Snapshot latest = snaps[count - 1];

        float dist = Vector3.Distance(transform.position, latest.position);

        // 3️⃣ Teleport if too far
        if (dist > teleportDistance)
        {
            transform.position = latest.position;
            if (syncRotation)
                transform.rotation = Quaternion.Euler(0, latest.yaw, 0);
            return;
        }

        // 2️⃣ Single snapshot fallback → move toward latest at fixed speed
        if (count == 1)
        {
            float step = singleSnapshotMoveSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, latest.position, step);

            if (syncRotation)
            {
                float yawStep = singleSnapshotMoveSpeed * Time.deltaTime;
                float newYaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, latest.yaw, yawStep);
                transform.rotation = Quaternion.Euler(0, newYaw, 0);
            }

            return;
        }

        Snapshot from = new Snapshot();
        Snapshot to = new Snapshot();

        double renderTime = NetworkTime.time - interpolationBackTime;

        if (renderTime <= snaps[0].time)
        {
            from.position = transform.position;
            from.yaw = transform.eulerAngles.y;
            from.time = NetworkTime.time; // initialize time
            to = snaps[0];
        }
        else
        {
            from = snaps[0];
            to = snaps[snaps.Length - 1];

            for (int i = 0; i < snaps.Length - 1; i++)
            {
                if (snaps[i + 1].time >= renderTime)
                {
                    from = snaps[i];
                    to = snaps[i + 1];
                    break;
                }
            }
        }

        float length = (float)(to.time - from.time);
        if (length <= 0.0001f) length = 0.0001f;

        float t = (float)((renderTime - from.time) / length);
        t = Mathf.Clamp01(t);

        transform.position = Vector3.Lerp(from.position, to.position, t);
        if (syncRotation)
        {
            float yaw = Mathf.LerpAngle(from.yaw, to.yaw, t);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        // Track if buffer is now ≥2 snapshots to trigger the above logic
        if (count >= 2)
            justGotMultipleSnapshots = true;
    }

    #endregion

    #region Quantization

    static short Quantize(float v) => (short)(v * POSITION_SCALE);
    static float Dequantize(short v) => v / POSITION_SCALE;
    static short QuantizeAngle(float degrees) => (short)(degrees * 100);
    static float DequantizeAngle(short v) => v / 100f;

    #endregion

    void AddSnapshot(Vector3 pos, float yaw)
    {
        Snapshot s = new Snapshot
        {
            time = NetworkTime.time,
            position = pos,
            yaw = yaw
        };

        buffer.Enqueue(s);

        while (buffer.Count > snapshotBufferSize)
            buffer.Dequeue();
    }
}
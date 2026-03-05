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

    const float POSITION_SCALE = 100f; // centimeters

    float nextSendTime;

    Vector3 lastSentPosition;
    float lastSentYaw;

    struct Snapshot
    {
        public double time;
        public Vector3 position;
        public float yaw;
    }

    readonly Queue<Snapshot> buffer = new Queue<Snapshot>(4);

    #region Sending

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

        bool posChanged = Vector3.Distance(pos, lastSentPosition) > positionThreshold;
        bool rotChanged = syncRotation && Mathf.Abs(Mathf.DeltaAngle(yaw, lastSentYaw)) > rotationThreshold;

        if (!posChanged && !rotChanged)
            return;

        byte flags = 0;
        if (posChanged) flags |= 1;
        if (rotChanged) flags |= 2;

        CmdSendTransform(
            flags,
            Quantize(pos.x),
            Quantize(pos.y),
            Quantize(pos.z),
            QuantizeAngle(yaw)
        );

        lastSentPosition = pos;
        lastSentYaw = yaw;
    }

    [Command(channel = Channels.Unreliable)]
    void CmdSendTransform(byte flags, short x, short y, short z, short yaw)
    {
        RpcReceiveTransform(flags, x, y, z, yaw);
    }

    #endregion

    #region Receiving

    [ClientRpc(channel = Channels.Unreliable)]
    void RpcReceiveTransform(byte flags, short x, short y, short z, short yaw)
    {
        if (isOwned) return;

        Snapshot s = new Snapshot
        {
            time = NetworkTime.time,
            position = (flags & 1) != 0
                ? new Vector3(Dequantize(x), Dequantize(y), Dequantize(z))
                : transform.position,
            yaw = (flags & 2) != 0
                ? DequantizeAngle(yaw)
                : transform.eulerAngles.y
        };

        buffer.Enqueue(s);
        while (buffer.Count > 3)
            buffer.Dequeue();
    }

    void LateUpdate()
    {
        if (isOwned) return;
        if (buffer.Count < 2) return;

        double renderTime = NetworkTime.time - interpolationBackTime;

        Snapshot a = buffer.Peek();
        Snapshot b = default;

        foreach (var snap in buffer)
        {
            if (snap.time > renderTime)
            {
                b = snap;
                break;
            }
            a = snap;
        }

        float t = (float)((renderTime - a.time) / (b.time - a.time));
        t = Mathf.Clamp01(t);

        Vector3 pos = Vector3.Lerp(a.position, b.position, t);
        transform.position = pos;

        if (syncRotation)
        {
            float yaw = Mathf.LerpAngle(a.yaw, b.yaw, t);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
        }
    }

    #endregion

    #region Quantization

    static short Quantize(float v)
        => (short)(v * POSITION_SCALE);

    static float Dequantize(short v)
        => v / POSITION_SCALE;

    static short QuantizeAngle(float degrees)
        => (short)(degrees * 100);

    static float DequantizeAngle(short v)
        => v / 100f;

    #endregion
}
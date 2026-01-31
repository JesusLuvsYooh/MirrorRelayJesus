using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public class RelaySettingsShared : MonoBehaviour
{
    static public ushort relayPort = 9000; //Relay UDP/KCP port, listens for clients
    static public ushort hostRegisterPort = 9001;   // port for host registry data //relayRegisteryPort

    static public bool useTokenAuth = true; //Send a pre-connection authentication token to the relay, flags IP as allowed for X time
    static public string tokenSecret = "MY_SECRET_TOKEN"; // for relay auth

    //for hosts encypted registry data, could be sent via website or user account specific
    // for now we're just having this unique per game project, so people without your game do not know it
    static public string hostRegisterSecret = "CHANGE_ME";

    public enum LogMode
    {
        ErrorsOnly, // Production-safe
        All         // Iincludes warnings, useful for Editor
    }

    [Header("Logging")]
    static public LogMode logMode = LogMode.All;

    static public void Log(string msg)
    {
        if (logMode == LogMode.All)
            Debug.Log(msg);
    }

    static public void LogWarning(string msg)
    {
        if (logMode == LogMode.All)
            Debug.LogWarning(msg);
    }

    static public void LogError(string msg)
    {
        Debug.LogError(msg);
    }

    // Turn any secret string into a fixed 32-byte AES key
    private static byte[] CreateAesKey(string secret)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(secret));
    }

    // Separate key for HMAC signing
    private static byte[] CreateHmacKey(string secret)
    {
        return Encoding.UTF8.GetBytes("HMAC_" + secret);
    }

    // ----------------------------------------------------------
    // YOUR SIMPLE ONE-LINE STYLE METHOD
    // ----------------------------------------------------------
    public static string Encrypt(string data, string secret)
    {
        Log($"[Relay Settings] Encrypt: {data}");
        byte[] aesKey = CreateAesKey(secret);
        byte[] hmacKey = CreateHmacKey(secret);

        // --- 1) AES ENCRYPT ---
        using var aes = Aes.Create();
        aes.Key = aesKey;
        aes.GenerateIV(); // random per message

        byte[] plainBytes = Encoding.UTF8.GetBytes(data);

        using var encryptor = aes.CreateEncryptor();
        byte[] encrypted = encryptor.TransformFinalBlock(
            plainBytes, 0, plainBytes.Length);

        // Combine IV + ciphertext
        byte[] combined = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, combined, aes.IV.Length, encrypted.Length);

        string encryptedBase64 = Convert.ToBase64String(combined);

        // --- 2) HMAC SIGN ---
        using var hmac = new HMACSHA256(hmacKey);
        byte[] signature = hmac.ComputeHash(
            Encoding.UTF8.GetBytes(encryptedBase64));

        string signatureBase64 = Convert.ToBase64String(signature);
        Log($"[Relay Settings] Encrypted: {encryptedBase64 + "|" + signatureBase64}");
        // Final secure packet
        return encryptedBase64 + "|" + signatureBase64;
    }

    // ----------------------------------------------------------
    // Helper to reverse it (you'll need this on the server)
    // ----------------------------------------------------------
    public static string Decrypt(string packet, string secret)
    {
        Log($"[Relay Settings] Decrypt: {packet}");
        byte[] aesKey = CreateAesKey(secret);
        byte[] hmacKey = CreateHmacKey(secret);

        string[] parts = packet.Split('|');
        if (parts.Length != 2)
            throw new Exception("Invalid packet format");

        string encryptedBase64 = parts[0];
        string receivedSignature = parts[1];

        // Verify signature first
        using var hmac = new HMACSHA256(hmacKey);
        byte[] expectedSig = hmac.ComputeHash(
            Encoding.UTF8.GetBytes(encryptedBase64));

        string expectedSigBase64 = Convert.ToBase64String(expectedSig);

        if (expectedSigBase64 != receivedSignature)
            throw new Exception("Packet tampered with or wrong secret!");

        // Decrypt
        byte[] combined = Convert.FromBase64String(encryptedBase64);

        using var aes = Aes.Create();
        aes.Key = aesKey;

        byte[] iv = new byte[16];
        byte[] cipher = new byte[combined.Length - 16];

        Buffer.BlockCopy(combined, 0, iv, 0, 16);
        Buffer.BlockCopy(combined, 16, cipher, 0, cipher.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        byte[] decrypted = decryptor.TransformFinalBlock(
            cipher, 0, cipher.Length);

        Log($"[Relay Settings] Decrypted: {Encoding.UTF8.GetString(decrypted)}");
        return Encoding.UTF8.GetString(decrypted);
    }

}

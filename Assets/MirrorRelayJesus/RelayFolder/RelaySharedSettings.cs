using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RelaySharedSettings : MonoBehaviour
{
    static public ushort relayPort = 7778; //Relay UDP/KCP port, listens for clients
    static public int relayRegisteryPort = 9001;   // port for host registry data

    static public bool useTokenAuth = true; //Send a pre-connection authentication token to the relay, flags IP as allowed for X time
    static public string tokenSecret = "MY_SECRET_TOKEN"; // for relay auth
    static public string HOST_SECRET = "CHANGE_ME_TO_SOMETHING_SECRET"; //for hosts encypted registry data

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
}

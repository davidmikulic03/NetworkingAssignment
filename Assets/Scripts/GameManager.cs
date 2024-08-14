using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
public class GameManager : MonoBehaviour {
    private static NetworkManager m_NetworkManager;

    private List<ulong> Scoreboard = new List<ulong>();

    void Awake() {
        m_NetworkManager = GetComponent<NetworkManager>();
    }

    private void Update() {
        
    }

    [ServerRpc] public void ScorePointServerRpc(ulong id) {
        if ((int)id >= Scoreboard.Count)
            Scoreboard.Add(1);
        else
            Scoreboard[(int)id]++;

        Debug.Log("Player " + id + " scored. They now have " + Scoreboard[(int)id] + " points.");
    }

    private void OnGUI() {
        GUILayout.BeginArea(new Rect(20, 20, 900, 900));
        if(!m_NetworkManager.IsClient && !m_NetworkManager.IsServer) {
            StartButtons();
        } else {
            StatusLabels();
        }
        GUILayout.EndArea();
    }

    static void StartButtons() {
        if (GUILayout.Button("Host")) m_NetworkManager.StartHost();
        if (GUILayout.Button("Client")) m_NetworkManager.StartClient();
    }

    static void StatusLabels() {
        var mode = m_NetworkManager.IsHost ? "Host" : "Client";

        GUILayout.Label("Transport: " +
            m_NetworkManager.NetworkConfig.NetworkTransport.GetType().Name);
        GUILayout.Label("Mode: " + mode);
    }
}

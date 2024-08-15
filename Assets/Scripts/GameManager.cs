using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
public class GameManager : MonoBehaviour {
    [SerializeField] private Chat Chat;
    public WinScreen WinScreen;

    private static NetworkManager m_NetworkManager;

    void Awake() {
        m_NetworkManager = GetComponent<NetworkManager>();
        Chat.Hide();
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

    void StartButtons() {
        if (GUILayout.Button("Host")) {
            m_NetworkManager.StartHost();
            Chat.Show();
        } if (GUILayout.Button("Client")) {
            m_NetworkManager.StartClient();
            Chat.Show();
        }
    }

    static void StatusLabels() {
        var mode = m_NetworkManager.IsHost ? "Host" : "Client";

        GUILayout.Label("Transport: " +
            m_NetworkManager.NetworkConfig.NetworkTransport.GetType().Name);
        GUILayout.Label("Mode: " + mode);
    }
}

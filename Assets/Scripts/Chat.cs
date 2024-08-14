using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Chat : NetworkBehaviour
{
    [SerializeField] private GameObject ChatMessagePrefab;
    [SerializeField] private TextMeshProUGUI InputField;
    [SerializeField] private Transform ContentParent;
    [SerializeField] private Scrollbar Scrollbar;
    [SerializeField] private RectTransform Rect;

    private PlayerInput InputActions;

    private List<GameObject> ChatMessages = new List<GameObject>();

    private Vector3 RestPosition;

    private void Awake() {
        InputActions = new PlayerInput();
        InputActions.Enable();
        RestPosition = new Vector3(-352.935f, 363.319f, 0f);
        //Rect.localPosition;
    }

    public override void OnNetworkSpawn() {
        
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Return) 
            && InputField.text.Length > 0) {
            SendChatMessage();
        }
    }

    public void AddMessage(string Message, ulong PlayerId) {
        var messageObject = Instantiate(ChatMessagePrefab);
        string message = PlayerId == NetworkManager.LocalClientId ? "You: " + Message : "Player " + PlayerId + ": " + Message;
        messageObject.GetComponent<TextMeshProUGUI>().text = message;
        messageObject.transform.SetParent(ContentParent, false);
        ChatMessages.Add(messageObject);
        if (ChatMessages.Count > 32) {
            Destroy(ChatMessages[0]);
            ChatMessages.RemoveAt(0);
        }
    }
    public void SendChatMessage() {
        Debug.Log("Sent message from client.");
        string message = InputField.text;
        SendChatMessageServerRpc(message, NetworkManager.LocalClientId);
        
        InputField.text.Remove(0);
        Scrollbar.value = 0f;
    }
    [ClientRpc] private void ReceiveChatMessageClientRpc(FixedString512Bytes Message, ulong PlayerId) {
        Debug.Log("Client received message from server.");
        AddMessage(Message.ToString(), PlayerId);
    }

    [ServerRpc(RequireOwnership = false)] private void SendChatMessageServerRpc(FixedString512Bytes Message, ulong PlayerId) {
        Debug.Log("Server received message from client.");
        ReceiveChatMessageClientRpc(Message, PlayerId);
    }

    public void Hide() {
        Rect.localPosition = new Vector3(1000000, 100000, 1000000);
    }
    public void Show() {
        //Complete bruteforce but I didn't feel like dealing with it.
        Rect.localPosition = new Vector3(-352.935f + 960.1167f, 363.319f - 539.9343f, 0f);
    }
}

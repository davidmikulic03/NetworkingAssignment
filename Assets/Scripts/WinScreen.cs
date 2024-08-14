using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WinScreen : MonoBehaviour
{

    private TextMeshProUGUI TextMesh;

    void Start() {
        TextMesh = GetComponent<TextMeshProUGUI>();
        TextMesh.text = "";
    }

    public void Win() {
        TextMesh.text = "You win!";
    }
    public void Lose() {
        TextMesh.text = "You lose...";
    }
}

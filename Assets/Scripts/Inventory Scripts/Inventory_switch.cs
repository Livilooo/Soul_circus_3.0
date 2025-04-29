using UnityEngine;
using System.Collections;

public class MenuAppearScript : MonoBehaviour {
    private PlayerController playerController;
    public GameObject menu; // Assign in inspector
    private bool isShowing;

    void Start()
    {
        menu.SetActive(false);
        playerController = GameObject.FindObjectOfType<PlayerController>();
    }
    private void UpdatePlayerMovement(bool allowMovement)
    {
        if (playerController != null)
        {
            playerController.canMove = allowMovement;
        }
    }
    
    void Update() {
        if (Input.GetKeyDown("tab")) {
            isShowing = !isShowing;
            menu.SetActive(isShowing);
            playerController.canMove = false;
            UpdatePlayerMovement(false); // Disable movement at the start of the node [T7](2)
        }
    }
}

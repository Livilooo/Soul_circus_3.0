using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldStreamingScript : MonoBehaviour
{
    // Add a tag for the player
    public string playerTag = "Player";

    private void Start()
    {
        // Optionally, you may want to start with all children inactive
        SetChildrenActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            Debug.Log("Player entered the trigger area. Activating world section.");
            SetChildrenActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            Debug.Log("Player exited the trigger area. Deactivating world section.");
            SetChildrenActive(false);
        }
    }

    private void SetChildrenActive(bool state)
    {
        foreach (Transform child in transform)
        {
            if (child != null)
            {
                child.gameObject.SetActive(state);
                Debug.Log("Set " + child.gameObject.name + " to " + (state ? "active" : "inactive"));
            }
        }
    }
}

using UnityEngine;

public class StoryTriggerZone : MonoBehaviour
{
    public GameEvent storyProgressEvent;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            storyProgressEvent.Raise();
        }
    }
}


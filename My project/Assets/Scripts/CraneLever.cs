using UnityEngine;

public class CraneLever : MonoBehaviour
{
    public CraneController crane;
    public GameObject uiPrompt;

    private bool canInteract = false;
    private PlayerController currentPlayer;

    void Start()
    {
        if (uiPrompt != null) uiPrompt.SetActive(false);
    }

    void Update()
    {
        // Use GameInput
        if (canInteract && GameInput.Instance != null && GameInput.Instance.GetInteractDown())
        {
            if (crane.isPlayerControlling)
            {
                DeactivateLever();
            }
            else if (currentPlayer != null)
            {
                ActivateLever();
            }
        }
    }

    private void ActivateLever()
    {
        crane.EnterControl(currentPlayer);
        if (uiPrompt) uiPrompt.SetActive(false);

        // Trigger Crane View (Zoom Out + Pan Right)
        if (CameraController.Instance != null)
            CameraController.Instance.SetCraneView(true);
    }

    private void DeactivateLever()
    {
        crane.ExitControl();
        if (uiPrompt) uiPrompt.SetActive(true);

        // Reset Camera
        if (CameraController.Instance != null)
            CameraController.Instance.SetCraneView(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            canInteract = true;
            currentPlayer = other.GetComponent<PlayerController>();
            if (uiPrompt && !crane.isPlayerControlling) uiPrompt.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (crane.isPlayerControlling) return; // Ignore exit if controlling

            canInteract = false;
            currentPlayer = null;
            if (uiPrompt) uiPrompt.SetActive(false);
        }
    }
}
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WaterHazard : MonoBehaviour
{
    [Header("Visuals")]
    public GameObject splashEffect;

    private bool triggerLock = false; // Prevent double-dunking in the same frame

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. Visual Feedback
        if (splashEffect != null)
        {
            Vector3 spawnPos = other.transform.position;
            spawnPos.z = -5f;
            Instantiate(splashEffect, spawnPos, Quaternion.identity);
        }

        // 2. Logic
        if (other.CompareTag("Player"))
        {
            TriggerPenalty("DROWNED");
        }
        else if (other.CompareTag("Enemy"))
        {
            Destroy(other.gameObject);
        }
        else if (other.CompareTag("Package"))
        {
            if (other.TryGetComponent<Package>(out var pkg))
            {
                if (pkg.isHeld)
                {
                    // If held, rely on player collision or trigger specifically here
                    TriggerPenalty("DROWNED");
                }
                else
                {
                    Debug.Log("Loose ball hit water - Respawning...");
                    pkg.Respawn();
                }
            }
        }
        else if (other.CompareTag("Grabbable"))
        {
            Destroy(other.gameObject, 0.5f);
        }
    }

    private void TriggerPenalty(string reason)
    {
        if (triggerLock) return; // Already dying

        if (GameManager.Instance && GameManager.Instance.currentState == GameManager.GameState.Playing)
        {
            triggerLock = true;
            Debug.Log($"WaterHazard: Triggering Down. Reason: {reason}");
            GameManager.Instance.UseDown(reason);
        }
    }

    void OnDisable() { triggerLock = false; }
}
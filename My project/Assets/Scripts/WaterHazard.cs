using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WaterHazard : MonoBehaviour
{
    [Header("Visuals")]
    public GameObject splashEffect;

    private bool triggerLock = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (splashEffect != null)
        {
            Vector3 spawnPos = other.transform.position;
            spawnPos.z = -5f;
            Instantiate(splashEffect, spawnPos, Quaternion.identity);
        }

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
                    TriggerPenalty("DROWNED");
                }
                else
                {
                    // FIX: If we are in Fumble Mode, hitting water is a TURNOVER, not a safe respawn
                    if (GameManager.Instance && GameManager.Instance.currentState == GameManager.GameState.Fumble)
                    {
                        TriggerPenalty("FUMBLE LOST");
                    }
                    else
                    {
                        Debug.Log("Loose ball hit water - Respawning...");
                    }
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
        if (triggerLock) return;

        if (GameManager.Instance)
        {
            triggerLock = true;
            Debug.Log($"WaterHazard: Triggering Down. Reason: {reason}");
            GameManager.Instance.UseDown(reason);
        }
    }

    void OnDisable() { triggerLock = false; }
}
using UnityEngine;

public class SurfaceZone : MonoBehaviour
{
    public enum ZoneType
    {
        OilSlick,
        Quicksand,
        Mud
    }

    [Header("Settings")]
    public ZoneType zoneType;

    [Header("Oil Settings")]
    [Tooltip("Lower value = More slippery (0.1 is ice, 1.0 is normal)")]
    public float frictionMultiplier = 0.1f;
    public float accelerationMultiplier = 0.5f;

    [Header("Quicksand Settings")]
    public float sinkingSpeed = -0.5f;
    public float moveSpeedPenalty = 0.3f;
    public float jumpPowerPenalty = 0.5f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && other.TryGetComponent<PlayerController>(out var player))
        {
            player.EnterSurfaceZone(this);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && other.TryGetComponent<PlayerController>(out var player))
        {
            player.ExitSurfaceZone(this);
        }
    }
}
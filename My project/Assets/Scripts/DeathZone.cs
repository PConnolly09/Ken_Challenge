using UnityEngine;

public class DeathZone : MonoBehaviour
{
    [Tooltip("If true, this will also destroy enemies that fall into it.")]
    public bool killEnemiesToo = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check for Player
        if (other.CompareTag("Player") && other.TryGetComponent<PlayerController>(out var player))
        {
            player.OutOfBounds();
        }
        // Check for Enemies (so they don't fall forever and lag the game)
        else if (killEnemiesToo && other.CompareTag("Enemy"))
        {
            Destroy(other.gameObject);
        }
    }
}
using UnityEngine;

public class SpringTile : MonoBehaviour
{
    [Header("Settings")]
    public float springForce = 25f;
    [Tooltip("If true, overrides current velocity. If false, adds to it.")]
    public bool resetVelocity = true;

    [Header("Feedback")]
    public Animator anim;
    public AudioSource audioSource;
    public AudioClip boingSfx;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if collision is coming from above roughly
        Vector2 normal = collision.GetContact(0).normal;

        // Normal pointing DOWN means the player hit the TOP of the spring
        if (normal.y < -0.5f)
        {
            if (collision.gameObject.TryGetComponent<PlayerController>(out var player))
            {
                ActivateSpring(player);
            }
        }
    }

    private void ActivateSpring(PlayerController player)
    {
        if (anim) anim.SetTrigger("Bounce");
        if (audioSource && boingSfx) audioSource.PlayOneShot(boingSfx);

        player.ApplySpringForce(springForce, resetVelocity);
    }
}
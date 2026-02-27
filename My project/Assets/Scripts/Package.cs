using UnityEngine;
using System.Collections;

public class Package : MonoBehaviour
{
    public bool isHeld = false;
    private Transform holdPoint;

    // RESTORED: The EnemyAI and PlayerController need to know who has this!
    public GameObject currentHolder;

    [Header("Visuals")]
    public SpriteRenderer spriteRenderer;
    [Tooltip("Assign a Particle System GameObject here. It will activate while the package is flashing.")]
    public GameObject flashingParticles;
    public float flashSpeed = 0.15f;
    public float defaultFlashDuration = 1.5f; // Used if an enemy drops it

    private Collider2D col;
    private Rigidbody2D rb;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // Ensure particles are off by default
        if (flashingParticles != null) flashingParticles.SetActive(false);
    }

    void Update()
    {
        if (isHeld && holdPoint != null)
        {
            transform.position = holdPoint.position;
            transform.rotation = holdPoint.rotation;
        }
    }

    // FIXED: Changed 'PlayerController player' to 'Component holder' so both Player and Enemy can use this
    public void SetHeld(bool held, Transform point, Component holder)
    {
        isHeld = held;
        holdPoint = point;

        // Set the current holder if it's being picked up, clear it if dropped
        currentHolder = (held && holder != null) ? holder.gameObject : null;

        if (isHeld)
        {
            if (rb) { rb.simulated = false; }
            if (col) { col.enabled = false; }
            StopAllCoroutines();

            spriteRenderer.enabled = true;
            if (flashingParticles) flashingParticles.SetActive(false); // Turn OFF particles
        }
        else
        {
            if (rb)
            {
                rb.simulated = true;
                rb.linearVelocity = new Vector2(Random.Range(-3f, 3f), 5f); // Pop out
            }
            if (col) { col.enabled = true; }

            // Determine how long to flash. If it's the player, use their stats. Otherwise, use default.
            float flashDuration = defaultFlashDuration;
            if (holder != null && holder.TryGetComponent<PlayerController>(out var player))
            {
                flashDuration = player.stats.fumblePickupDelay;
            }

            StartCoroutine(FlashRoutine(flashDuration));
        }
    }

    private IEnumerator FlashRoutine(float duration)
    {
        if (flashingParticles) flashingParticles.SetActive(true); // Turn ON particles

        float elapsed = 0f;
        while (elapsed < duration)
        {
            spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(flashSpeed);
            elapsed += flashSpeed;
        }

        spriteRenderer.enabled = true;
        if (flashingParticles) flashingParticles.SetActive(false); // Turn OFF particles
    }
}
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class HeavyObject : MonoBehaviour
{
    [Header("Crush Settings")]
    public float minCrushVelocity = 2f;
    public GameObject crushEffect;

    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        gameObject.tag = "Grabbable";

        rb.mass = 2204f;
        rb.gravityScale = 50f;
        rb.linearDamping = 30f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    public void OnGrab()
    {
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    public void OnRelease()
    {
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.WakeUp();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Ignore static collisions
        if (rb.bodyType != RigidbodyType2D.Dynamic) return;

        if (collision.gameObject.CompareTag("Enemy"))
        {
            // CRUSH CHECK:
            // 1. Is this object moving down faster than min velocity?
            // OR
            // 2. Is this object significantly above the enemy (Y difference)?

            bool fallingFast = collision.relativeVelocity.y > minCrushVelocity;
            bool isAbove = transform.position.y > collision.transform.position.y + 0.5f;

            if (fallingFast || isAbove)
            {
                if (collision.gameObject.TryGetComponent<EnemyAI>(out var enemy))
                {
                    Debug.Log("CRUSHED " + collision.gameObject.name);

                    if (crushEffect)
                        Instantiate(crushEffect, transform.position, Quaternion.identity);

                    Destroy(collision.gameObject);
                }
            }
        }
    }
}
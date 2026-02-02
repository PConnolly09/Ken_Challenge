using UnityEngine;

public class MinionEnemy : EnemyAI
{

    [Header("Minion Jump")]
    public float jumpForce = 12f;

    protected override void Chase()
    {
        if (currentTarget == null) return;

        float xDiff = currentTarget.position.x - transform.position.x;

        // Anti-Jitter: Only move if not stacked on top of target
        if (Mathf.Abs(xDiff) > 0.2f)
        {
            Move(Mathf.Sign(xDiff), moveSpeed * 1.2f);

            // Inside MinionEnemy.Chase()
            if (CheckWallAhead())
            {
                // Jump logic here
                rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
                // Don't flip immediately, try to clear it
            }
        }
        else
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
    }

    protected override void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Player") && !isKnockedBack)
        {
            if (col.gameObject.TryGetComponent<PlayerController>(out var player))
            {
                player.AddAttachment(gameObject);
            }
        }
        base.OnCollisionEnter2D(col);
    }
}

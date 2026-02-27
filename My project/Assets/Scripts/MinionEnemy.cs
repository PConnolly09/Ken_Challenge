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

            if (CheckWallAhead())
            {
                // FIX: Use proper jumping logic instead of spamming raw Force every frame
                AttemptNavigationJump();
            }
        }
        else
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
    }

    protected override void Die()
    {
        if (TryGetComponent<PooledMinion>(out var poolHelper))
        {
            poolHelper.DieAndReturnToPool();
        }
        else
        {
            base.Die();
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
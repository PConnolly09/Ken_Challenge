using UnityEngine;
using System.Collections;

public class BruteEnemy : EnemyAI
{
    [Header("Tackle")]
    public float tackleRange = 6f;
    public float tackleForce = 22f;
    public float tackleCooldown = 4f;

    private bool canTackle = true;
    private bool isTackling = false; // Is currently mid-lunge
    private bool isRecovering = false; // Is walking away

    protected override void ResetState()
    {
        canTackle = true;
        isTackling = false;
        isRecovering = false;
        StopAllCoroutines();
    }

    protected override void Chase()
    {
        if (currentTarget == null || isTackling) return;

        // 1. Recovery Phase (Walk away)
        if (isRecovering)
        {
            float dir = (transform.position.x > currentTarget.position.x) ? 1 : -1;
            Move(dir, moveSpeed * 0.5f);
            return;
        }

        float dist = Vector2.Distance(transform.position, currentTarget.position);

        // 2. Tackle Phase
        if (canTackle && dist < tackleRange)
        {
            StartCoroutine(TackleSequence());
        }
        // 3. Approach Phase
        else
        {
            float dir = Mathf.Sign(currentTarget.position.x - transform.position.x);
            Move(dir, moveSpeed * 0.8f);
        }
    }

    private IEnumerator TackleSequence()
    {
        canTackle = false;
        isTackling = true;

        // WIND UP (Stop and telegraph)
        rb.linearVelocity = Vector2.zero;
        if (EffectManager.Instance) EffectManager.Instance.TackleTelegraph(transform, 0.5f);
        yield return new WaitForSeconds(1.5f);

        // LUNGE
        if (currentTarget != null)
        {
            float dir = Mathf.Sign(currentTarget.position.x - transform.position.x);
            rb.linearVelocity = new Vector2(dir * tackleForce, 2f); // 2f Y gives a slight hop so he doesn't snag on floor
        }

        yield return new WaitForSeconds(1.0f); // Duration of the dangerous lunge

        // RECOVERY
        isTackling = false;
        isRecovering = true;
        yield return new WaitForSeconds(tackleCooldown);
        isRecovering = false;
        canTackle = true;
    }

    protected override void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Player") && !isKnockedBack)
        {
            if (col.gameObject.TryGetComponent<PlayerController>(out var player))
            {

                // If we hit the player, smash them
                // Force enter recovery immediately so we don't stand on them
                if (isTackling)
                {
                    player.SetProne(1.2f);
                    isTackling = false;
                    isRecovering = true;
                    StartCoroutine(ManualRecoveryRoutine());
                }
            }
        }
        base.OnCollisionEnter2D(col);
    }

    private IEnumerator ManualRecoveryRoutine()
    {
        yield return new WaitForSeconds(tackleCooldown);
        isRecovering = false;
        canTackle = true;
    }
}
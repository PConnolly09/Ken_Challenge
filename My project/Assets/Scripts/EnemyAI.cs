using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public abstract class EnemyAI : MonoBehaviour
{
    [Header("Base AI Settings")]
    [Range(0.1f, 20f)] public float moveSpeed = 3f;
    public float detectionRange = 10f;
    public LayerMask obstacleLayer;
    public GameObject bloodSplatterPrefab;

    [Header("Navigation")]
    public float navigationJumpForce = 8f;
    public float jumpCooldown = 1.5f;
    private float lastJumpTime;

    private float flipCooldownTimer = 0f;
    private float minTimeBetweenFlips = 0.5f;
    private float lastPatrolSwitchTime = 0f;

    [Header("Patrol Settings")]
    public Transform[] patrolPoints;
    protected int currentPatrolIndex = 0;
    protected float patrolWaitTimer = 0f;
    public float waitAtWaypointTime = 1f;

    [Header("Pathfinding")]
    public float giveUpDuration = 2.0f;
    private float targetIgnoreTimer;
    private float stuckCheckTimer = 0f;
    private float lastXPosition;
    protected bool isWaiting = false; // Prevents "stuck" jumps at waypoints

    [Header("Package Logic")]
    public bool carriesPackage = false;
    public Transform packageHoldPoint;
    public float fleeRangeMultiplier = 2.0f;
    protected Package currentHeldPackage;

    [Header("State Info")]
    protected bool movingRight = true;
    protected Rigidbody2D rb;
    protected Transform playerTransform;
    protected Transform currentTarget;
    protected bool isChasing = false;
    public bool isKnockedBack = false;
    protected Vector3 startPos;
    private Collider2D selfCollider;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        selfCollider = GetComponent<Collider2D>();
        rb.gravityScale = 4f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (selfCollider)
        {
            PhysicsMaterial2D noFriction = new PhysicsMaterial2D("EnemyZeroFriction");
            noFriction.friction = 0f;
            noFriction.bounciness = 0f;
            selfCollider.sharedMaterial = noFriction;
        }

        startPos = transform.position;
        lastXPosition = transform.position.x;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) playerTransform = p.transform;

        int layer = gameObject.layer;
        Physics2D.IgnoreLayerCollision(layer, layer, true);
        Physics2D.queriesStartInColliders = false;

        movingRight = transform.localScale.x > 0;
    }

    public void AssignPatrolRoute(Transform[] newWaypoints)
    {
        patrolPoints = newWaypoints;
        currentPatrolIndex = 0;
        patrolWaitTimer = 0f;
    }

    protected virtual void FixedUpdate()
    {
        if (flipCooldownTimer > 0) flipCooldownTimer -= Time.fixedDeltaTime;
        if (isKnockedBack || !enabled) return;

        MonitorStuckState();

        isWaiting = false; // Reset waiting status every frame

        if (carriesPackage)
        {
            RunAway();
            return;
        }

        if (targetIgnoreTimer > 0)
        {
            targetIgnoreTimer -= Time.fixedDeltaTime;
            isChasing = false;
            currentTarget = null;
            Patrol();
            return;
        }

        DetermineTarget();

        if (isChasing && currentTarget != null)
        {
            Chase();
        }
        else
        {
            Patrol();
        }
    }

    private void MonitorStuckState()
    {
        if (isWaiting)
        {
            stuckCheckTimer = 0;
            lastXPosition = transform.position.x;
            return;
        }

        if (Mathf.Abs(rb.linearVelocity.x) > 0.1f || isChasing || carriesPackage || patrolPoints.Length > 0)
        {
            float distMoved = Mathf.Abs(transform.position.x - lastXPosition);

            if (distMoved < 0.01f)
            {
                stuckCheckTimer += Time.fixedDeltaTime;

                if (stuckCheckTimer > 0.1f) AttemptNavigationJump();

                if (stuckCheckTimer > 1.5f)
                {
                    if (!isChasing && !carriesPackage) SwitchPatrolPoint();
                    else { Flip(); if (isChasing) ForceGiveUp(); }
                    stuckCheckTimer = 0;
                }
            }
            else
            {
                stuckCheckTimer = 0;
            }
        }
        lastXPosition = transform.position.x;
    }

    // FIX: Replaced thin raycasts with a comprehensive vertical BoxCast.
    // Accepts a distance parameter so they can look further ahead for upcoming floating steps.
    protected bool CheckWallAhead(float dist = 0.8f)
    {
        if (flipCooldownTimer > 0) return false;

        Vector2 dir = movingRight ? Vector2.right : Vector2.left;

        // A box 2.4 units tall covers from slightly below their feet to above their head.
        // Guarantees NO floating stairs or thin platforms can slip through undetected.
        Vector2 boxSize = new Vector2(0.1f, 2.4f);
        Vector2 boxCenter = (Vector2)transform.position + Vector2.up * 0.5f;

        RaycastHit2D hit = Physics2D.BoxCast(boxCenter, boxSize, 0f, dir, dist, obstacleLayer);

        return hit.collider != null;
    }

    protected bool CheckHazardAhead()
    {
        Vector2 origin = (Vector2)transform.position + (movingRight ? Vector2.right : Vector2.left) * 0.6f + Vector2.down * 0.7f;

        // Cast down 3 units (3 blocks) to check for a safe floor to drop onto
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 3.0f, obstacleLayer);

        if (hit.collider != null)
        {
            if (hit.collider.GetComponent<WaterHazard>() || hit.collider.CompareTag("Water"))
                return true; // Water is a hazard

            return false; // Safe ground found within 3 blocks, not a cliff! Let them drop down.
        }

        return true; // No ground within 3 blocks -> it's a true cliff, turn around
    }

    // HELPER: Verifies the minion is actually touching the floor
    protected bool IsGrounded()
    {
        Vector2 pos = transform.position;
        return Physics2D.Raycast(pos + Vector2.left * 0.3f, Vector2.down, 1.2f, obstacleLayer) ||
               Physics2D.Raycast(pos + Vector2.right * 0.3f, Vector2.down, 1.2f, obstacleLayer);
    }

    protected bool AttemptNavigationJump()
    {
        if (Time.time < lastJumpTime + 0.15f) return false;

        if (IsGrounded())
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, navigationJumpForce);
            lastJumpTime = Time.time;
            return true;
        }
        return false;
    }

    protected void ForceGiveUp()
    {
        targetIgnoreTimer = giveUpDuration;
        isChasing = false;
        Flip();
    }

    protected void DetermineTarget()
    {
        if (GameManager.Instance && GameManager.Instance.currentState == GameManager.GameState.Fumble)
        {
            if (GameManager.Instance.currentPackageTransform != null)
            {
                currentTarget = GameManager.Instance.currentPackageTransform;
                isChasing = true;
                return;
            }
        }

        if (playerTransform != null)
        {
            float dist = Vector2.Distance(transform.position, playerTransform.position);
            if (dist < detectionRange)
            {
                Vector2 dir = (playerTransform.position - transform.position).normalized;
                RaycastHit2D hit = Physics2D.Raycast(transform.position + Vector3.up * 0.5f, dir, dist, obstacleLayer);

                if (hit.collider == null)
                {
                    currentTarget = playerTransform;
                    isChasing = true;
                    return;
                }
            }
        }
        isChasing = false;
        currentTarget = null;
    }

    protected void SwitchPatrolPoint()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            if (Time.time - lastPatrolSwitchTime > 1.0f)
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
                patrolWaitTimer = 0;
                lastPatrolSwitchTime = Time.time;
                Flip();
            }
        }
    }

    protected virtual void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        Transform targetPoint = patrolPoints[currentPatrolIndex];
        float dist = Mathf.Abs(transform.position.x - targetPoint.position.x);

        if (dist < 0.5f)
        {
            isWaiting = true;
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            patrolWaitTimer += Time.fixedDeltaTime;
            if (patrolWaitTimer > waitAtWaypointTime)
            {
                SwitchPatrolPoint();
            }
            return;
        }

        // 1. Is there a wall/stair right in front of us? Jump it!
        if (CheckWallAhead(0.8f))
        {
            AttemptNavigationJump();
        }
        else if (IsGrounded())
        {
            // 2. We don't see a wall right here. But is there a floating step slightly further ahead?
            bool stepComingUp = CheckWallAhead(1.8f);

            // 3. If there is NO step coming up, AND it's a bottomless pit, turn around!
            // (If a step IS coming up, we bravely ignore the empty gap beneath it and keep walking!)
            if (!stepComingUp && CheckHazardAhead())
            {
                SwitchPatrolPoint();
                return;
            }
        }

        float dir = Mathf.Sign(targetPoint.position.x - transform.position.x);
        Move(dir, moveSpeed * 0.8f);
    }

    protected void RunAway()
    {
        if (playerTransform == null) return;

        // Apply the same smart floating-step gap logic to fleeing!
        if (CheckWallAhead(0.8f))
        {
            AttemptNavigationJump();
        }
        else if (IsGrounded())
        {
            bool stepComingUp = CheckWallAhead(1.8f);
            if (!stepComingUp && CheckHazardAhead())
            {
                Flip();
                return;
            }
        }

        float dir = (transform.position.x > playerTransform.position.x) ? 1 : -1;
        float dist = Vector2.Distance(transform.position, playerTransform.position);

        if (dist > detectionRange * fleeRangeMultiplier) rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        else Move(dir, moveSpeed * 1.5f);
    }

    protected void Move(float direction, float speed)
    {
        rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);

        if (direction > 0 && !movingRight) Flip();
        else if (direction < 0 && movingRight) Flip();
    }

    protected abstract void Chase();
    protected virtual void ResetState() { }

    public void TakeHit(float force, Vector2 direction, bool isStun)
    {
        if (carriesPackage) DropPackage();
        StopAllCoroutines();
        ResetState();
        StartCoroutine(KnockbackRoutine(force, direction, isStun));
    }

    public void DropPackage()
    {
        carriesPackage = false;
        if (currentHeldPackage != null)
        {
            if (currentHeldPackage.currentHolder == this.gameObject)
            {
                currentHeldPackage.SetHeld(false, null, null);

                if (selfCollider && currentHeldPackage.TryGetComponent<Collider2D>(out var pkgCol))
                {
                    Physics2D.IgnoreCollision(selfCollider, pkgCol, false);
                }
            }
            currentHeldPackage = null;
        }
    }

    private IEnumerator KnockbackRoutine(float force, Vector2 direction, bool isStun)
    {
        isKnockedBack = true;
        rb.linearVelocity = Vector2.zero;
        Vector2 safeDirection = new Vector2(direction.x, Mathf.Max(direction.y, 0.4f)).normalized;
        rb.AddForce(safeDirection * force, ForceMode2D.Impulse);
        yield return new WaitForSeconds(isStun ? 2.0f : 0.8f);
        isKnockedBack = false;
    }

    protected void Flip()
    {
        if (flipCooldownTimer > 0) return;

        movingRight = !movingRight;
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;

        flipCooldownTimer = minTimeBetweenFlips;
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (!carriesPackage && collision.gameObject.CompareTag("Package"))
        {
            if (collision.gameObject.TryGetComponent<Package>(out var pkg) && !pkg.isHeld)
            {
                carriesPackage = true;
                currentHeldPackage = pkg;
                pkg.SetHeld(true, packageHoldPoint, this);

                if (selfCollider) Physics2D.IgnoreCollision(selfCollider, collision.collider, true);
            }
        }
        if (isKnockedBack && ((1 << collision.gameObject.layer) & obstacleLayer) != 0)
        {
            if (EffectManager.Instance) EffectManager.Instance.PlayEffect(EffectManager.Instance.bloodSplatterPrefab, collision.contacts[0].point);
            if (carriesPackage) DropPackage();
            Die();
        }
    }

    protected virtual void OnDrawGizmos()
    {
        Gizmos.color = new Color(1, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Vector3 origin = transform.position + new Vector3(0, 0.5f, 0);
        Vector3 dir = movingRight ? Vector3.right : Vector3.left;
        Gizmos.DrawRay(origin, dir * 0.8f);

        if (patrolPoints != null)
        {
            Gizmos.color = Color.cyan;
            foreach (var p in patrolPoints) if (p) Gizmos.DrawSphere(p.position, 0.3f);
        }
    }
}
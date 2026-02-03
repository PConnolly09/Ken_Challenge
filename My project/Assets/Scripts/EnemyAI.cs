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
    private float stuckThreshold = 0.2f;
    private float lastXPosition;

    [Header("Package Logic")]
    public bool carriesPackage = false;
    public Transform packageHoldPoint;
    public float fleeRangeMultiplier = 2.0f;

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

    protected virtual void FixedUpdate()
    {
        if (flipCooldownTimer > 0) flipCooldownTimer -= Time.fixedDeltaTime;
        if (isKnockedBack || !enabled) return;

        MonitorStuckState();

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
            if (CheckWallAhead())
            {
                if (!AttemptNavigationJump())
                {
                    rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                    ForceGiveUp();
                }
            }
            else
            {
                Chase();
            }
        }
        else
        {
            Patrol();
        }
    }

    private void MonitorStuckState()
    {
        if (Mathf.Abs(rb.linearVelocity.x) > 0.1f || isChasing || carriesPackage)
        {
            float distMoved = Mathf.Abs(transform.position.x - lastXPosition);

            if (distMoved < 0.02f)
            {
                stuckCheckTimer += Time.fixedDeltaTime;
                if (stuckCheckTimer > stuckThreshold)
                {
                    // Stuck logic
                    if (!isChasing && !carriesPackage)
                    {
                        SwitchPatrolPoint();
                    }
                    else
                    {
                        if (!AttemptNavigationJump()) { Flip(); if (isChasing) ForceGiveUp(); }
                    }
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

    protected bool CheckWallAhead()
    {
        if (flipCooldownTimer > 0) return false;

        Vector2 pos = transform.position;
        Vector2 dir = movingRight ? Vector2.right : Vector2.left;
        float dist = 0.8f;

        bool hitTop = Physics2D.Raycast(pos + Vector2.up * 0.8f, dir, dist, obstacleLayer);
        bool hitMid = Physics2D.Raycast(pos + Vector2.up * 0.4f, dir, dist, obstacleLayer);
        bool hitBot = Physics2D.Raycast(pos + Vector2.up * 0.1f, dir, dist, obstacleLayer);

        return hitTop || hitMid || hitBot;
    }

    protected bool CheckHazardAhead()
    {
        Vector2 origin = (Vector2)transform.position + (movingRight ? Vector2.right : Vector2.left) * 0.8f;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 1.5f);

        if (hit.collider != null)
        {
            if (hit.collider.GetComponent<WaterHazard>()) return true;
            if (hit.collider.CompareTag("Water")) return true;
        }
        else
        {
            return true; // Cliff
        }
        return false;
    }

    protected bool AttemptNavigationJump()
    {
        if (Time.time < lastJumpTime + jumpCooldown) return false;

        bool isGrounded = Physics2D.Raycast(transform.position, Vector2.down, 1.1f, obstacleLayer);

        if (isGrounded)
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
            else
            {
                // If stuck again immediately, THEN jump
                AttemptNavigationJump();
            }
        }
    }

    protected virtual void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        if (CheckHazardAhead()) { SwitchPatrolPoint(); return; }

        // FIX: Prioritize Switching over Jumping for Patrol
        if (CheckWallAhead())
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            SwitchPatrolPoint();
            return;
        }

        Transform targetPoint = patrolPoints[currentPatrolIndex];
        float dist = Mathf.Abs(transform.position.x - targetPoint.position.x);

        if (dist < 0.5f)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            patrolWaitTimer += Time.fixedDeltaTime;
            if (patrolWaitTimer > waitAtWaypointTime)
            {
                SwitchPatrolPoint();
            }
        }
        else
        {
            float dir = Mathf.Sign(targetPoint.position.x - transform.position.x);
            Move(dir, moveSpeed * 0.8f);
        }
    }

    protected void RunAway()
    {
        if (playerTransform == null) return;

        if (CheckHazardAhead()) { Flip(); return; }

        if (CheckWallAhead())
        {
            if (!AttemptNavigationJump())
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                Flip();
            }
        }

        float dir = (transform.position.x > playerTransform.position.x) ? 1 : -1;
        float dist = Vector2.Distance(transform.position, playerTransform.position);

        if (dist > detectionRange * fleeRangeMultiplier)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
        else
        {
            Move(dir, moveSpeed * 1.5f);
        }
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
        if (GameManager.Instance && GameManager.Instance.currentPackageTransform)
        {
            Package pkg = GameManager.Instance.currentPackageTransform.GetComponent<Package>();
            if (pkg && pkg.currentHolder == this) pkg.SetHeld(false, null, null);
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

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (!carriesPackage && collision.gameObject.CompareTag("Package"))
        {
            if (collision.gameObject.TryGetComponent<Package>(out var pkg) && !pkg.isHeld)
            {
                carriesPackage = true;
                pkg.SetHeld(true, packageHoldPoint, this);
            }
        }
        if (isKnockedBack && ((1 << collision.gameObject.layer) & obstacleLayer) != 0)
        {
            if (EffectManager.Instance) EffectManager.Instance.PlayEffect(EffectManager.Instance.bloodSplatterPrefab, collision.contacts[0].point);
            if (carriesPackage) DropPackage();
            Destroy(gameObject);
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
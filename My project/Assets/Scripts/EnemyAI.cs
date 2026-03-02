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

    // Simplest, most robust stuck tracker
    private float stuckCheckTimer = 0f;
    private Vector2 stuckMarker;
    protected bool isWaiting = false;

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
        stuckMarker = transform.position;

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
        stuckMarker = transform.position;
    }

    protected virtual void FixedUpdate()
    {
        if (flipCooldownTimer > 0) flipCooldownTimer -= Time.fixedDeltaTime;
        if (isKnockedBack || !enabled) return;

        MonitorStuckState();

        isWaiting = false;

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
        if (isWaiting || patrolPoints == null || patrolPoints.Length == 0)
        {
            stuckCheckTimer = 0f;
            stuckMarker = transform.position;
            return;
        }

        // Use full Vector2 distance. If they move 0.2 units up OR forward, they are making progress.
        if (Vector2.Distance(transform.position, stuckMarker) > 0.2f)
        {
            stuckMarker = transform.position;
            stuckCheckTimer = 0f;
        }
        else
        {
            stuckCheckTimer += Time.fixedDeltaTime;

            // Panic jumps: If we haven't made progress, we might be caught on a tiny lip.
            if (stuckCheckTimer > 0.2f && stuckCheckTimer < 0.25f) AttemptNavigationJump();
            if (stuckCheckTimer > 0.6f && stuckCheckTimer < 0.65f) AttemptNavigationJump();
            if (stuckCheckTimer > 1.0f && stuckCheckTimer < 1.05f) AttemptNavigationJump();

            // Give up after 1.5s of absolutely no progress
            if (stuckCheckTimer > 1.5f)
            {
                if (!isChasing && !carriesPackage) SwitchPatrolPoint();
                else { Flip(); if (isChasing) ForceGiveUp(); }

                stuckCheckTimer = 0f;
                stuckMarker = transform.position;
            }
        }
    }

    protected bool CheckWallAhead()
    {
        if (flipCooldownTimer > 0) return false;

        Vector2 dir = movingRight ? Vector2.right : Vector2.left;

        // FIX: Wall vision is now 0.5 units! They will see stairs far in advance.
        float castDist = selfCollider.bounds.extents.x + 0.5f;

        Vector2 center = selfCollider.bounds.center;
        float bottomY = selfCollider.bounds.min.y + 0.1f;
        float midY = center.y;

        bool hitShin = Physics2D.Raycast(new Vector2(center.x, bottomY), dir, castDist, obstacleLayer);
        bool hitWaist = Physics2D.Raycast(new Vector2(center.x, midY), dir, castDist, obstacleLayer);

        return hitShin || hitWaist;
    }

    protected bool CheckHazardAhead()
    {
        Vector2 dir = movingRight ? Vector2.right : Vector2.left;

        // FIX: Cliff vision is only 0.2 units! They won't panic at gaps until they are right on the edge.
        float xOffset = dir.x * (selfCollider.bounds.extents.x + 0.2f);

        // Start raycast from their waist (center.y) to reliably cast downwards without clipping top stairs
        Vector2 origin = new Vector2(selfCollider.bounds.center.x + xOffset, selfCollider.bounds.center.y);

        // Cast down 4.0 units
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 4.0f, obstacleLayer);

        if (hit.collider != null)
        {
            if (hit.collider.GetComponent<WaterHazard>() || hit.collider.CompareTag("Water"))
                return true;
            return false; // Found a safe floor to drop onto
        }

        return true; // Bottomless pit detected
    }

    protected bool IsGrounded()
    {
        Vector2 center = selfCollider.bounds.center;
        float halfWidth = selfCollider.bounds.extents.x * 0.9f;
        float bottomY = selfCollider.bounds.min.y + 0.1f;

        // 3-point ground check ensures they can jump even when hanging off the edges of stairs
        return Physics2D.Raycast(new Vector2(center.x, bottomY), Vector2.down, 0.3f, obstacleLayer) ||
               Physics2D.Raycast(new Vector2(center.x - halfWidth, bottomY), Vector2.down, 0.3f, obstacleLayer) ||
               Physics2D.Raycast(new Vector2(center.x + halfWidth, bottomY), Vector2.down, 0.3f, obstacleLayer);
    }

    protected bool AttemptNavigationJump()
    {
        if (Time.time < lastJumpTime + 0.25f) return false;

        if (IsGrounded())
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, navigationJumpForce);
            lastJumpTime = Time.time;

            // Completely reset stuck tracking on every successful jump
            stuckCheckTimer = 0f;
            stuckMarker = transform.position;

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
            if (patrolWaitTimer > waitAtWaypointTime) SwitchPatrolPoint();
            return;
        }

        if (CheckWallAhead())
        {
            AttemptNavigationJump();
        }
        else if (IsGrounded() && CheckHazardAhead())
        {
            SwitchPatrolPoint();
            return;
        }

        float dir = Mathf.Sign(targetPoint.position.x - transform.position.x);
        Move(dir, moveSpeed * 0.8f);
    }

    protected void RunAway()
    {
        if (playerTransform == null) return;

        if (CheckWallAhead())
        {
            AttemptNavigationJump();
        }
        else if (IsGrounded() && CheckHazardAhead())
        {
            Flip();
            return;
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
        stuckMarker = transform.position;
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
        if (selfCollider == null) selfCollider = GetComponent<Collider2D>();
        if (selfCollider == null) return;

        Gizmos.color = new Color(1, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Wall Check Gizmo
        Gizmos.color = Color.red;
        Vector2 dir = movingRight ? Vector2.right : Vector2.left;
        float castDist = selfCollider.bounds.extents.x + 0.5f;
        Vector2 center = selfCollider.bounds.center;
        Gizmos.DrawRay(new Vector2(center.x, selfCollider.bounds.min.y + 0.1f), dir * castDist);
        Gizmos.DrawRay(new Vector2(center.x, center.y), dir * castDist);

        // Hazard Check Gizmo
        Gizmos.color = Color.blue;
        float xOffset = movingRight ? selfCollider.bounds.extents.x + 0.2f : -(selfCollider.bounds.extents.x + 0.2f);
        Vector2 hazOrigin = new Vector2(selfCollider.bounds.center.x + xOffset, selfCollider.bounds.center.y);
        Gizmos.DrawRay(hazOrigin, Vector2.down * 4.0f);
    }
}
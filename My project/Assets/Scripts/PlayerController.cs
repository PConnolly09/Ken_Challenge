using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(CinemachineImpulseSource))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Configuration")]
    public PlayerStats stats;

    [Header("References")]
    public Transform groundCheck;
    public Transform stiffArmPoint;
    public Transform attachmentPoint;
    public GameObject packageObject;
    public GameObject bloodSplatterPrefab;

    [Header("Audio")]
    public AudioClip jumpSfx;
    public AudioClip jukeSfx;
    public AudioClip spinSfx;
    public AudioClip stiffArmSfx;
    public AudioClip fumbleSfx;
    public AudioClip impactSfx;
    [Range(0.1f, 1f)] public float footstepInterval = 0.35f;

    // --- STATE ---
    public bool IsGrounded { get; private set; }
    public bool isStiffArming;
    public bool isSpinning;
    public bool isJuking;
    public bool isProne;
    public bool hasPackage = true;
    public int attachmentCount = 0;

    // --- INTERNAL PHYSICS ---
    private Rigidbody2D rb;
    private CapsuleCollider2D col;
    private Animator anim;
    private CinemachineImpulseSource impulseSource;
    private AudioSource audioSource;
    private SpriteRenderer spriteRenderer;

    private Vector2 _velocity;
    private float _gravity;
    private float _jumpVelocity;
    private float _horizontalInput;

    // Timers
    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private float _proneTimer;
    private float _tackleDebuffTimer;
    public float _pickupTimer;
    private float _jukeTimer;
    private float _footstepTimer;

    private readonly List<GameObject> attachedEnemies = new();
    private Color normalColor = Color.white; // FIX: Restored missing variable
    private Vector3 originalScale;
    private Vector3 targetSquashScale;

    private PhysicsMaterial2D noFrictionMat;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CapsuleCollider2D>();
        anim = GetComponent<Animator>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
        audioSource = GetComponent<AudioSource>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        rb.gravityScale = 0;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        noFrictionMat = new PhysicsMaterial2D("ZeroFriction") { friction = 0f, bounciness = 0f };
        col.sharedMaterial = noFrictionMat;

        originalScale = transform.localScale;
        targetSquashScale = originalScale;
        if (spriteRenderer) normalColor = spriteRenderer.color;

        if (packageObject != null && packageObject.TryGetComponent<Collider2D>(out var packColl))
        {
            Physics2D.IgnoreCollision(col, packColl, true);
        }

        CalculatePhysicsConstants();
    }

    void OnValidate()
    {
        if (stats != null) CalculatePhysicsConstants();
    }

    private void CalculatePhysicsConstants()
    {
        _gravity = -(2 * stats.jumpHeight) / Mathf.Pow(stats.timeToJumpApex, 2);
        _jumpVelocity = Mathf.Abs(_gravity) * stats.timeToJumpApex;
    }

    void Update()
    {
        if (GameManager.Instance == null) { _horizontalInput = 0; return; }
        if (GameManager.Instance.isIntroSequence) { _horizontalInput = 0; return; }

        if (isProne)
        {
            HandleProneState();
            UpdateAnimations();
            return;
        }

        HandleInput();
        UpdateTimers();
        CheckGrounded();
        HandleFootsteps();
        HandleSquashAndStretch();
        UpdateAnimations();
    }

    void FixedUpdate()
    {
        if (isProne || isSpinning) return;

        CalculateMovement();
        HandleCornerCorrection();
        rb.linearVelocity = _velocity;
    }

    private void CalculateMovement()
    {
        float penaltyFactor = Mathf.Max(0.1f, 1.0f - (attachmentCount * 0.3f));
        if (_tackleDebuffTimer > 0) penaltyFactor *= 0.5f;

        float targetSpeed = _horizontalInput * stats.maxRunSpeed * penaltyFactor;

        if (Mathf.Abs(_horizontalInput) < 0.01f && IsGrounded)
        {
            _velocity.x = 0;
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
        else
        {
            float accelRate;
            bool isApex = !IsGrounded && Mathf.Abs(_velocity.y) < stats.apexThreshold;

            if (IsGrounded)
                accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? (1 / stats.groundAccelerationTime) : (1 / stats.groundDecelerationTime);
            else
                accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? (1 / stats.groundAccelerationTime) * stats.airAccelMult : (1 / stats.groundDecelerationTime) * stats.airDecelMult;

            if (isApex) accelRate *= stats.apexAirAccelMult;

            _velocity.x = Mathf.MoveTowards(_velocity.x, targetSpeed, accelRate * stats.maxRunSpeed * Time.fixedDeltaTime);
        }

        float gravityMult = 1f;
        if (GameInput.Instance != null)
        {
            if (_velocity.y > 0 && !GameInput.Instance.GetJumpHeld()) gravityMult = stats.jumpCutGravityMult;
            else if (_velocity.y < 0) gravityMult = stats.downwardGravityMult;
        }

        bool isApexHang = !IsGrounded && Mathf.Abs(_velocity.y) < stats.apexThreshold;
        if (isApexHang) gravityMult *= stats.apexGravityMult;

        _velocity.y += _gravity * gravityMult * Time.fixedDeltaTime;
        _velocity.y = Mathf.Max(_velocity.y, -stats.maxFallSpeed);

        if (_horizontalInput != 0 && !isSpinning)
        {
            float dir = Mathf.Sign(_horizontalInput);
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * dir, transform.localScale.y, transform.localScale.z);
        }
    }

    private void HandleFootsteps()
    {
        if (IsGrounded && Mathf.Abs(_horizontalInput) > 0.1f && !isProne)
        {
            _footstepTimer -= Time.deltaTime;
            if (_footstepTimer <= 0)
            {
                float speedFactor = Mathf.Clamp(Mathf.Abs(_velocity.x) / stats.maxRunSpeed, 0.5f, 1.5f);
                _footstepTimer = footstepInterval / speedFactor;

                if (AudioManager.Instance) AudioManager.Instance.PlayRandomFootstep(false);
                if (EffectManager.Instance) EffectManager.Instance.PlayEffect(EffectManager.Instance.footstepDustPrefab, groundCheck.position, 0.6f, 0.5f);
            }
        }
        else
        {
            _footstepTimer = 0;
        }
    }

    private void HandleInput()
    {
        if (GameInput.Instance == null) return;

        _horizontalInput = GameInput.Instance.GetMovementInput().x;

        if (GameInput.Instance.GetJumpDown()) _jumpBufferTimer = stats.jumpBufferTime;

        if (_jumpBufferTimer > 0 && _coyoteTimer > 0) ExecuteJump();

        if (GameInput.Instance.GetSpinDown() && !isSpinning) StartCoroutine(PerformSpinMove());
        if (GameInput.Instance.GetStiffArmDown() && !isStiffArming) PerformStiffArm();
        if (GameInput.Instance.GetJukeDown() && !isJuking && _jukeTimer <= 0) StartCoroutine(PerformJuke());
    }

    private void ExecuteJump()
    {
        _jumpBufferTimer = 0;
        _coyoteTimer = 0;

        float jumpPenaltyFactor = Mathf.Max(0f, 1.0f - (attachmentCount * 0.3f));
        float speedRatio = Mathf.Abs(_velocity.x) / stats.maxRunSpeed;
        float baseJumpForce = _jumpVelocity + (stats.momentumJumpBonus * speedRatio);

        _velocity.y = baseJumpForce * jumpPenaltyFactor;

        PlaySound(jumpSfx);
        if (EffectManager.Instance) EffectManager.Instance.PlayEffect(EffectManager.Instance.jumpDustPrefab, groundCheck.position);
        ApplyImpulseSquash(new Vector3(0.7f, 1.4f, 1f));
    }

    private void CheckGrounded()
    {
        if (_velocity.y > 0.1f)
        {
            IsGrounded = false;
            _coyoteTimer = 0;
            if (CameraController.Instance) CameraController.Instance.SetPlayerGrounded(false);
            return;
        }

        bool wasGrounded = IsGrounded;
        IsGrounded = Physics2D.OverlapCircle(groundCheck.position, stats.groundCheckRadius, stats.groundLayer);

        if (IsGrounded)
        {
            _coyoteTimer = stats.coyoteTime;

            if (!wasGrounded)
            {
                if (_velocity.y < -5f)
                {
                    if (EffectManager.Instance) EffectManager.Instance.PlayEffect(EffectManager.Instance.landDustPrefab, groundCheck.position);
                    ApplyImpulseSquash(new Vector3(1.3f, 0.7f, 1f));
                }
                if (CameraController.Instance) CameraController.Instance.SetPlayerGrounded(true);
            }
        }
        else
        {
            _coyoteTimer -= Time.deltaTime;
            if (CameraController.Instance) CameraController.Instance.SetPlayerGrounded(false);
        }
    }

    private void HandleCornerCorrection()
    {
        if (_velocity.y <= 0) return;
        Vector2 pos = transform.position;
        Vector2 size = col.size;
        Vector2 origin = new Vector2(pos.x, pos.y + (size.y * 0.5f));

        RaycastHit2D hit = Physics2D.BoxCast(origin, new Vector2(size.x * 0.8f, 0.1f), 0, Vector2.up, 0.1f, stats.groundLayer);

        if (hit)
        {
            float dist = hit.point.x - pos.x;
            if (Mathf.Abs(dist) > (size.x / 2f) - stats.cornerCorrectionDistance)
            {
                float nudge = (dist > 0) ? -0.05f : 0.05f;
                Vector3 p = transform.position;
                p.x += nudge;
                transform.position = p;
            }
        }
    }

    private void HandleSquashAndStretch()
    {
        targetSquashScale = Vector3.Lerp(targetSquashScale, originalScale, Time.deltaTime * stats.squashSpeed);
        float direction = Mathf.Sign(transform.localScale.x);
        transform.localScale = new Vector3(Mathf.Abs(targetSquashScale.x) * direction, targetSquashScale.y, 1f);
    }

    private void ApplyImpulseSquash(Vector3 scaleForce)
    {
        targetSquashScale = new Vector3(originalScale.x * scaleForce.x, originalScale.y * scaleForce.y, 1f);
        float direction = Mathf.Sign(transform.localScale.x);
        transform.localScale = new Vector3(Mathf.Abs(targetSquashScale.x) * direction, targetSquashScale.y, 1f);
    }

    private void PerformStiffArm()
    {
        StartCoroutine(StiffArmRoutine());
        anim.SetTrigger("StiffArmTrigger");
        PlaySound(stiffArmSfx);
    }

    private IEnumerator StiffArmRoutine()
    {
        isStiffArming = true;
        CheckStiffArmHit();
        yield return new WaitForSeconds(0.1f);
        CheckStiffArmHit();
        yield return new WaitForSeconds(0.2f);
        isStiffArming = false;
    }

    private void CheckStiffArmHit()
    {
        Collider2D[] enemies = Physics2D.OverlapCircleAll(stiffArmPoint.position, stats.stiffArmRange);
        foreach (var enemyCol in enemies)
        {
            if (enemyCol.CompareTag("Enemy"))
            {
                if (enemyCol.TryGetComponent<EnemyAI>(out var enemyScript) && !enemyScript.isKnockedBack)
                {
                    float momentumBonus = Mathf.Abs(_velocity.x) * stats.speedPushMultiplier;
                    Vector2 dir = new Vector2(transform.localScale.x, 0.2f).normalized;

                    enemyScript.TakeHit(stats.stiffArmForce + momentumBonus, dir, false);
                    if (EffectManager.Instance) EffectManager.Instance.PlayEffect(EffectManager.Instance.stiffArmImpactPrefab, enemyCol.transform.position);
                    impulseSource.GenerateImpulse(0.5f);
                    PlaySound(impactSfx);
                }
            }
        }
    }

    private IEnumerator PerformJuke()
    {
        isJuking = true;
        _jukeTimer = stats.jukeCooldown;
        PlaySound(jukeSfx);
        if (EffectManager.Instance) EffectManager.Instance.PlayEffect(EffectManager.Instance.jukeGhostPrefab, transform.position);

        spriteRenderer.color = stats.jukeColor;
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), true);

        yield return new WaitForSeconds(stats.jukeDuration);

        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), false);
        spriteRenderer.color = normalColor;
        isJuking = false;
    }

    private IEnumerator PerformSpinMove()
    {
        isSpinning = true;
        impulseSource.GenerateImpulse(0.3f);
        PlaySound(spinSfx);
        anim.SetTrigger("SpinTrigger");
        if (EffectManager.Instance) EffectManager.Instance.PlayEffect(EffectManager.Instance.spinTrailPrefab, transform.position);

        int playerLayer = gameObject.layer;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);

        float facingDir = transform.localScale.x > 0 ? 1 : -1;
        bool hasInput = Mathf.Abs(_horizontalInput) > 0.1f;

        if (attachmentCount == 0)
        {
            if (hasInput) _velocity = new Vector2(Mathf.Sign(_horizontalInput) * stats.spinMoveForce, 0);
            else _velocity = Vector2.zero;

            Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, 2.5f);
            foreach (var col in nearby)
            {
                if (col.CompareTag("Enemy") && col.TryGetComponent<EnemyAI>(out var e))
                    e.TakeHit(10f, (col.transform.position - transform.position).normalized, true);
            }
        }
        else
        {
            _velocity = Vector2.zero;
            GameObject drop = attachedEnemies[0];
            attachedEnemies.RemoveAt(0);
            attachmentCount--;
            UpdateUI();

            drop.transform.SetParent(null);
            Vector3 dropPos = drop.transform.position; dropPos.z = 0; drop.transform.position = dropPos;
            drop.GetComponent<Collider2D>().enabled = true;
            if (drop.TryGetComponent<Rigidbody2D>(out var erb)) erb.simulated = true;
            if (drop.TryGetComponent<EnemyAI>(out var s)) { s.enabled = true; s.TakeHit(15f, new Vector2(Random.Range(-1f, 1f), 1f).normalized, true); }
            Physics2D.IgnoreCollision(col, drop.GetComponent<Collider2D>(), false);
        }

        float elapsed = 0;
        while (elapsed < stats.spinDuration)
        {
            transform.localScale = new Vector3(-transform.localScale.x, originalScale.y, originalScale.z);
            yield return new WaitForSeconds(0.05f);
            elapsed += 0.05f;
        }

        transform.localScale = new Vector3(_horizontalInput != 0 ? Mathf.Sign(_horizontalInput) : facingDir, originalScale.y, originalScale.z);
        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, false);
        isSpinning = false;
    }

    private void AttemptPickup(Collision2D collision)
    {
        if (!hasPackage && _pickupTimer <= 0 && collision.collider.CompareTag("Package"))
        {
            if (collision.gameObject.TryGetComponent<Package>(out var pkg) && !pkg.isHeld)
            {
                hasPackage = true;
                packageObject = collision.gameObject;
                pkg.SetHeld(true, attachmentPoint, this);
                Physics2D.IgnoreCollision(col, collision.collider, true);
                PlaySound(jumpSfx);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        AttemptPickup(collision);

        if (collision.collider.CompareTag("Enemy") && !isSpinning)
        {
            if (collision.gameObject.TryGetComponent<EnemyAI>(out var enemy))
            {
                if (isStiffArming)
                {
                    enemy.TakeHit(30f, new Vector2(transform.localScale.x, 0.3f).normalized, false);
                }
                else if (enemy is MinionEnemy && !enemy.carriesPackage)
                {
                    AddAttachment(collision.gameObject);
                }
                else
                {
                    if (enemy is BruteEnemy) { _tackleDebuffTimer = 1.5f; PlaySound(impactSfx, 1.2f); }
                    ProcessFumble(0.2f);
                }
            }
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        AttemptPickup(collision);
    }

    private void HandleProneState()
    {
        if (GameInput.Instance != null && GameInput.Instance.GetJumpDown()) _proneTimer -= 0.2f;

        _proneTimer -= Time.deltaTime;
        if (_proneTimer <= 0)
        {
            isProne = false;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            transform.rotation = Quaternion.identity;
        }
    }

    public void ProcessFumble(float extraRisk = 0)
    {
        if (!hasPackage || isSpinning || isJuking) return;
        if (Random.value < (stats.baseFumbleChance + (attachmentCount * 0.15f) + extraRisk))
        {
            hasPackage = false; _pickupTimer = stats.fumblePickupDelay;
            if (packageObject != null && packageObject.TryGetComponent<Package>(out var pkg)) pkg.SetHeld(false, null, this);
            impulseSource.GenerateImpulse(1.5f); PlaySound(fumbleSfx);
            if (EffectManager.Instance) EffectManager.Instance.PlayEffect(EffectManager.Instance.fumbleExplosionPrefab, transform.position);
        }
    }

    public void AddAttachment(GameObject enemy)
    {
        if (isSpinning || isJuking || attachedEnemies.Contains(enemy)) return;
        attachmentCount++; attachedEnemies.Add(enemy); UpdateUI();
        Physics2D.IgnoreCollision(col, enemy.GetComponent<Collider2D>(), true);
        enemy.GetComponent<EnemyAI>().enabled = false;
        if (enemy.TryGetComponent<Rigidbody2D>(out var erb)) { erb.linearVelocity = Vector2.zero; erb.simulated = false; }
        enemy.transform.SetParent(attachmentPoint);
        enemy.transform.localPosition = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.2f, 0.3f), 0);
        if (EffectManager.Instance) EffectManager.Instance.PlayEffect(EffectManager.Instance.attachPoofPrefab, enemy.transform.position);
    }

    public void SetProne(float duration)
    {
        if (isJuking || isSpinning) return;
        isProne = true;
        _proneTimer = duration;
        _velocity.x = 0;
        rb.linearVelocity = Vector2.zero;

        transform.rotation = Quaternion.Euler(0, 0, 90);
        impulseSource.GenerateImpulse(1.5f);
        PlaySound(impactSfx);
        ProcessFumble(0.4f);
        if (EffectManager.Instance) EffectManager.Instance.PlayEffect(EffectManager.Instance.tackleImpactPrefab, transform.position);
    }

    private void UpdateTimers()
    {
        if (_jumpBufferTimer > 0) _jumpBufferTimer -= Time.deltaTime;
        if (_jukeTimer > 0) _jukeTimer -= Time.deltaTime;
        if (_pickupTimer > 0) _pickupTimer -= Time.deltaTime;
        if (_tackleDebuffTimer > 0) _tackleDebuffTimer -= Time.deltaTime;
    }

    private void UpdateAnimations()
    {
        if (anim == null) return;
        float s = Mathf.Abs(rb.linearVelocity.x);
        anim.SetFloat("Speed", s);
        anim.SetFloat("RunMultiplier", Mathf.Clamp(s / stats.baseRunSpeed, 0.5f, 3f));
        anim.SetBool("isGrounded", IsGrounded);
        anim.SetBool("isProne", isProne);
    }

    private void UpdateUI() { if (GameManager.Instance) GameManager.Instance.UpdateAttachmentCount(attachmentCount); }
    private void PlaySound(AudioClip c, float v = 1f) { if (c && audioSource) audioSource.PlayOneShot(c, v); }
    private void OnDrawGizmosSelected() { if (groundCheck) Gizmos.DrawWireSphere(groundCheck.position, stats.groundCheckRadius); if (stiffArmPoint) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(stiffArmPoint.position, stats.stiffArmRange); } }
}
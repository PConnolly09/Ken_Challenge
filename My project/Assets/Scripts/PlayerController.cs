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

    [Header("Audio")]
    public AudioClip jumpSfx;
    public AudioClip jukeSfx;
    public AudioClip spinSfx;
    public AudioClip stiffArmSfx;
    public AudioClip fumbleSfx;
    public AudioClip impactSfx;
    [Range(0.1f, 1f)] public float footstepInterval = 0.3f;

    // --- STATE ---
    public bool IsGrounded { get; private set; }
    public bool isStiffArming;
    public bool isSpinning;
    public bool isJuking;
    public bool isProne;
    public bool hasPackage = true;
    public int attachmentCount = 0;

    // --- SURFACE MODIFIERS ---
    private SurfaceZone currentSurface;
    private bool isInQuicksand = false;
    private bool isInOil = false;

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
    private Color normalColor = Color.white;
    private Vector3 originalScale;
    private Vector3 targetSquashScale;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CapsuleCollider2D>();
        anim = GetComponent<Animator>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
        audioSource = GetComponent<AudioSource>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Physics Setup
        rb.gravityScale = 0;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        originalScale = transform.localScale;
        targetSquashScale = originalScale;
        if (spriteRenderer) normalColor = spriteRenderer.color;

        // Ignore collision with initial package
        if (packageObject != null && packageObject.TryGetComponent<Collider2D>(out var packColl))
        {
            Physics2D.IgnoreCollision(col, packColl, true);
        }

        CalculatePhysicsConstants();
    }

    private void CalculatePhysicsConstants()
    {
        if (stats.timeToJumpApex <= 0) stats.timeToJumpApex = 0.32f;
        _gravity = -(2 * stats.jumpHeight) / Mathf.Pow(stats.timeToJumpApex, 2);
        _jumpVelocity = Mathf.Abs(_gravity) * stats.timeToJumpApex;
    }

    void Update()
    {
        if (GameManager.Instance && GameManager.Instance.isIntroSequence) return;

        if (isProne)
        {
            HandleProneState();
            UpdateAnimations();
            return;
        }

        HandleInput();

        // Handle One-Way Platform Drop Down
        if (GameInput.Instance != null && IsGrounded)
        {
            float yInput = GameInput.Instance.GetMovementInput().y;
            if (yInput < -0.7f && GameInput.Instance.GetJumpDown())
            {
                StartCoroutine(DisableOneWayCollision());
            }
        }

        HandleFootsteps();
        HandleSquashAndStretch();
        UpdateAnimations();
        UpdateTimers();
    }

    void FixedUpdate()
    {
        CheckGrounded();

        if (isProne || isSpinning) return;

        CalculateMovement();
        HandleCornerCorrection();

        rb.linearVelocity = _velocity;
    }

    // --- OUT OF BOUNDS LOGIC ---
    public void OutOfBounds()
    {
        // Stop the player in their tracks so they don't fall forever
        _velocity = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;

        // Hide player so they don't awkwardly float in the void
        if (spriteRenderer) spriteRenderer.enabled = false;

        // Guarantee a fumble so the play ends visually
        if (hasPackage) ProcessFumble(1.0f);

        // Visual / Audio Feedback
        if (CameraController.Instance) CameraController.Instance.Shake(2f);
        if (EffectManager.Instance)
            EffectManager.Instance.PlayEffect(EffectManager.Instance.fumbleExplosionPrefab, transform.position);

        // Tell the GameManager to burn a down
        if (GameManager.Instance)
        {
            GameManager.Instance.UseDown();
        }
    }

    // --- MOVEMENT LOGIC ---
    private void CalculateMovement()
    {
        float penaltyFactor = Mathf.Max(0.3f, 1.0f - (attachmentCount * stats.speedPenaltyPerEnemy));
        if (_tackleDebuffTimer > 0) penaltyFactor *= 0.6f;

        if (isInQuicksand && currentSurface != null) penaltyFactor *= currentSurface.moveSpeedPenalty;

        float targetSpeed = _horizontalInput * stats.maxRunSpeed * penaltyFactor;

        float accelRate;

        if (IsGrounded)
        {
            float surfaceAccelMult = (isInOil && currentSurface != null) ? currentSurface.accelerationMultiplier : 1.0f;
            float surfaceFrictionMult = (isInOil && currentSurface != null) ? currentSurface.frictionMultiplier : 1.0f;

            if (Mathf.Abs(targetSpeed) > 0.01f)
            {
                accelRate = (1 / stats.groundAccelerationTime) * surfaceAccelMult;
            }
            else
            {
                accelRate = (1 / stats.groundDecelerationTime) * surfaceFrictionMult;
            }
        }
        else
        {
            accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? (1 / stats.groundAccelerationTime) * stats.airAccelMult : (1 / stats.groundDecelerationTime) * stats.airDecelMult;
        }

        bool isApex = !IsGrounded && Mathf.Abs(_velocity.y) < stats.apexThreshold;
        if (isApex && Mathf.Abs(targetSpeed) > 0.01f) accelRate *= stats.apexAirAccelMult;

        _velocity.x = Mathf.MoveTowards(_velocity.x, targetSpeed, accelRate * stats.maxRunSpeed * Time.fixedDeltaTime);

        // --- Vertical ---
        if (isInQuicksand && currentSurface != null)
        {
            _velocity.y = currentSurface.sinkingSpeed;
        }
        else if (IsGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }
        else
        {
            float gMult = 1f;
            if (GameInput.Instance != null)
            {
                if (_velocity.y > 0 && !GameInput.Instance.GetJumpHeld()) gMult = stats.jumpCutGravityMult;
                else if (_velocity.y < 0) gMult = stats.downwardGravityMult;
            }
            if (isApex) gMult *= stats.apexGravityMult;

            _velocity.y += _gravity * gMult * Time.fixedDeltaTime;
            _velocity.y = Mathf.Max(_velocity.y, -stats.maxFallSpeed);
        }

        // Flip
        if (_horizontalInput != 0 && !isSpinning)
        {
            float dir = Mathf.Sign(_horizontalInput);
            transform.localScale = new Vector3(Mathf.Abs(originalScale.x) * dir, originalScale.y, originalScale.z);
        }
    }

    private void HandleInput()
    {
        if (GameInput.Instance == null) return;

        _horizontalInput = GameInput.Instance.GetMovementInput().x;

        if (GameInput.Instance.GetJumpDown()) _jumpBufferTimer = stats.jumpBufferTime;
        if (_jumpBufferTimer > 0 && (_coyoteTimer > 0 || isInQuicksand)) ExecuteJump();

        if (GameInput.Instance.GetSpinDown() && !isSpinning) StartCoroutine(PerformSpinMove());
        if (GameInput.Instance.GetStiffArmDown() && !isStiffArming) PerformStiffArm();
        if (GameInput.Instance.GetJukeDown() && !isJuking && _jukeTimer <= 0) StartCoroutine(PerformJuke());
    }

    private void ExecuteJump()
    {
        _jumpBufferTimer = 0;
        _coyoteTimer = 0;

        float speedRatio = Mathf.Abs(_velocity.x) / stats.maxRunSpeed;
        float baseJump = _jumpVelocity;

        if (isInQuicksand && currentSurface != null) baseJump *= currentSurface.jumpPowerPenalty;

        _velocity.y = baseJump + (stats.momentumJumpBonus * speedRatio);

        PlaySound(jumpSfx);
        if (EffectManager.Instance) EffectManager.Instance.PlayEffect(EffectManager.Instance.jumpDustPrefab, groundCheck.position);
        ApplyImpulseSquash(new Vector3(0.7f, 1.4f, 1f));

        IsGrounded = false;
        isInQuicksand = false;
    }

    // --- INTERACTIONS & EXTERNAL CALLS ---

    public Vector2 GetVelocity() => _velocity;

    public void ApplySpringForce(float force, bool resetVelocity)
    {
        if (resetVelocity) _velocity.y = 0;
        _velocity.y += force;
        IsGrounded = false;
        _coyoteTimer = 0;
        ApplyImpulseSquash(new Vector3(0.6f, 1.5f, 1f));
    }

    public void EnterSurfaceZone(SurfaceZone zone)
    {
        currentSurface = zone;
        if (zone.zoneType == SurfaceZone.ZoneType.OilSlick) isInOil = true;
        else if (zone.zoneType == SurfaceZone.ZoneType.Quicksand) isInQuicksand = true;
    }

    public void ExitSurfaceZone(SurfaceZone zone)
    {
        if (currentSurface == zone)
        {
            isInOil = false;
            isInQuicksand = false;
            currentSurface = null;
        }
    }

    private IEnumerator DisableOneWayCollision()
    {
        Collider2D[] platformColliders = Physics2D.OverlapCircleAll(groundCheck.position, 0.2f, stats.groundLayer);
        foreach (var platform in platformColliders) Physics2D.IgnoreCollision(col, platform, true);
        yield return new WaitForSeconds(0.4f);
        foreach (var platform in platformColliders) Physics2D.IgnoreCollision(col, platform, false);
    }

    // --- STANDARD ABILITIES ---

    private void PerformStiffArm()
    {
        isStiffArming = true;
        anim.SetTrigger("StiffArmTrigger");
        PlaySound(stiffArmSfx);
        if (CameraController.Instance) CameraController.Instance.Shake(stats.camShakeOnStiffArm);

        Collider2D[] enemies = Physics2D.OverlapCircleAll(stiffArmPoint.position, stats.stiffArmRange);
        foreach (var eCol in enemies)
        {
            if (eCol.CompareTag("Enemy") && eCol.TryGetComponent<EnemyAI>(out var enemy))
            {
                if (!enemy.isKnockedBack)
                {
                    float momentumBonus = Mathf.Abs(_velocity.x) * stats.speedPushMultiplier;
                    enemy.TakeHit(stats.stiffArmForce + momentumBonus, new Vector2(transform.localScale.x, 0.2f), false);
                    if (EffectManager.Instance) EffectManager.Instance.PlayEffect(EffectManager.Instance.stiffArmImpactPrefab, eCol.transform.position);
                    StartCoroutine(HitStop(0.06f));
                    PlaySound(impactSfx);
                }
            }
        }
        Invoke(nameof(EndStiffArm), stats.stiffArmDuration);
    }

    private void EndStiffArm() => isStiffArming = false;

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

        if (attachmentCount == 0)
        {
            if (Mathf.Abs(_horizontalInput) > 0.1f)
                _velocity = new Vector2(Mathf.Sign(_horizontalInput) * stats.spinMoveForce, 0);

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
            if (attachedEnemies.Count > 0)
            {
                GameObject drop = attachedEnemies[0];
                attachedEnemies.RemoveAt(0);
                attachmentCount--;
                UpdateUI();

                drop.transform.SetParent(null);
                drop.GetComponent<Collider2D>().enabled = true;
                if (drop.TryGetComponent<Rigidbody2D>(out var erb)) { erb.simulated = true; }
                if (drop.TryGetComponent<EnemyAI>(out var s))
                {
                    s.enabled = true;
                    s.TakeHit(15f, new Vector2(Random.Range(-1f, 1f), 1f).normalized, true);
                }
                Physics2D.IgnoreCollision(col, drop.GetComponent<Collider2D>(), false);
            }
        }

        float elapsed = 0;
        while (elapsed < stats.spinDuration)
        {
            transform.localScale = new Vector3(-transform.localScale.x, originalScale.y, originalScale.z);
            yield return new WaitForSeconds(0.05f);
            elapsed += 0.05f;
        }

        isSpinning = false;
        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, false);
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

    // --- COLLISIONS & ATTACHMENTS ---

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & stats.groundLayer) != 0)
        {
            if (_velocity.y < 0)
            {
                _velocity.y = 0;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
            }
        }
        HandleCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision) => HandleCollision(collision);

    private void HandleCollision(Collision2D collision)
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

        if (collision.collider.CompareTag("Enemy") && !isSpinning && !isJuking)
        {
            if (collision.gameObject.TryGetComponent<EnemyAI>(out var enemy))
            {
                if (isStiffArming) return;

                if (enemy is MinionEnemy && !enemy.carriesPackage) AddAttachment(collision.gameObject);
                else
                {
                    if (enemy is BruteEnemy) _tackleDebuffTimer = 1.5f;
                    ProcessFumble(0.2f);
                }
            }
        }
    }

    public void AddAttachment(GameObject enemy)
    {
        if (isSpinning || isJuking || attachedEnemies.Contains(enemy)) return;
        attachmentCount++;
        attachedEnemies.Add(enemy);
        UpdateUI();

        Physics2D.IgnoreCollision(col, enemy.GetComponent<Collider2D>(), true);
        enemy.GetComponent<EnemyAI>().enabled = false;
        if (enemy.TryGetComponent<Rigidbody2D>(out var erb)) { erb.linearVelocity = Vector2.zero; erb.simulated = false; }

        enemy.transform.SetParent(attachmentPoint);
        enemy.transform.localPosition = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.2f, 0.3f), 0);
        if (EffectManager.Instance) EffectManager.Instance.PlayEffect(EffectManager.Instance.attachPoofPrefab, enemy.transform.position);
    }

    public void ProcessFumble(float extraRisk = 0)
    {
        if (!hasPackage || isSpinning || isJuking) return;
        if (Random.value < (stats.baseFumbleChance + (attachmentCount * 0.15f) + extraRisk))
        {
            hasPackage = false;
            _pickupTimer = stats.fumblePickupDelay;
            if (packageObject != null && packageObject.TryGetComponent<Package>(out var pkg))
                pkg.SetHeld(false, null, this);

            impulseSource.GenerateImpulse(1.5f);
            PlaySound(fumbleSfx);
            if (EffectManager.Instance) EffectManager.Instance.PlayEffect(EffectManager.Instance.fumbleExplosionPrefab, transform.position);
        }
    }

    // --- HELPERS ---

    private void CheckGrounded()
    {
        if (_velocity.y > 0.1f) { IsGrounded = false; return; }

        bool wasGrounded = IsGrounded;
        IsGrounded = Physics2D.OverlapCircle(groundCheck.position, stats.groundCheckRadius, stats.groundLayer);

        if (IsGrounded)
        {
            _coyoteTimer = stats.coyoteTime;
            if (!wasGrounded)
            {
                if (_velocity.y < -10f)
                {
                    ApplyImpulseSquash(new Vector3(1.4f, 0.6f, 1f));
                    StartCoroutine(HitStop(stats.landHitStop));
                    if (CameraController.Instance) CameraController.Instance.Shake(stats.camShakeOnLand);
                    if (EffectManager.Instance) EffectManager.Instance.PlayEffect(EffectManager.Instance.landDustPrefab, groundCheck.position);
                }
            }
        }
        else
        {
            _coyoteTimer -= Time.deltaTime;
        }
    }

    private IEnumerator HitStop(float duration)
    {
        if (duration <= 0) yield break;
        float originalTime = Time.timeScale;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = originalTime;
    }

    public void SetProne(float duration)
    {
        if (isJuking || isSpinning) return;
        isProne = true;
        _proneTimer = duration;
        _velocity = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        transform.rotation = Quaternion.Euler(0, 0, 90);
        impulseSource.GenerateImpulse(1.5f);
        PlaySound(impactSfx);
        ProcessFumble(0.4f);
    }

    private void HandleProneState()
    {
        _proneTimer -= Time.deltaTime;
        if (_proneTimer <= 0)
        {
            isProne = false;
            transform.rotation = Quaternion.identity;

            rb.simulated = true;
            if (spriteRenderer) spriteRenderer.enabled = true;
        }
    }

    private void HandleCornerCorrection()
    {
        if (_velocity.y <= 0) return;
        RaycastHit2D hit = Physics2D.BoxCast(new Vector2(transform.position.x, transform.position.y + 1f), new Vector2(col.size.x * 0.8f, 0.1f), 0, Vector2.up, 0.1f, stats.groundLayer);
        if (hit && Mathf.Abs(hit.point.x - transform.position.x) > (col.size.x / 2f) - stats.cornerCorrectionDistance)
            transform.position += new Vector3((hit.point.x - transform.position.x) > 0 ? -0.1f : 0.1f, 0, 0);
    }

    private void HandleSquashAndStretch()
    {
        targetSquashScale = Vector3.Lerp(targetSquashScale, originalScale, Time.deltaTime * stats.squashSpeed);
        float dir = Mathf.Sign(transform.localScale.x);
        transform.localScale = new Vector3(Mathf.Abs(targetSquashScale.x) * dir, targetSquashScale.y, 1f);
    }
    private void ApplyImpulseSquash(Vector3 scale) => targetSquashScale = scale;
    private void HandleFootsteps()
    {
        if (IsGrounded && Mathf.Abs(_horizontalInput) > 0.1f && !isProne)
        {
            _footstepTimer -= Time.deltaTime;
            if (_footstepTimer <= 0) { _footstepTimer = footstepInterval; if (AudioManager.Instance) AudioManager.Instance.PlayRandomFootstep(false); }
        }
    }
    private void UpdateTimers()
    {
        if (_jumpBufferTimer > 0) _jumpBufferTimer -= Time.deltaTime;
        if (_jukeTimer > 0) _jukeTimer -= Time.deltaTime;
        if (_pickupTimer > 0) _pickupTimer -= Time.deltaTime;
        if (_tackleDebuffTimer > 0) _tackleDebuffTimer -= Time.deltaTime;
    }
    private void UpdateAnimations() { if (anim) { anim.SetFloat("Speed", Mathf.Abs(_velocity.x)); anim.SetBool("isGrounded", IsGrounded); anim.SetBool("isProne", isProne); } }
    private void UpdateUI() { if (GameManager.Instance) GameManager.Instance.UpdateAttachmentCount(attachmentCount); }
    private void PlaySound(AudioClip c) { if (c && audioSource) audioSource.PlayOneShot(c); }
}
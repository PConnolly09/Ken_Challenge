using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class Package : MonoBehaviour
{
    private Rigidbody2D rb;
    private Collider2D coll;
    private Transform targetAnchor;
    private SpriteRenderer spriteRenderer;

    [Header("Status")]
    public bool isHeld = false;
    [Tooltip("Check this if the player starts the level holding this.")]
    public bool startHeld = false;
    public MonoBehaviour currentHolder;

    [Header("Visuals")]
    public Color normalColor = Color.white;
    public Color flashColor = new Color(1f, 0.8f, 0.5f, 1f);
    public float flashSpeed = 3f;

    [Header("Physics Settings")]
    public float dropGravity = 3f;
    public float airDrag = 0.5f;
    public float maxFumbleSpeed = 15f;
    public PhysicsMaterial2D fumbleMaterial;

    private PhysicsMaterial2D settlingMaterial;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        coll = GetComponent<CircleCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        gameObject.tag = "Package";
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        settlingMaterial = new PhysicsMaterial2D("DeadBall") { bounciness = 0f, friction = 0.8f };

        if (fumbleMaterial != null) coll.sharedMaterial = fumbleMaterial;
        if (spriteRenderer) normalColor = spriteRenderer.color;
    }

    void Start()
    {
        if (startHeld)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && player.TryGetComponent<PlayerController>(out var pc))
            {
                pc.hasPackage = true;
                pc.packageObject = gameObject;
                SetHeld(true, pc.attachmentPoint, pc);
            }
        }
        else
        {
            isHeld = false;
            transform.SetParent(null);
            rb.simulated = true;
            coll.enabled = true;
            coll.isTrigger = false;
            rb.gravityScale = dropGravity;
            rb.linearDamping = airDrag;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            coll.sharedMaterial = settlingMaterial;
        }
    }

    void Update()
    {
        bool shouldFlash = true;

        if (isHeld)
        {
            if (targetAnchor == null)
            {
                SetHeld(false, null, null);
                return;
            }
            transform.position = targetAnchor.position;
            transform.rotation = targetAnchor.rotation;

            // If held by Player, STOP flashing. Otherwise (Enemy), keep flashing.
            if (currentHolder is PlayerController) shouldFlash = false;
        }

        if (shouldFlash) ApplyFlash();
        else if (spriteRenderer) spriteRenderer.color = normalColor;
    }

    private void ApplyFlash()
    {
        if (spriteRenderer)
        {
            float t = Mathf.PingPong(Time.time * flashSpeed, 1f);
            spriteRenderer.color = Color.Lerp(normalColor, flashColor, t);
        }
    }

    void FixedUpdate()
    {
        if (!isHeld)
        {
            rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, maxFumbleSpeed);

            if (rb.linearVelocity.magnitude < 3f)
            {
                if (coll.sharedMaterial != settlingMaterial)
                    coll.sharedMaterial = settlingMaterial;
            }
        }
    }

    public void Respawn()
    {
        SetHeld(false, null, null);
        rb.linearVelocity = Vector2.zero;
        coll.sharedMaterial = settlingMaterial;
        if (GameManager.Instance) GameManager.Instance.RecoverFumble();
    }

    public void SetHeld(bool held, Transform anchor, MonoBehaviour holder)
    {
        isHeld = held;
        targetAnchor = anchor;
        currentHolder = held ? holder : null;

        if (held)
        {
            rb.simulated = false;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            coll.enabled = false;

            if (holder is PlayerController && GameManager.Instance)
                GameManager.Instance.RecoverFumble();
        }
        else
        {
            transform.SetParent(null);
            rb.simulated = true;
            coll.enabled = true;
            coll.isTrigger = false;

            rb.gravityScale = dropGravity;
            rb.linearDamping = airDrag;
            coll.sharedMaterial = fumbleMaterial;
            rb.WakeUp();

            Vector2 fumbleDir = new Vector2(Random.Range(-0.6f, 0.6f), 1f).normalized;
            rb.AddForce(fumbleDir * 12f, ForceMode2D.Impulse);
            rb.AddTorque(Random.Range(-50f, 50f), ForceMode2D.Impulse);

            if (GameManager.Instance)
                GameManager.Instance.StartFumbleEvent(this.transform);
        }
    }
}
using UnityEngine;

public class CraneController : MonoBehaviour
{
    // Global reference for UI to check status
    public static CraneController ActiveCrane { get; private set; }

    public enum ControlMode { MoveStructure, OperateCart }

    [Header("Hierarchy References")]
    public Transform gantry;
    public Transform cart;
    public Transform grabber;
    public SpriteRenderer ropeLeft;
    public SpriteRenderer ropeRight;

    [Header("Settings")]
    public ControlMode currentMode = ControlMode.OperateCart;
    public bool isPlayerControlling = false;
    public float gantrySpeed = 4f;
    public float cartSpeed = 4f;
    public float hoistSpeed = 3f;

    [Header("Limits (Local Space)")]
    public float gantryTravelDist = 10f;
    public float cartMinLocalX = -4f;
    public float cartMaxLocalX = 4f;
    public float grabberMinLocalY = -5f;
    public float grabberMaxLocalY = -1f;

    [Header("Claw Logic")]
    public Transform grabPoint;
    public float grabRadius = 0.8f;
    public LayerMask grabbableLayer;
    private GameObject currentObject;
    private Rigidbody2D currentObjectRb;

    // Internal state
    private float gantryStartX;
    private float grabberInitialLocalX;
    private PlayerController playerRef;
    private Rigidbody2D playerRb;
    private RigidbodyType2D originalPlayerBodyType;

    void Awake()
    {
        if (gantry) gantryStartX = gantry.position.x;
        if (grabber) grabberInitialLocalX = grabber.localPosition.x;

        SetupKinematic(gantry);
        SetupKinematic(cart);
        SetupKinematic(grabber);
    }

    private void SetupKinematic(Transform t)
    {
        if (!t) return;
        Rigidbody2D rb = t.GetComponent<Rigidbody2D>();
        if (!rb) rb = t.gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.mass = 1000f;
    }

    void Update()
    {
        UpdateRopes();

        if (!isPlayerControlling || GameInput.Instance == null) return;

        if (GameInput.Instance.GetCraneModeSwitch())
        {
            currentMode = (currentMode == ControlMode.MoveStructure)
                ? ControlMode.OperateCart
                : ControlMode.MoveStructure;
        }

        if (GameInput.Instance.GetPauseDown()) ExitControl();

        HandleMovement();
    }

    void HandleMovement()
    {
        Vector2 input = GameInput.Instance.GetMovementInput();
        float hInput = input.x;
        float vInput = input.y;

        if (currentMode == ControlMode.MoveStructure)
        {
            if (gantry != null && Mathf.Abs(hInput) > 0.01f)
            {
                float nextX = gantry.position.x + (hInput * gantrySpeed * Time.deltaTime);
                nextX = Mathf.Clamp(nextX, gantryStartX - gantryTravelDist, gantryStartX + gantryTravelDist);
                gantry.position = new Vector3(nextX, gantry.position.y, gantry.position.z);
            }
        }
        else
        {
            if (cart != null && Mathf.Abs(hInput) > 0.01f)
            {
                float nextLocalX = cart.localPosition.x + (hInput * cartSpeed * Time.deltaTime);
                nextLocalX = Mathf.Clamp(nextLocalX, cartMinLocalX, cartMaxLocalX);
                cart.localPosition = new Vector3(nextLocalX, cart.localPosition.y, cart.localPosition.z);
            }

            if (grabber != null && Mathf.Abs(vInput) > 0.01f)
            {
                float nextLocalY = grabber.localPosition.y + (vInput * hoistSpeed * Time.deltaTime);
                nextLocalY = Mathf.Clamp(nextLocalY, grabberMinLocalY, grabberMaxLocalY);
                grabber.localPosition = new Vector3(grabberInitialLocalX, nextLocalY, grabber.localPosition.z);
            }

            if (GameInput.Instance.GetJumpDown())
            {
                if (currentObject) Release();
                else AttemptGrab();
            }
        }
    }

    void AttemptGrab()
    {
        Collider2D hit = Physics2D.OverlapCircle(grabPoint.position, grabRadius, grabbableLayer);

        if (hit && hit.CompareTag("Grabbable"))
        {
            Debug.Log("CRANE: Picked up " + hit.name);
            currentObject = hit.gameObject;
            currentObjectRb = currentObject.GetComponent<Rigidbody2D>();

            if (currentObjectRb)
            {
                currentObjectRb.simulated = false;
                currentObjectRb.linearVelocity = Vector2.zero;
            }

            currentObject.transform.SetParent(grabber);
            currentObject.transform.SetPositionAndRotation(grabPoint.position, Quaternion.identity);

            if (currentObject.TryGetComponent<HeavyObject>(out var heavy)) heavy.OnGrab();
        }
    }

    void Release()
    {
        if (currentObject != null)
        {
            Debug.Log("CRANE: Dropped " + currentObject.name);

            if (currentObject.TryGetComponent<HeavyObject>(out var heavy)) heavy.OnRelease();

            currentObject.transform.SetParent(null);

            if (currentObjectRb)
            {
                currentObjectRb.simulated = true;
                currentObjectRb.WakeUp();
                currentObjectRb.AddForce(Vector2.down * 2f, ForceMode2D.Impulse);
            }
        }

        currentObject = null;
        currentObjectRb = null;
    }

    void UpdateRopes()
    {
        if (!cart || !grabber || !ropeLeft || !ropeRight) return;
        float height = Mathf.Abs(Mathf.Abs(cart.position.y) - Mathf.Abs(grabber.position.y));
        Vector2 newSizeL = new(ropeLeft.size.x, height);
        Vector2 newSizeR = new(ropeRight.size.x, height);
        ropeLeft.size = newSizeL;
        ropeRight.size = newSizeR;
    }

    public void EnterControl(PlayerController player)
    {
        isPlayerControlling = true;
        ActiveCrane = this; // Register as global active crane for UI
        playerRef = player;
        player.enabled = false;

        if (player.TryGetComponent<Rigidbody2D>(out playerRb))
        {
            originalPlayerBodyType = playerRb.bodyType;
            playerRb.linearVelocity = Vector2.zero;
            playerRb.bodyType = RigidbodyType2D.Kinematic;
        }
        currentMode = ControlMode.OperateCart;
    }

    public void ExitControl()
    {
        isPlayerControlling = false;
        if (ActiveCrane == this) ActiveCrane = null; // Deregister

        if (playerRef)
        {
            if (playerRb)
            {
                playerRb.bodyType = originalPlayerBodyType;
                playerRb.WakeUp();
            }
            playerRef.enabled = true;
            playerRef = null;
            playerRb = null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (grabPoint)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(grabPoint.position, grabRadius);
        }
    }
}
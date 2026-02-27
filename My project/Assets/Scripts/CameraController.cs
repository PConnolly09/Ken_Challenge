using UnityEngine;
using Unity.Cinemachine;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance;

    [Header("Cinemachine References")]
    public CinemachineCamera virtualCamera;
    private CinemachinePositionComposer positionComposer;
    private CinemachineImpulseSource globalImpulseSource;
    private Rigidbody2D playerRb;
    private PlayerController playerController;

    [Header("Zoom & Framing")]
    public float orthographicSize = 10f;
    public float craneOrthographicSize = 14f;

    [Header("Directional Framing (X-Axis)")]
    [Tooltip("Player on Left Third (0.3) when facing Right (Forward)")]
    public float forwardBias = 0.3f;
    [Tooltip("Player on Right Third (0.7) when facing Left (Backward)")]
    public float backwardBias = 0.7f;
    [Tooltip("Deep pan offsets after running one way for a long time")]
    public float deepForwardBias = 0.2f;
    public float deepBackwardBias = 0.8f;

    [Header("Dynamic Panning Delays")]
    [Tooltip("Time the player must hold a new direction before the camera flips.")]
    public float turnDelay = 0.35f;
    [Tooltip("Seconds of continuous movement before the deep pan starts.")]
    public float deepPanDelay = 3.0f;

    [Tooltip("Standard snappy pan speed")]
    public float panSmoothTime = 0.25f;
    [Tooltip("Slow, cinematic pan speed for the deep pan")]
    public float deepPanSmoothTime = 2.0f;

    private float currentXBias;
    private float targetXBias;
    private float panVelocity = 0f;
    private bool isFacingRight = true;
    private float currentTurnTimer = 0f;
    private float timeFacingSameDirection = 0f;
    private float targetSize;
    private bool isCraneView = false;

    [Header("Vertical & Tracking Settings")]
    public float verticalDeadZone = 0.3f;
    public float horizontalDeadZone = 0.15f;
    public float screenYBias = 0.2f;

    [Tooltip("Standard Cinemachine damping keeps it smooth but tightly tracked")]
    public float horizontalDamping = 0.15f;
    public float verticalDamping = 0.5f;

    [Header("Y-Axis Offsets (Falling & Thud)")]
    public float lookDownOffset = -4f;
    public float fallLookDownOffset = -8f;
    public float fallSpeedThreshold = -18f;
    public float lookShiftSpeed = 4f;
    [Tooltip("How fast the camera snaps back up when you hit the floor")]
    public float thudCatchupSpeed = 12f;
    private float currentYOffset = 0f;

    void Awake()
    {
        Instance = this;
        globalImpulseSource = GetComponent<CinemachineImpulseSource>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            playerRb = player.GetComponent<Rigidbody2D>();
            playerController = player.GetComponent<PlayerController>();

            if (playerController)
            {
                isFacingRight = playerController.transform.localScale.x > 0;
            }
        }

        if (virtualCamera != null)
        {
            positionComposer = virtualCamera.GetComponent<CinemachinePositionComposer>();
            targetSize = orthographicSize;
            targetXBias = isFacingRight ? forwardBias : backwardBias;
            currentXBias = targetXBias;
            ApplyCameraSettings();
        }
    }

    void Update()
    {
        if (virtualCamera != null)
        {
            virtualCamera.Lens.OrthographicSize = Mathf.Lerp(virtualCamera.Lens.OrthographicSize, targetSize, Time.deltaTime * 3f);
        }

        if (!isCraneView)
        {
            HandleDirectionalFraming();
            HandleVerticalFraming();
        }
        else
        {
            // If in crane view, gracefully slide to the center
            currentXBias = Mathf.SmoothDamp(currentXBias, 0.5f, ref panVelocity, panSmoothTime);
            UpdateComposer();

            // Reset Y Offset as well
            currentYOffset = Mathf.Lerp(currentYOffset, 0f, Time.deltaTime * lookShiftSpeed);
            positionComposer.TargetOffset = new Vector3(0, currentYOffset, 0);
        }
    }

    private void HandleDirectionalFraming()
    {
        if (playerRb == null || positionComposer == null || playerController == null) return;

        // 1. Determine Intent based on INPUT or VELOCITY (Fixes the "standing still" bug)
        float inputX = GameInput.Instance != null ? GameInput.Instance.GetMovementInput().x : Input.GetAxisRaw("Horizontal");
        float velX = playerRb.linearVelocity.x;
        int intentDir = 0;

        if (Mathf.Abs(inputX) > 0.1f)
        {
            intentDir = (int)Mathf.Sign(inputX);
        }
        else if (Mathf.Abs(velX) > 1.5f)
        {
            intentDir = (int)Mathf.Sign(velX);
        }

        // 2. Turn Delay & Sustained Direction Logic
        if (intentDir != 0)
        {
            bool intendsRight = intentDir > 0;
            if (intendsRight != isFacingRight)
            {
                currentTurnTimer += Time.deltaTime;
                if (currentTurnTimer >= turnDelay)
                {
                    isFacingRight = intendsRight;
                    currentTurnTimer = 0f;
                    timeFacingSameDirection = 0f; // Reset deep pan
                }
            }
            else
            {
                currentTurnTimer = 0f;
                timeFacingSameDirection += Time.deltaTime; // Building up to the deep pan
            }
        }
        else
        {
            currentTurnTimer = 0f;
            // CRITICAL FIX: Reset the deep pan timer if the player stops moving. 
            // This stops the camera from drifting off while you're just standing there.
            timeFacingSameDirection = 0f;
        }

        // 3. Dual-Stage SmoothDamp Application
        float activeSmoothTime = panSmoothTime;

        if (isFacingRight)
        {
            if (timeFacingSameDirection >= deepPanDelay)
            {
                targetXBias = deepForwardBias;
                activeSmoothTime = deepPanSmoothTime;
            }
            else targetXBias = forwardBias;
        }
        else
        {
            if (timeFacingSameDirection >= deepPanDelay)
            {
                targetXBias = deepBackwardBias;
                activeSmoothTime = deepPanSmoothTime;
            }
            else targetXBias = backwardBias;
        }

        currentXBias = Mathf.SmoothDamp(currentXBias, targetXBias, ref panVelocity, activeSmoothTime);

        UpdateComposer();
    }

    private void HandleVerticalFraming()
    {
        if (playerRb == null || positionComposer == null) return;

        float targetOffset = 0f;
        float activeShiftSpeed = lookShiftSpeed;

        // Manual Look Down
        if (GameInput.Instance != null && GameInput.Instance.GetMovementInput().y < -0.5f)
        {
            targetOffset = lookDownOffset;
        }
        else if (Input.GetAxisRaw("Vertical") < -0.5f)
        {
            targetOffset = lookDownOffset;
        }

        // Falling Camera offset
        bool isGrounded = playerController != null && playerController.IsGrounded;
        bool fallingFast = !isGrounded && playerRb.linearVelocity.y < fallSpeedThreshold;

        if (fallingFast)
        {
            targetOffset = fallLookDownOffset;
        }

        // "Thud" Catchup - snap the camera back quickly when we land
        if (isGrounded && currentYOffset < -0.1f)
        {
            activeShiftSpeed = thudCatchupSpeed;
        }

        currentYOffset = Mathf.Lerp(currentYOffset, targetOffset, Time.deltaTime * activeShiftSpeed);
        positionComposer.TargetOffset = new Vector3(0, currentYOffset, 0);
    }

    private void UpdateComposer()
    {
        if (positionComposer == null) return;
        var comp = positionComposer.Composition;
        comp.ScreenPosition = new Vector2(currentXBias, screenYBias);
        positionComposer.Composition = comp;
    }

    public void ApplyCameraSettings()
    {
        if (positionComposer == null) return;

        var comp = positionComposer.Composition;
        comp.ScreenPosition = new Vector2(currentXBias, screenYBias);

        comp.DeadZone.Size = new Vector2(horizontalDeadZone, verticalDeadZone);
        comp.HardLimits.Size = new Vector2(2f, 2f);

        positionComposer.Composition = comp;

        // Restored normal damping so it tracks smoothly and doesn't fight the player
        positionComposer.Damping = new Vector3(horizontalDamping, verticalDamping, 0f);
    }

    public void SetCraneView(bool active)
    {
        isCraneView = active;
        if (active)
        {
            targetSize = craneOrthographicSize;
        }
        else
        {
            targetSize = orthographicSize;
            timeFacingSameDirection = 0f;
        }
    }

    public void Shake(float force = 1f)
    {
        if (globalImpulseSource) globalImpulseSource.GenerateImpulse(Vector3.one * force);
    }
}
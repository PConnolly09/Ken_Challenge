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
    public float orthographicSize = 6f;

    [Header("Directional Framing")]
    // Player on Left Third (0.3) when facing Right (Forward)
    public float forwardBias = 0.3f;
    // Player on Right Third (0.7) when facing Left (Backward)
    public float backwardBias = 0.7f;

    [Header("Dynamic Panning")]
    public float switchThreshold = 0.1f;

    [Tooltip("Speed of camera pan when standing still (No Input, No Velocity).")]
    public float minBiasSpeed = 0.2f;

    [Tooltip("Speed of camera pan when moving fast OR holding input.")]
    public float maxBiasSpeed = 2.5f;
    public float highSpeedThreshold = 6.0f;

    [Tooltip("Time in seconds the player must face the new direction while standing still before camera switches.")]
    public float stationaryTurnDelay = 1.0f;

    [Tooltip("Time in seconds the player must be continuously moving in the new direction before camera switches.")]
    public float movingTurnDelay = 0.5f;

    private float currentXBias;
    private float currentBiasSpeed; // Internal smoother
    private bool isFacingRight = true;

    [Header("Debug")]
    public float turnTimer = 0f;

    private float targetSize;

    private bool isCraneView = false;

    [Header("Vertical")]
    public float verticalDeadZone = 0.3f;
    public float screenYBias = 0.2f;
    public float horizontalDamping = 0.5f;

    [Header("Look Down")]
    public float lookDownOffset = -4f;
    public float lookShiftSpeed = 2f;
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
                // Initialize direction based on player visual scale
                isFacingRight = playerController.transform.localScale.x > 0;
            }
        }

        if (virtualCamera != null)
        {
            positionComposer = virtualCamera.GetComponent<CinemachinePositionComposer>();
            // Ensure we start with a valid 0-1 range bias
            currentXBias = isFacingRight ? forwardBias : backwardBias;
            targetSize = orthographicSize;
            ApplyCameraSettings();
        }
    }

    void Update()
    {
        if (virtualCamera != null)
        {
            virtualCamera.Lens.OrthographicSize = Mathf.Lerp(virtualCamera.Lens.OrthographicSize, targetSize, Time.deltaTime * 2f);
        }

        if (!isCraneView)
        {
            HandleDirectionalFraming();
            HandleLookDown();
        }
    }

    private void HandleDirectionalFraming()
    {
        if (playerRb == null || positionComposer == null || playerController == null) return;

        // 1. Determine Player State
        bool playerFacingRight = playerController.transform.localScale.x > 0;
        float velocityX = playerRb.linearVelocity.x;

        // Determine Input State (Used to keep camera alive during collisions)
        float inputX = 0f;
        if (GameInput.Instance != null) inputX = GameInput.Instance.GetMovementInput().x;
        else inputX = Input.GetAxisRaw("Horizontal");

        // Are we moving? And are we moving in the direction we are facing?
        // We consider "Moving" if we have velocity OR significant Input (prevents dead stop on collision)
        bool isMoving = Mathf.Abs(velocityX) > 0.5f || Mathf.Abs(inputX) > 0.1f;

        // Check if movement/input aligns with facing direction
        float dirCheck = (Mathf.Abs(velocityX) > 0.1f) ? velocityX : inputX;
        bool movingSameDir = isMoving && (Mathf.Sign(dirCheck) == (playerFacingRight ? 1f : -1f));

        // 2. Check if we need to switch Camera Side
        if (playerFacingRight != isFacingRight)
        {
            turnTimer += Time.deltaTime;

            // Use different delays based on whether movement is committed
            float requiredDelay = movingSameDir ? movingTurnDelay : stationaryTurnDelay;

            if (turnTimer > requiredDelay)
            {
                isFacingRight = playerFacingRight;
                turnTimer = 0f;
            }
        }
        else
        {
            // Reset timer if player turns back to the original camera direction
            turnTimer = 0f;
        }

        // 3. Calculate Pan Speed
        // If we have Input, we maintain Max Speed even if velocity drops (e.g. pushing a box/package)
        bool hasActiveInput = Mathf.Abs(inputX) > 0.1f;
        float absVel = Mathf.Abs(velocityX);

        float targetSpeed = (absVel > highSpeedThreshold || hasActiveInput) ? maxBiasSpeed : minBiasSpeed;

        // Use a faster lerp to recover speed, slower to drop speed
        float lerpSpeed = (targetSpeed > currentBiasSpeed) ? 5f : 2f;
        currentBiasSpeed = Mathf.Lerp(currentBiasSpeed, targetSpeed, Time.deltaTime * lerpSpeed);

        // 4. Apply Bias
        float targetX = isFacingRight ? forwardBias : backwardBias;
        currentXBias = Mathf.MoveTowards(currentXBias, targetX, currentBiasSpeed * Time.deltaTime);

        UpdateComposer();
    }

    private void HandleLookDown()
    {
        if (playerRb == null || positionComposer == null) return;

        float targetOffset = 0f;
        if (GameInput.Instance != null)
        {
            if (GameInput.Instance.GetMovementInput().y < -0.5f) targetOffset = lookDownOffset;
        }
        else if (Input.GetAxisRaw("Vertical") < -0.5f) targetOffset = lookDownOffset;

        // Only apply falling offset if NOT grounded
        bool isGrounded = playerController != null && playerController.IsGrounded;
        bool fallingFast = !isGrounded && playerRb.linearVelocity.y < -12f;

        if (fallingFast) targetOffset = lookDownOffset;

        currentYOffset = Mathf.Lerp(currentYOffset, targetOffset, Time.deltaTime * lookShiftSpeed);
        positionComposer.TargetOffset = new Vector3(0, currentYOffset, 0);
    }

    private void UpdateComposer()
    {
        if (positionComposer == null) return;
        var comp = positionComposer.Composition;
        comp.ScreenPosition = new Vector2(currentXBias, screenYBias);
        positionComposer.Composition = comp;
    }

    public void SetCraneView(bool active)
    {
        isCraneView = active;
        if (active)
        {
            targetSize = 10f;
            currentXBias = 0.5f; // Center for crane view
            UpdateComposer();
        }
        else
        {
            targetSize = orthographicSize;
            // Reset to player framing
            isFacingRight = playerController != null && playerController.transform.localScale.x > 0;
            currentXBias = isFacingRight ? forwardBias : backwardBias;
        }
    }

    public void ApplyCameraSettings()
    {
        if (positionComposer == null) return;

        var comp = positionComposer.Composition;
        comp.ScreenPosition = new Vector2(currentXBias, screenYBias);
        comp.DeadZone.Size = new Vector2(0.1f, verticalDeadZone);
        comp.HardLimits.Size = new Vector2(0.8f, 0.8f);

        positionComposer.Composition = comp;
        positionComposer.Damping = new Vector3(horizontalDamping, 0f, 0f);
    }

    public void SetPlayerGrounded(bool grounded) { }

    public void Shake(float force = 1f)
    {
        if (globalImpulseSource) globalImpulseSource.GenerateImpulse(Vector3.one * force);
    }
}
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

    [Header("Zoom & Framing")]
    public float orthographicSize = 6f;

    [Header("Directional Framing")]
    public float forwardBias = -0.2f;
    public float backwardBias = 0.2f;
    public float switchThreshold = 0.1f;
    public float turnDelay = 0.5f;
    public float biasShiftSpeed = .75f;

    private float currentXBias;
    private bool isFacingRight = true;
    private float turnTimer = 0f;
    private float targetSize;

    // FLAGS
    private bool isCraneView = false;
    private bool isFumbleMode = false;

    [Header("Vertical")]
    public float verticalDamping = 0.0f;
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
        if (player) playerRb = player.GetComponent<Rigidbody2D>();

        if (virtualCamera != null)
        {
            positionComposer = virtualCamera.GetComponent<CinemachinePositionComposer>();
            currentXBias = forwardBias;
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

        if (!isCraneView && !isFumbleMode)
        {
            HandleDirectionalFraming();
            HandleLookDown();
        }
        else if (isFumbleMode)
        {
            // Center the camera during fumble for stability
            currentXBias = Mathf.MoveTowards(currentXBias, 0f, biasShiftSpeed * Time.deltaTime);
            UpdateComposer();
        }
    }

    private void HandleDirectionalFraming()
    {
        if (playerRb == null || positionComposer == null) return;

        float velocityX = playerRb.linearVelocity.x;
        bool tryingToSwitch = false;

        if (isFacingRight && velocityX < -switchThreshold)
        {
            tryingToSwitch = true;
            turnTimer += Time.deltaTime;
            if (turnTimer > turnDelay) { isFacingRight = false; turnTimer = 0f; }
        }
        else if (!isFacingRight && velocityX > switchThreshold)
        {
            tryingToSwitch = true;
            turnTimer += Time.deltaTime;
            if (turnTimer > turnDelay) { isFacingRight = true; turnTimer = 0f; }
        }

        if (!tryingToSwitch) turnTimer = 0f;

        float targetX = isFacingRight ? forwardBias : backwardBias;
        currentXBias = Mathf.MoveTowards(currentXBias, targetX, biasShiftSpeed * Time.deltaTime);

        UpdateComposer();
    }

    private void HandleLookDown()
    {
        if (playerRb == null || positionComposer == null) return;

        float targetOffset = 0f;
        // GameInput support implied or fallback to axis
        if (GameInput.Instance != null)
        {
            if (GameInput.Instance.GetMovementInput().y < -0.5f) targetOffset = lookDownOffset;
        }
        else if (Input.GetAxisRaw("Vertical") < -0.5f) targetOffset = lookDownOffset;

        bool fallingFast = playerRb.linearVelocity.y < -12f;
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
            currentXBias = -0.4f;
            UpdateComposer();
        }
        else
        {
            targetSize = orthographicSize;
            isFacingRight = true;
            currentXBias = forwardBias;
        }
    }

    // FIX: New Method for Fumble Mode
    public void SetFumbleMode(bool active)
    {
        isFumbleMode = active;
        if (active)
        {
            // Zoom out slightly and center
            targetSize = orthographicSize * 1.2f;
            // Bias will be zeroed in Update
        }
        else
        {
            targetSize = orthographicSize;
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
        positionComposer.Damping = new Vector3(horizontalDamping, verticalDamping, 0f);
    }

    public void SetPlayerGrounded(bool grounded) { }

    public void Shake(float force = 1f)
    {
        if (globalImpulseSource) globalImpulseSource.GenerateImpulse(Vector3.one * force);
    }
}
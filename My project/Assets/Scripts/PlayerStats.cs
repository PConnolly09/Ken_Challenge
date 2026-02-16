using UnityEngine;

[CreateAssetMenu(menuName = "Player Stats")]
public class PlayerStats : ScriptableObject
{
    [Header("CONFIG")]
    public LayerMask groundLayer;

    [Header("MOVEMENT (Juiced)")]
    public float maxRunSpeed = 22f;
    public float groundAccelerationTime = 0.08f;
    public float groundDecelerationTime = 0.04f; // Snappy stop

    [Header("AIR CONTROL")]
    public float airAccelMult = 0.7f;
    public float airDecelMult = 0.05f; // Preserves momentum

    [Header("JUMPING")]
    public float jumpHeight = 4.8f;
    public float timeToJumpApex = 0.32f; // Snappy jump
    public float downwardGravityMult = 2.5f; // Heavy fall
    public float jumpCutGravityMult = 4.5f; // Responsive variable jump
    public float maxFallSpeed = 30f;
    public float momentumJumpBonus = 3.5f;

    [Header("APEX MODIFIERS")]
    public float apexThreshold = 2.5f;
    public float apexGravityMult = 0.3f;
    public float apexAirAccelMult = 1.5f;

    [Header("JUICE & FEEDBACK (New)")]
    [Range(0, 0.1f)] public float landHitStop = 0.04f;
    public float camShakeOnLand = 0.3f;
    public float camShakeOnStiffArm = 1.2f;

    [Header("ASSISTS")]
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.15f;
    public float cornerCorrectionDistance = 0.6f;
    public float groundCheckRadius = 0.35f;

    // --- RESTORED ABILITY VARIABLES ---

    [Header("STIFF ARM")]
    public float stiffArmForce = 25f;
    public float stiffArmRange = 2.0f;
    public float stiffArmDuration = 0.3f;
    public float speedPushMultiplier = 1.0f;

    [Header("JUKE")]
    public float jukeDuration = 0.3f;
    public float jukeCooldown = 1f;
    public Color jukeColor = new Color(1, 1, 1, 0.5f);

    [Header("SPIN")]
    public float spinDuration = 0.4f;
    public float spinMoveForce = 12f;

    [Header("GAMEPLAY (Fumbles)")]
    public float baseFumbleChance = 0.05f;
    public float fumblePickupDelay = 1.2f;
    [Tooltip("How much speed we lose per enemy. 0.15 = 15% penalty.")]
    public float speedPenaltyPerEnemy = 0.15f;

    [Header("VISUALS")]
    public float baseRunSpeed = 10f;
    public float squashSpeed = 12f;
    public float squashStretchAmount = 0.1f;

    private void Reset()
    {
        groundLayer = -1;
    }
}
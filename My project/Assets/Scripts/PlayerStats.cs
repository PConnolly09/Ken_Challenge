using UnityEngine;

[CreateAssetMenu(menuName = "Player Stats")]
public class PlayerStats : ScriptableObject
{
    [Header("CONFIG")]
    [Tooltip("The physics layer for walls/ground")]
    public LayerMask groundLayer;

    [Header("MOVEMENT")]
    [Tooltip("Top speed on ground (Units/sec)")]
    public float maxRunSpeed = 20f;
    [Tooltip("Time to reach max speed from 0 (Lower = Snappier)")]
    public float groundAccelerationTime = 0.1f;
    [Tooltip("Time to stop from max speed (Lower = Snappier)")]
    public float groundDecelerationTime = 0.05f;
    [Tooltip("Multiplier for acceleration while airborne (0-1)")]
    public float airAccelMult = 0.5f;
    [Tooltip("Multiplier for deceleration while airborne (0-1)")]
    public float airDecelMult = 0.5f;

    [Header("JUMPING")]
    [Tooltip("Max height of jump in Unity Units (Tiles)")]
    public float jumpHeight = 3.5f;
    [Tooltip("Time to reach the apex of the jump")]
    public float timeToJumpApex = 0.35f;
    [Tooltip("Multiplier for gravity when falling (Higher = Heavier fall)")]
    public float downwardGravityMult = 1.5f;
    [Tooltip("Gravity multiplier when releasing jump button early (Variable Jump Height)")]
    public float jumpCutGravityMult = 3f;
    [Tooltip("Max falling speed (Terminal Velocity)")]
    public float maxFallSpeed = 25f;
    [Tooltip("Bonus height added based on horizontal momentum")]
    public float momentumJumpBonus = 1.5f;

    [Header("APEX MODIFIERS (Hang Time)")]
    [Tooltip("Vertical speed threshold where hang time triggers (e.g., when velocity is between -2 and 2).")]
    public float apexThreshold = 2.0f;
    [Tooltip("Gravity multiplier at apex. Lower (0.5) = floatier peak.")]
    public float apexGravityMult = 0.4f;
    [Tooltip("Air acceleration multiplier at apex. Allows player to steer better at the top of the jump.")]
    public float apexAirAccelMult = 1.5f;

    [Header("ASSISTS")]
    [Tooltip("Grace period to jump after falling off a ledge")]
    public float coyoteTime = 0.1f;
    [Tooltip("Grace period to register a jump input before hitting the ground")]
    public float jumpBufferTime = 0.15f;
    [Tooltip("Distance to nudge player around ceiling corners")]
    public float cornerCorrectionDistance = 0.5f;
    [Tooltip("Radius for ground detection circle")]
    public float groundCheckRadius = 0.3f;

    [Header("ABILITIES")]
    public float dashForce = 30f;

    [Header("Stiff Arm")]
    public float stiffArmForce = 25f;
    public float stiffArmRange = 2.0f;
    public float stiffArmDuration = 0.3f;
    [Tooltip("Extra force added to Stiff Arm based on player speed")]
    public float speedPushMultiplier = 1.0f;

    [Header("Juke")]
    public float jukeDuration = 0.3f;
    public float jukeCooldown = 1f;

    [Header("Spin")]
    public float spinDuration = 0.4f;
    public float spinMoveForce = 12f;

    [Header("GAMEPLAY (Fumbles)")]
    public float baseFumbleChance = 0.05f;
    [Tooltip("Seconds before player can pick up a fumbled ball")]
    public float fumblePickupDelay = 1.2f;

    [Header("VISUALS")]
    [Tooltip("Reference speed for animation playback speed (prevents skating effect)")]
    public float baseRunSpeed = 8f;
    public Color jukeColor = new (1, 1, 1, 0.5f);
    public float squashStretchAmount = 0.1f;
    public float squashSpeed = 10f;
}
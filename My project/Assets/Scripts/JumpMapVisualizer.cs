using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class JumpMapVisualizer : MonoBehaviour
{
    [Header("References")]
    public PlayerStats stats;
    public Tilemap groundTilemap;

    [Header("Range Settings")]
    [Tooltip("Only draw arcs for tiles within this distance of the Scene View Camera.")]
    public float drawRadius = 20f;
    [Tooltip("Skip tiles to reduce clutter. 1 = Check every tile, 2 = Check every other tile.")]
    [Range(1, 5)]
    public int tileResolution = 1;

    [Header("Simulation Settings")]
    [Tooltip("Percentage of max speed to simulate. 1.0 = Full Run, 0.5 = Half Speed.")]
    [Range(0f, 1f)]
    public float speedMultiplier = 1.0f;

    [Tooltip("Simulate having minions attached (Speed Penalty).")]
    [Range(0, 5)]
    public int simulatedMinions = 0;

    [Header("Visual Settings")]
    public bool showRunningJump = true;
    [Tooltip("Color for the upward portion of the jump (Takeoff).")]
    public Color ascentColor = new Color(0.2f, 1f, 0.2f, 0.6f); // Bright Green
    [Tooltip("Color for the downward portion of the jump (Landing).")]
    public Color descentColor = new Color(1f, 0.4f, 0.4f, 0.6f); // Reddish

    public bool showStandingJump = false;
    public Color standingJumpColor = new Color(1, 1, 0, 0.3f);

    [Range(10, 40)]
    public int steps = 25;
    [Range(0.01f, 0.1f)]
    public float timeStep = 0.05f;

    // Physics Cache
    private float _gravity;
    private float _baseJumpVelocity;

    private void OnDrawGizmos()
    {
        if (groundTilemap == null) groundTilemap = GetComponent<Tilemap>();
        if (groundTilemap == null || stats == null) return;

        CalculatePhysicsConstants();

        // Get the Scene View camera position to optimize drawing
#if UNITY_EDITOR
        if (SceneView.currentDrawingSceneView == null) return;
        Vector3 camPos = SceneView.currentDrawingSceneView.camera.transform.position;
#else
        Vector3 camPos = transform.position;
#endif
        // Optimization: Only scan bounds within range
        BoundsInt bounds = groundTilemap.cellBounds;

        // Convert world range to cell range roughly
        Vector3Int minRange = groundTilemap.WorldToCell(camPos - new Vector3(drawRadius, drawRadius, 0));
        Vector3Int maxRange = groundTilemap.WorldToCell(camPos + new Vector3(drawRadius, drawRadius, 0));

        // Clamp to actual map bounds
        int xMin = Mathf.Max(bounds.xMin, minRange.x);
        int xMax = Mathf.Min(bounds.xMax, maxRange.x);
        int yMin = Mathf.Max(bounds.yMin, minRange.y);
        int yMax = Mathf.Min(bounds.yMax, maxRange.y);

        // Calculate effective speed based on modifiers
        float penaltyFactor = Mathf.Max(0.3f, 1.0f - (simulatedMinions * stats.speedPenaltyPerEnemy));
        float effectiveSpeed = stats.maxRunSpeed * penaltyFactor * speedMultiplier;

        // Apply resolution skip
        for (int x = xMin; x <= xMax; x += tileResolution)
        {
            for (int y = yMin; y <= yMax; y += tileResolution)
            {
                Vector3Int cellPos = new Vector3Int(x, y, 0);

                // 1. Is there a block here?
                if (!groundTilemap.HasTile(cellPos)) continue;

                // 2. Is the space ABOVE it empty? (i.e., is it a surface?)
                if (groundTilemap.HasTile(cellPos + Vector3Int.up)) continue;

                // 3. Draw Trajectories
                Vector3 worldPos = groundTilemap.GetCellCenterWorld(cellPos);
                worldPos.y += 0.5f; // Top of tile

                if (showRunningJump)
                {
                    // Pass true to use ascent/descent colors
                    DrawTrajectory(worldPos, effectiveSpeed, true, Color.white);
                    DrawTrajectory(worldPos, -effectiveSpeed, true, Color.white);
                }

                if (showStandingJump)
                {
                    // Pass false to use single color
                    DrawTrajectory(worldPos, 0f, false, standingJumpColor);
                }
            }
        }
    }

    private void CalculatePhysicsConstants()
    {
        float apexTime = stats.timeToJumpApex > 0 ? stats.timeToJumpApex : 0.35f;
        _gravity = -(2 * stats.jumpHeight) / Mathf.Pow(apexTime, 2);
        _baseJumpVelocity = Mathf.Abs(_gravity) * apexTime;
    }

    private void DrawTrajectory(Vector3 startPos, float horizontalSpeed, bool usePhaseColor, Color singleColor)
    {
        Vector3 previousPos = startPos;

        float speedRatio = Mathf.Abs(horizontalSpeed) / stats.maxRunSpeed;
        float initialYVel = _baseJumpVelocity + (stats.momentumJumpBonus * speedRatio);

        Vector2 currentVelocity = new Vector2(horizontalSpeed, initialYVel);
        Vector3 currentPos = startPos;

        for (int i = 0; i < steps; i++)
        {
            float gravityMult = 1f;
            if (currentVelocity.y < 0) gravityMult = stats.downwardGravityMult;

            currentVelocity.y += _gravity * gravityMult * timeStep;
            currentVelocity.y = Mathf.Max(currentVelocity.y, -stats.maxFallSpeed);

            currentPos.x += currentVelocity.x * timeStep;
            currentPos.y += currentVelocity.y * timeStep;

            if (usePhaseColor)
            {
                // Green for Up, Red for Down
                Gizmos.color = currentVelocity.y > 0 ? ascentColor : descentColor;
            }
            else
            {
                Gizmos.color = singleColor;
            }

            Gizmos.DrawLine(previousPos, currentPos);
            previousPos = currentPos;
        }
    }
}
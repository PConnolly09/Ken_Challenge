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

    [Header("Origin Settings (Edges & Coyote Time)")]
    [Tooltip("If true, trajectories are drawn from the outer edges of the tile, not the center.")]
    public bool drawFromEdges = true;
    [Tooltip("If true, offsets the starting point out into the air based on your Coyote Time and Speed.")]
    public bool includeCoyoteTime = true;

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

        Vector3Int minRange = groundTilemap.WorldToCell(camPos - new Vector3(drawRadius, drawRadius, 0));
        Vector3Int maxRange = groundTilemap.WorldToCell(camPos + new Vector3(drawRadius, drawRadius, 0));

        int xMin = Mathf.Max(bounds.xMin, minRange.x);
        int xMax = Mathf.Min(bounds.xMax, maxRange.x);
        int yMin = Mathf.Max(bounds.yMin, minRange.y);
        int yMax = Mathf.Min(bounds.yMax, maxRange.y);

        float penaltyFactor = Mathf.Max(0.3f, 1.0f - (simulatedMinions * stats.speedPenaltyPerEnemy));
        float effectiveSpeed = stats.maxRunSpeed * penaltyFactor * speedMultiplier;

        float edgeOffset = groundTilemap.cellSize.x / 2f;
        float coyoteOffset = 0f;

        if (includeCoyoteTime && stats != null)
        {
            // Distance = Speed * Time. This is exactly how far past the edge they can get.
            coyoteOffset = effectiveSpeed * stats.coyoteTime;
        }

        for (int x = xMin; x <= xMax; x += tileResolution)
        {
            for (int y = yMin; y <= yMax; y += tileResolution)
            {
                Vector3Int cellPos = new Vector3Int(x, y, 0);

                if (!groundTilemap.HasTile(cellPos)) continue;
                if (groundTilemap.HasTile(cellPos + Vector3Int.up)) continue; // Surface check

                Vector3 centerPos = groundTilemap.GetCellCenterWorld(cellPos);
                centerPos.y += groundTilemap.cellSize.y / 2f; // Top of tile

                if (showRunningJump)
                {
                    if (drawFromEdges)
                    {
                        // Right-bound jump (Start from Right Edge + Coyote Time)
                        Vector3 rightStart = centerPos + new Vector3(edgeOffset + coyoteOffset, 0, 0);
                        DrawTrajectory(rightStart, effectiveSpeed, true, Color.white);

                        // Left-bound jump (Start from Left Edge + Coyote Time)
                        Vector3 leftStart = centerPos - new Vector3(edgeOffset + coyoteOffset, 0, 0);
                        DrawTrajectory(leftStart, -effectiveSpeed, true, Color.white);
                    }
                    else
                    {
                        DrawTrajectory(centerPos, effectiveSpeed, true, Color.white);
                        DrawTrajectory(centerPos, -effectiveSpeed, true, Color.white);
                    }
                }

                if (showStandingJump)
                {
                    if (drawFromEdges)
                    {
                        // Standing jumps don't really use coyote time distance because speed is 0
                        Vector3 rightEdge = centerPos + new Vector3(edgeOffset, 0, 0);
                        Vector3 leftEdge = centerPos - new Vector3(edgeOffset, 0, 0);
                        DrawTrajectory(rightEdge, 0f, false, standingJumpColor);
                        DrawTrajectory(leftEdge, 0f, false, standingJumpColor);
                    }
                    else
                    {
                        DrawTrajectory(centerPos, 0f, false, standingJumpColor);
                    }
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
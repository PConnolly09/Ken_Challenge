using UnityEngine;

public class BreakableWall : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Minimum speed required to break this wall.")]
    public float breakSpeedThreshold = 15f;
    public bool requiresStiffArm = false;

    [Header("Feedback")]
    public GameObject debrisPrefab;
    public AudioClip breakSfx;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<PlayerController>(out var player))
        {
            bool hasSpeed = Mathf.Abs(player.GetVelocity().x) >= breakSpeedThreshold;
            bool isAttacking = player.isStiffArming || player.isSpinning;

            if (requiresStiffArm)
            {
                if (isAttacking) Break();
            }
            else
            {
                if (hasSpeed || isAttacking) Break();
            }
        }
    }

    public void Break()
    {
        if (debrisPrefab) Instantiate(debrisPrefab, transform.position, Quaternion.identity);
        if (breakSfx) AudioSource.PlayClipAtPoint(breakSfx, transform.position); // Using static because object destroys

        if (Camera.main.TryGetComponent<CameraController>(out var cam))
        {
            cam.Shake(1.5f);
        }

        Destroy(gameObject);
    }
}
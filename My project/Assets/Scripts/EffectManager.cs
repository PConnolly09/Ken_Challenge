using UnityEngine;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;

    [Header("Movement VFX")]
    public GameObject jumpDustPrefab;
    public GameObject landDustPrefab;
    public GameObject footstepDustPrefab;
    public GameObject spinTrailPrefab;
    public GameObject jukeGhostPrefab;

    [Header("Combat VFX")]
    public GameObject bloodSplatterPrefab;
    public GameObject stiffArmImpactPrefab;
    public GameObject tackleImpactPrefab;
    public GameObject squishEffectPrefab;
    public GameObject attachPoofPrefab;

    [Header("Telegraphs (Indicators)")]
    public GameObject tackleTelegraphPrefab;
    public GameObject stripTelegraphPrefab;

    public void TackleTelegraph(Transform parent, float duration)
    {
        PlayAttachedEffect(tackleTelegraphPrefab, parent, Vector3.up * 1.5f, duration);
    }

    [Header("Package VFX")]
    public GameObject packagePickupPrefab;
    public GameObject packageDropPrefab;
    public GameObject fumbleExplosionPrefab;

    [Header("UI & Game")]
    public GameObject touchdownConfettiPrefab;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // FIX: Added duration parameter (default 3s). 
    // Usage: PlayEffect(prefab, pos, 1f, 0.5f) for short effects.
    public void PlayEffect(GameObject prefab, Vector3 position, float scale = 1f, float duration = 3f)
    {
        if (prefab == null) return;

        // Custom duration override for Blood Splatter to reduce clutter
        if (prefab == bloodSplatterPrefab) duration = 1.0f;

        Vector3 spawnPos = new Vector3(position.x, position.y, -5f);
        GameObject vfx = Instantiate(prefab, spawnPos, Quaternion.identity);
        vfx.transform.localScale *= scale;

        Destroy(vfx, duration);
    }

    public void PlayAttachedEffect(GameObject prefab, Transform parent, Vector3 offset, float duration)
    {
        if (prefab == null) return;
        GameObject vfx = Instantiate(prefab, parent.position + offset, Quaternion.identity, parent);
        Destroy(vfx, duration);
    }
}
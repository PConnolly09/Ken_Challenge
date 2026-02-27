using UnityEngine;
using UnityEngine.Pool;
using System.Collections;
using System.Collections.Generic;

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

    [Header("Package VFX")]
    public GameObject packagePickupPrefab;
    public GameObject packageDropPrefab;
    public GameObject fumbleExplosionPrefab;

    [Header("UI & Game")]
    public GameObject touchdownConfettiPrefab;

    // --- NEW: POOLING SYSTEM ---
    private Dictionary<GameObject, ObjectPool<GameObject>> effectPools = new Dictionary<GameObject, ObjectPool<GameObject>>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Retrieves an ObjectPool for a specific prefab, creating it if it doesn't exist
    private ObjectPool<GameObject> GetPoolForPrefab(GameObject prefab)
    {
        if (!effectPools.TryGetValue(prefab, out var pool))
        {
            pool = new ObjectPool<GameObject>(
                createFunc: () => Instantiate(prefab),
                actionOnGet: (obj) => obj.SetActive(true),
                actionOnRelease: (obj) => {
                    obj.SetActive(false);
                    obj.transform.SetParent(null); // Clear parent on release
                },
                actionOnDestroy: (obj) => Destroy(obj),
                collectionCheck: false,
                defaultCapacity: 10,
                maxSize: 60 // Prevents runaway memory allocation
            );
            effectPools[prefab] = pool;
        }
        return pool;
    }

    public void TackleTelegraph(Transform parent, float duration)
    {
        PlayAttachedEffect(tackleTelegraphPrefab, parent, Vector3.up * 1.5f, duration);
    }

    // UPDATED: Now utilizes ObjectPool instead of Instantiate/Destroy
    public void PlayEffect(GameObject prefab, Vector3 position, float scale = 1f, float duration = 3f)
    {
        if (prefab == null) return;

        // Custom duration override for Blood Splatter to reduce clutter
        if (prefab == bloodSplatterPrefab) duration = 1.0f;

        var pool = GetPoolForPrefab(prefab);
        GameObject vfx = pool.Get();

        vfx.transform.position = new Vector3(position.x, position.y, -5f);
        vfx.transform.rotation = Quaternion.identity;
        // Apply scaling relative to base prefab scale
        vfx.transform.localScale = prefab.transform.localScale * scale;

        StartCoroutine(ReturnToPoolRoutine(pool, vfx, duration));
    }

    // UPDATED: Now utilizes ObjectPool instead of Instantiate/Destroy
    public void PlayAttachedEffect(GameObject prefab, Transform parent, Vector3 offset, float duration)
    {
        if (prefab == null) return;

        var pool = GetPoolForPrefab(prefab);
        GameObject vfx = pool.Get();

        vfx.transform.SetParent(parent);
        vfx.transform.position = parent.position + offset;
        vfx.transform.rotation = Quaternion.identity;
        vfx.transform.localScale = prefab.transform.localScale;

        StartCoroutine(ReturnToPoolRoutine(pool, vfx, duration));
    }

    // Automatically returns the VFX object to its pool after the duration
    private IEnumerator ReturnToPoolRoutine(ObjectPool<GameObject> pool, GameObject vfx, float duration)
    {
        yield return new WaitForSeconds(duration);

        // Ensure object wasn't destroyed manually by another system
        if (vfx != null && vfx.activeSelf)
        {
            pool.Release(vfx);
        }
    }
}
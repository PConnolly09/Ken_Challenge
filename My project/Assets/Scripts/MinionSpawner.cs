using UnityEngine;
using UnityEngine.Pool;

public class MinionSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject minionPrefab;
    public int maxActiveMinions = 5;
    public float spawnInterval = 3f;
    public float spawnRadius = 1f; // Spawns directly around THIS spawner object

    [Header("Minion Patrol Route")]
    [Tooltip("Drag your scene waypoints here. The spawner will hand them to the minions when they spawn.")]
    public Transform[] patrolWaypoints;

    private int currentActive = 0;
    private float spawnTimer;

    private ObjectPool<GameObject> pool;

    void Awake()
    {
        pool = new ObjectPool<GameObject>(
            createFunc: () => {
                GameObject obj = Instantiate(minionPrefab);
                PooledMinion helper = obj.AddComponent<PooledMinion>();
                helper.spawner = this;
                return obj;
            },
            actionOnGet: (obj) => {
                obj.SetActive(true);
                currentActive++;
            },
            actionOnRelease: (obj) => {
                obj.SetActive(false);
                currentActive--;
            },
            actionOnDestroy: (obj) => Destroy(obj),
            collectionCheck: false,
            defaultCapacity: maxActiveMinions,
            maxSize: 20
        );

        spawnTimer = spawnInterval;
    }

    void Update()
    {
        if (currentActive < maxActiveMinions)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0)
            {
                SpawnMinion();
                spawnTimer = spawnInterval;
            }
        }
    }

    void SpawnMinion()
    {
        GameObject minion = pool.Get();
        // Spawn around the Spawner itself
        minion.transform.position = transform.position + (Vector3)Random.insideUnitCircle * spawnRadius;

        // Reset state and hand over the Scene Waypoints!
        if (minion.TryGetComponent<EnemyAI>(out var ai))
        {
            ai.enabled = true;
            ai.isKnockedBack = false;
            ai.carriesPackage = false;
            ai.AssignPatrolRoute(patrolWaypoints);
        }

        if (minion.TryGetComponent<Collider2D>(out var col)) col.enabled = true;
        if (minion.TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
        }
    }

    public void ReturnToPool(GameObject minion)
    {
        pool.Release(minion);
    }

    // DRAW GIZMOS FOR SPAWNER & WAYPOINT VISUALIZATION
    void OnDrawGizmos()
    {
        // Draw Spawner Area (Green)
        Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
        Gizmos.DrawSphere(transform.position, spawnRadius);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        // Draw Waypoint Path (Cyan)
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < patrolWaypoints.Length; i++)
            {
                if (patrolWaypoints[i] != null)
                {
                    Gizmos.DrawWireSphere(patrolWaypoints[i].position, 0.3f);
                    if (i > 0 && patrolWaypoints[i - 1] != null)
                    {
                        Gizmos.DrawLine(patrolWaypoints[i - 1].position, patrolWaypoints[i].position);
                    }
                }
            }

            // Draw a faded line from Spawner to the First Waypoint
            if (patrolWaypoints[0] != null)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
                Gizmos.DrawLine(transform.position, patrolWaypoints[0].position);
            }
        }
    }
}

// Helper class attached dynamically to handle callbacks to the pool
public class PooledMinion : MonoBehaviour
{
    public MinionSpawner spawner;

    public void DieAndReturnToPool()
    {
        if (spawner != null)
        {
            spawner.ReturnToPool(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
using UnityEngine;
using System.Collections.Generic;

public class MinionSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject minionPrefab;
    public int maxActiveMinions = 5;
    public float spawnInterval = 3f;
    public float spawnRadius = 1f;

    [Header("Minion Patrol Route")]
    [Tooltip("Drag your scene waypoints here. The spawner will hand them to the minions when they spawn.")]
    public Transform[] patrolWaypoints;

    private float spawnTimer;

    // FIX: Replaced strict Unity Pool with a robust manual pool that survives external Destroys (DeathZones)
    private List<GameObject> activeMinions = new List<GameObject>();
    private Queue<GameObject> inactiveMinions = new Queue<GameObject>();

    void Awake()
    {
        spawnTimer = spawnInterval;
    }

    void Update()
    {
        // Bulletproof cleanup: Removes any minions destroyed externally (like falling in a pit)
        activeMinions.RemoveAll(item => item == null);

        if (activeMinions.Count < maxActiveMinions)
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
        GameObject minion = null;

        // Try to grab from the inactive pool first
        while (inactiveMinions.Count > 0 && minion == null)
        {
            minion = inactiveMinions.Dequeue();
        }

        // If pool is empty, create a new one
        if (minion == null)
        {
            minion = Instantiate(minionPrefab);
            PooledMinion helper = minion.AddComponent<PooledMinion>();
            helper.spawner = this;
        }

        minion.transform.position = transform.position + (Vector3)Random.insideUnitCircle * spawnRadius;
        minion.SetActive(true);
        activeMinions.Add(minion);

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
        if (minion != null)
        {
            minion.SetActive(false);
            activeMinions.Remove(minion);
            inactiveMinions.Enqueue(minion);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
        Gizmos.DrawSphere(transform.position, spawnRadius);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

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

            if (patrolWaypoints[0] != null)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
                Gizmos.DrawLine(transform.position, patrolWaypoints[0].position);
            }
        }
    }
}

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
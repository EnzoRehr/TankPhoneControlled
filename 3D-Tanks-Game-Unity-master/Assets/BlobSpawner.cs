
using UnityEngine;
public class BlobSpawner : MonoBehaviour
{
    [Header("Spawning")]
    public GameObject lavaMonsterPrefab;
    [Header("Settings")]
    public float spawnDelay = 5f;
    public float checkRadius = 30f;
    private float _spawnTimer = 0f;
    private bool _waitingToSpawn = false;
    void Start()
    {
        SpawnMonster();
    }
    void Update()
    {
        if (_waitingToSpawn)
        {
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                SpawnMonster();
                _waitingToSpawn = false;
            }
            return;
        }
        if (!IsMonsterAliveNearby())
        {
            Debug.Log("Furnace: No monsters nearby, starting spawn countdown...");
            _waitingToSpawn = true;
            _spawnTimer = spawnDelay;
        }
    }
    bool IsMonsterAliveNearby()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, checkRadius);
        foreach (Collider hit in hits)
        {
            if (hit.GetComponent<LavaMonsterAI>() != null)
                return true;
        }
        return false;
    }
    void SpawnMonster()
    {
        if (lavaMonsterPrefab == null)
        {
            Debug.LogWarning("Furnace: No lava monster prefab assigned!");
            return;
        }
        Instantiate(lavaMonsterPrefab, transform.position, transform.rotation);
        Debug.Log("Furnace: Spawned lava monster!");
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, checkRadius);
    }
}
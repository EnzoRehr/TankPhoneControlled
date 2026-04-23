using UnityEngine;
using System.Collections;
using TMPro;

public class WaveManager : MonoBehaviour
{
    [System.Serializable]
    public class Wave
    {
        public string waveName;
        public GameObject[] enemyPrefabs;
        public int enemyCount;
    }

    [Header("=== WAVES ===")]
    public Wave[] waves;

    [Header("=== SPAWN PERIMETER ===")]
    public Vector3 spawnCenter = Vector3.zero;
    public float minSpawnRadius = 10f;
    public float maxSpawnRadius = 20f;

    [Header("=== SETTINGS ===")]
    public float timeBetweenWaves = 5f;
    public float timeBetweenSpawns = 0.5f;
    public string enemyTag = "Enemy";

    [Header("=== UI ===")]
    public GameObject countdownPanel;
    public TextMeshProUGUI countdownText;
    public TextMeshProUGUI waveNameText;

    public int currentWave = 0;
    private bool waitingForNextWave = false;
    private bool waveSpawned = false;

    void Start()
    {
        if (countdownPanel != null)
            countdownPanel.SetActive(false);

        Debug.LogWarning($"WaveManager Start - waves configurate: {waves.Length}");
        StartCoroutine(CountdownNextWave());
    }

    void Update()
    {
        if (waveSpawned && !waitingForNextWave)
        {
            GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
            if (enemies.Length == 0)
            {
                waitingForNextWave = true;
                waveSpawned = false;
                currentWave++;
                StartCoroutine(CountdownNextWave());
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        DrawCircle(spawnCenter, maxSpawnRadius);
        Gizmos.color = Color.yellow;
        DrawCircle(spawnCenter, minSpawnRadius);
    }

    void DrawCircle(Vector3 center, float radius)
    {
        int segments = 36;
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(
                Mathf.Cos(angle) * radius, 0,
                Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }

    IEnumerator CountdownNextWave()
    {
        if (currentWave >= waves.Length)
        {
            Debug.LogWarning("Toate wave-urile terminate! Nivel complet!");
            yield break;
        }

        if (countdownPanel != null)
            countdownPanel.SetActive(true);

        if (waveNameText != null)
            waveNameText.text = waves[currentWave].waveName;

        for (int i = (int)timeBetweenWaves; i > 0; i--)
        {
            if (countdownText != null)
                countdownText.text = i.ToString();

            Debug.LogWarning($"Wave urmator in: {i}...");
            yield return new WaitForSeconds(1f);
        }

        if (countdownPanel != null)
            countdownPanel.SetActive(false);

        StartCoroutine(StartWave());
    }

    IEnumerator StartWave()
    {
        if (currentWave >= waves.Length)
        {
            Debug.LogWarning("Toate wave-urile terminate! Nivel complet!");
            yield break;
        }

        Wave wave = waves[currentWave];
        waitingForNextWave = false;
        waveSpawned = false;

        Debug.LogWarning($"Wave {currentWave + 1} incepe: {wave.waveName}");

        NetworkInputBridge.BroadcastWaveStart();

        for (int i = 0; i < wave.enemyCount; i++)
        {
            SpawnEnemy(wave);
            yield return new WaitForSeconds(timeBetweenSpawns);
        }

        waveSpawned = true;
    }

    void SpawnEnemy(Wave wave)
    {
        if (wave.enemyPrefabs.Length == 0)
        {
            Debug.LogWarning("EROARE: Nu are prefaburi in wave!");
            return;
        }

        GameObject prefab = wave.enemyPrefabs[Random.Range(0, wave.enemyPrefabs.Length)];

        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = Random.Range(minSpawnRadius, maxSpawnRadius);

        Vector3 randomPos = new Vector3(
            spawnCenter.x + Mathf.Cos(angle) * distance,
            spawnCenter.y,
            spawnCenter.z + Mathf.Sin(angle) * distance
        );

        Instantiate(prefab, randomPos, Quaternion.identity);
        Debug.LogWarning($"Spawnat: {prefab.name} la {randomPos}");
    }
}
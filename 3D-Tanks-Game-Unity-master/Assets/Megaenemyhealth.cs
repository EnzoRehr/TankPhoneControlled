using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// MegaEnemyHealth — attach to any enemy tank.
///
/// SETUP:
/// 1. Attach this script to your enemy root GameObject.
/// 2. Make sure the enemy is on the layer set as 'enemyLayer' in MegaTankShooter.
/// 3. Adjust Max Health, death behaviour and optional world-space health bar in Inspector.
/// </summary>
public class MegaEnemyHealth : MonoBehaviour
{
    [Header("=== HEALTH ===")]
    [Range(10f, 2000f)] public float maxHealth = 100f;

    [Header("=== DEATH ===")]
    [Tooltip("Optional explosion/death VFX prefab spawned on death.")]
    public GameObject deathVFXPrefab;

    [Tooltip("Seconds before the GameObject is destroyed after death. 0 = instant.")]
    [Range(0f, 10f)] public float destroyDelay = 2f;

    [Tooltip("Log a message to the console on death.")]
    public bool logOnDeath = true;

    [Header("=== REGENERATION (optional) ===")]
    [Range(0f, 50f)] public float regenPerSecond = 0f;
    [Range(0f, 10f)] public float regenDelay = 5f;

    [Header("=== WORLD HEALTH BAR ===")]
    [Tooltip("Show a floating health bar above the enemy.")]
    public bool showHealthBar = true;

    [Tooltip("How high above the pivot the health bar floats.")]
    public float healthBarHeight = 2.5f;

    // ?? Events ???????????????????????????????????????????????????????
    public System.Action<float, float> OnHealthChanged; // (current, max)
    public System.Action OnDeath;

    // ?? Runtime ??????????????????????????????????????????????????????
    [HideInInspector] public float currentHealth;
    [HideInInspector] public bool isDead = false;

    private float regenTimer = 0f;

    // ?????????????????????????????????????????????????????????????????
    void Awake()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (isDead || regenPerSecond <= 0f) return;

        regenTimer -= Time.deltaTime;
        if (regenTimer <= 0f && currentHealth < maxHealth)
            Heal(regenPerSecond * Time.deltaTime);
    }

    // ?? Public API ????????????????????????????????????????????????????
    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        regenTimer = regenDelay;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public float HealthFraction => currentHealth / maxHealth;

    // ?? Death ?????????????????????????????????????????????????????????
    void Die()
    {
        if (isDead) return;
        isDead = true;

        if (logOnDeath)
            Debug.Log($"[MegaEnemyHealth] {gameObject.name} destroyed!");

        OnDeath?.Invoke();

        if (deathVFXPrefab != null)
        {
            GameObject vfx = Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 2f);
        }
        Destroy(gameObject);
    }

    // ?? Floating world-space health bar ??????????????????????????????
    void OnGUI()
    {
        if (!showHealthBar || isDead || Camera.main == null) return;

        Vector3 worldPos = transform.position + Vector3.up * healthBarHeight;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

        // Don't draw if behind camera
        if (screenPos.z < 0f) return;

        float barW = 80f;
        float barH = 10f;
        float x = screenPos.x - barW * 0.5f;
        float y = Screen.height - screenPos.y - barH * 0.5f;

        // Background
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(x - 1, y - 1, barW + 2, barH + 2), Texture2D.whiteTexture);

        // Health fill — red when low, green when full
        GUI.color = Color.Lerp(Color.red, Color.green, HealthFraction);
        GUI.DrawTexture(new Rect(x, y, barW * HealthFraction, barH), Texture2D.whiteTexture);

        // HP text
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y - 14f, barW, 14f),
                  $"{Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(maxHealth)}");
    }
}
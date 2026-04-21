using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// MegaTankHealth — attach to any tank (player or enemy) to give it health.
/// 
/// The MegaTankShooter script will look for this component on enemies it hits
/// and call TakeDamage() automatically.
/// 
/// You can adjust all parameters in the Inspector.
/// </summary>
public class MegaTankHealth : MonoBehaviour
{
    [Header("=== HEALTH SETTINGS ===")]
    [Tooltip("Maximum health points.")]
    [Range(10f, 2000f)]
    public float maxHealth = 100f;

    [Tooltip("If true, print a death message to the console (handy for testing).")]
    public bool logOnDeath = true;

    [Tooltip("Optionally destroy the GameObject this many seconds after death (0 = don't destroy).")]
    [Range(0f, 10f)]
    public float destroyDelay = 3f;

    [Header("=== REGENERATION (optional) ===")]
    [Tooltip("Health regenerated per second. 0 = off.")]
    [Range(0f, 50f)]
    public float regenPerSecond = 0f;

    [Tooltip("Delay in seconds after taking damage before regen kicks in.")]
    [Range(0f, 10f)]
    public float regenDelay = 5f;

    // ?? Runtime ??????????????????????????????????????????????????????
    [HideInInspector] public float currentHealth;
    [HideInInspector] public bool isDead = false;

    private float regenTimer = 0f;

    // ?? Events you can subscribe to from other scripts ???????????????
    public System.Action<float, float> OnHealthChanged;  // (current, max)
    public System.Action OnDeath;

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
        {
            Heal(regenPerSecond * Time.deltaTime);
        }
    }

    // ?? Public API ????????????????????????????????????????????????????
    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        regenTimer = regenDelay;

        Debug.Log($"[MegaTankHealth] Took {amount} damage! HP: {currentHealth}/{maxHealth}");

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

    public void SetHealth(float value)
    {
        currentHealth = Mathf.Clamp(value, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth <= 0f) Die();
    }

    // Returns health as 0–1 fraction (useful for health bar UI)
    public float HealthFraction => currentHealth / maxHealth;

    // ?? Death ?????????????????????????????????????????????????????????
    void Die()
    {
        if (isDead) return;
        isDead = true;

        if (logOnDeath)
            Debug.Log($"[MegaTankHealth] {gameObject.name} has been destroyed!");

        OnDeath?.Invoke();

        // Add your explosion VFX / audio / ragdoll here:
        // Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        if (destroyDelay > 0f)
            Destroy(gameObject, destroyDelay);
    }

    // ?? Optional: simple world-space health bar using OnGUI ??????????
    // Uncomment the block below if you want floating health bars over enemies.

    void OnGUI()
    {
        if (Camera.main == null) return;

        // Screen bottom center bar
        float barW = 200f, barH = 20f;
        float x = Screen.width * 0.5f - barW * 0.5f;
        float y = Screen.height - 40f;

        // Background
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(x - 1, y - 1, barW + 2, barH + 2), Texture2D.whiteTexture);

        // Health fill
        GUI.color = Color.Lerp(Color.red, Color.green, HealthFraction);
        GUI.DrawTexture(new Rect(x, y, barW * HealthFraction, barH), Texture2D.whiteTexture);

        // Text
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y - 20f, barW, 20f),
            $"HP  {Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}");
    }

}
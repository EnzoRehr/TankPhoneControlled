
using UnityEngine;

/// <summary>
/// Attach to your enemy fireball prefab.
/// - Explodes on contact with anything
/// - Explodes after 4 seconds if nothing hit
/// - Deals damage to the player via MegaTankHealth
/// </summary>
public class EnemyFireball : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float explosionRadius = 3f;
    public float damage = 25f;
    public float lifetime = 4f;

    [Header("VFX (optional)")]
    public GameObject explosionEffectPrefab;
    public float explosionVFXDuration = 3f;

    private bool _exploded = false;
    private bool _armed = false;

    private void Start()
    {
        Invoke(nameof(Arm), 0.15f);
        Invoke(nameof(Explode), lifetime);
    }

    private void Arm() => _armed = true;

    private void OnCollisionEnter(Collision collision)
    {
        if (!_armed) return;
        if (ShouldIgnore(collision.gameObject)) return;
        Explode();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_armed) return;
        if (ShouldIgnore(other.gameObject)) return;
        Explode();
    }

    private bool ShouldIgnore(GameObject go)
    {
        // Don't trigger on other fireballs or enemy tanks
        if (go.GetComponent<EnemyFireball>() != null) return true;
        if (go.GetComponent<MegaEnemyHealth>() != null) return true;
        return false;
    }

    private void Explode()
    {
        if (_exploded) return;
        _exploded = true;

        CancelInvoke(nameof(Explode));

        // Spawn and auto-destroy explosion VFX
        if (explosionEffectPrefab != null)
        {
            GameObject vfx = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, explosionVFXDuration);
        }

        // Damage player in radius
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider hit in hits)
        {
            MegaTankHealth playerHealth = hit.GetComponent<MegaTankHealth>();
            if (playerHealth != null)
            {
                float distance = Vector3.Distance(transform.position, hit.transform.position);
                float falloff = 1f - Mathf.Clamp01(distance / explosionRadius);
                float actualDamage = damage * falloff;
                playerHealth.TakeDamage(actualDamage);
                Debug.Log($"[EnemyFireball] Hit player for {actualDamage:F1} damage.");
            }
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
        Gizmos.DrawSphere(transform.position, explosionRadius);
    }
}
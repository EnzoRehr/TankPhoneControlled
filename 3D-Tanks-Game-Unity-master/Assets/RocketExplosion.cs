
using UnityEngine;

/// <summary>
/// Attach to your rocket prefab.
/// - Explodes on contact with anything except the Player and other Rockets
/// - Explodes after 4 seconds if nothing hit
/// - Deals damage to anything with MegaEnemyHealth in explosion radius
/// </summary>
public class Rocket : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float explosionRadius = 5f;
    public float damage = 50f;
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
        // Ignore the player tank and other rockets
        if (go.CompareTag("Player")) return true;
        if (go.GetComponent<Rocket>() != null) return true;
        return false;
    }

    private void Explode()
    {
        if (_exploded) return;
        _exploded = true;

        CancelInvoke(nameof(Explode)); // only cancel the lifetime invoke

        // Spawn and auto-destroy explosion VFX
        if (explosionEffectPrefab != null)
        {
            GameObject vfx = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, explosionVFXDuration);
        }

        // Damage enemies in radius — skip other rockets
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider hit in hits)
        {
            if (hit.GetComponent<Rocket>() != null) continue; // skip other rockets

            MegaEnemyHealth health = hit.GetComponent<MegaEnemyHealth>();
            if (health != null)
            {
                float distance = Vector3.Distance(transform.position, hit.transform.position);
                float falloff = 1f - Mathf.Clamp01(distance / explosionRadius);
                float actualDamage = damage * falloff;
                health.TakeDamage(actualDamage);
                Debug.Log($"[Rocket] Hit {hit.gameObject.name} for {actualDamage:F1} damage.");
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
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Debug = UnityEngine.Debug;

public class LavaMonsterAI : MonoBehaviour
{
    [Header("Detection")]
    public float detectionRange = 20f;
    public float explodeRange = 4f;

    [Header("Hopping")]
    public float hopSpeed = 6f;
    public float hopInterval = 1.2f;

    [Header("Explosion")]
    public float explosionDamage = 50f;
    public float explosionRadius = 6f;
    public GameObject explosionVFXPrefab;

    [Header("Animation")]
    public string inflateAnimTrigger = "Inflate";

    private enum State { Idle, Hopping, Inflating, Exploded }
    private State _state = State.Idle;

    private Transform _player;
    private NavMeshAgent _agent;
    private Animator _animator;
    private Rigidbody _rb;

    private float _hopTimer = 0f;
    private bool _isHopping = false;
    private bool _readyToMove = false;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (_agent == null)
        {
            Debug.LogError("[LavaMonster] NavMeshAgent component missing!");
            return;
        }

        // Keep agent disabled until we land on the navmesh
        _agent.enabled = false;

        _rb = GetComponent<Rigidbody>();
        if (_rb != null)
        {
            _rb.freezeRotation = true;
            _rb.useGravity = true;
            _rb.isKinematic = false;
        }

        _animator = GetComponent<Animator>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            _player = playerObj.transform;
        else
            Debug.LogWarning("[LavaMonster] No GameObject tagged 'Player' found!");

        StartCoroutine(WaitForNavMesh());
    }

    private IEnumerator WaitForNavMesh()
    {
        Debug.Log("[LavaMonster] Waiting to land before activating NavMesh...");

        // Wait until close to the ground
        float timeout = 5f;
        while (timeout > 0f)
        {
            timeout -= Time.deltaTime;

            // Check if we're near the ground via raycast
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1.5f))
            {
                // Snap to ground
                transform.position = hit.point;

                // Disable rigidbody and hand control to NavMeshAgent
                if (_rb != null)
                {
                    _rb.velocity = Vector3.zero;
                    _rb.isKinematic = true;
                }

                // Enable agent now that we're on the ground
                _agent.enabled = true;
                _agent.speed = hopSpeed;
                _agent.angularSpeed = 0f;
                _agent.updateRotation = false;
                _agent.acceleration = 999f;
                _agent.isStopped = true;

                // Give it a frame to register
                yield return null;

                if (_agent.isOnNavMesh)
                {
                    Debug.Log("[LavaMonster] Landed and placed on NavMesh successfully.");
                    _readyToMove = true;
                    yield break;
                }
                else
                {
                    // Not on navmesh yet, disable and keep waiting
                    _agent.enabled = false;
                    if (_rb != null) _rb.isKinematic = false;
                }
            }

            yield return null;
        }

        Debug.LogError("[LavaMonster] Could not place agent on NavMesh after landing!");
        enabled = false;
    }

    void Update()
    {
        if (!_readyToMove) return;
        if (_player == null || _state == State.Exploded) return;
        if (_state == State.Inflating) return;

        float dist = Vector3.Distance(transform.position, _player.position);

        FacePlayer();

        if (_state == State.Idle && dist <= detectionRange)
        {
            Debug.Log("[LavaMonster] Player detected!");
            _state = State.Hopping;
            _agent.isStopped = false;
        }

        if (_state == State.Hopping)
        {
            if (dist <= explodeRange)
            {
                Debug.Log("[LavaMonster] Close enough — inflating!");
                EnterInflating();
                return;
            }

            // Continuously update destination so it tracks the player
            if (_agent.isOnNavMesh)
                _agent.SetDestination(_player.position);

            // Visual hop squish/stretch on a timer
            _hopTimer -= Time.deltaTime;
            if (_hopTimer <= 0f && !_isHopping)
            {
                StartCoroutine(HopSquishRoutine());
                _hopTimer = hopInterval;
            }
        }
    }

    private IEnumerator HopSquishRoutine()
    {
        _isHopping = true;

        Vector3 baseScale = transform.localScale;
        Vector3 squishScale = new Vector3(baseScale.x * 1.2f, baseScale.y * 0.7f, baseScale.z * 1.2f);
        Vector3 stretchScale = new Vector3(baseScale.x * 0.8f, baseScale.y * 1.4f, baseScale.z * 0.8f);

        // Squish down
        float elapsed = 0f;
        while (elapsed < 0.1f)
        {
            transform.localScale = Vector3.Lerp(baseScale, squishScale, elapsed / 0.1f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Stretch up
        elapsed = 0f;
        while (elapsed < 0.15f)
        {
            transform.localScale = Vector3.Lerp(squishScale, stretchScale, elapsed / 0.15f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Squish on landing
        elapsed = 0f;
        while (elapsed < 0.15f)
        {
            transform.localScale = Vector3.Lerp(stretchScale, squishScale, elapsed / 0.15f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Return to base
        elapsed = 0f;
        while (elapsed < 0.1f)
        {
            transform.localScale = Vector3.Lerp(squishScale, baseScale, elapsed / 0.1f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = baseScale;
        _isHopping = false;
    }

    void EnterInflating()
    {
        _state = State.Inflating;

        if (_agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            _agent.ResetPath();
        }

        if (_animator != null)
        {
            _animator.enabled = true;
            _animator.applyRootMotion = false;
            _animator.SetTrigger(inflateAnimTrigger);
        }
        else
        {
            Explode();
        }
    }

    // Call this from an Animation Event at the end of the inflate animation
    public void OnInflateComplete()
    {
        Debug.Log("[LavaMonster] Inflate complete — exploding!");
        Explode();
    }

    void FacePlayer()
    {
        Vector3 dir = _player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.LookRotation(dir);
    }

    void Explode()
    {
        if (_state == State.Exploded) return;
        _state = State.Exploded;

        Debug.Log("[LavaMonster] EXPLODING!");

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider hit in hits)
        {
            if (hit.transform == transform) continue;

            MegaEnemyHealth megaEnemyHealth = hit.GetComponent<MegaEnemyHealth>();
            if (megaEnemyHealth != null)
                megaEnemyHealth.TakeDamage(explosionDamage);

            MegaTankHealth megaTankHealth = hit.GetComponent<MegaTankHealth>();
            if (megaTankHealth != null)
                megaTankHealth.TakeDamage(explosionDamage);
        }

        if (explosionVFXPrefab != null)
        {
            GameObject vfx = Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explodeRange);
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, explosionRadius);
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class BossAI : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GameObject fireballPrefab;
    public Transform firePoint;
    public Animator animator;

    [Header("Movement")]
    public float circleRadius = 20f;
    public float moveSpeed = 6f;
    public float stoppingDistance = 2f;

    [Header("Backpedal")]
    public float backpedalDistance = 8f;
    public float backpedalSpeed = 4f;
    public float backpedalHoldTime = 2f;
    public float backpedalCooldown = 10f;

    [Header("Attack")]
    public int fireballCount = 5;
    public float delayBetweenFireballs = 0.5f;
    public float attackCooldown = 2f;
    public float fireballArcHeight = 5f;

    [Header("Rotation")]
    public float rotationSpeed = 10f;

    [Header("Tracking Delay")]
    [Tooltip("How many seconds behind the boss tracks the player. 0 = instant.")]
    public float trackingDelay = 1.2f;

    [Header("Animation")]
    [Tooltip("Agent velocity threshold to consider the boss as walking.")]
    public float walkThreshold = 0.1f;

    private static readonly string AnimIsWalking = "IsWalking";
    private static readonly string AnimIsTooClose = "IsTooClose";
    private static readonly string AnimIsDead = "IsDead";

    private readonly List<(float time, Vector3 pos)> _posBuffer = new();
    private float _backpedalCooldownUntil = 0f;
    private bool _isBackpedaling = false;

    private MegaEnemyHealth _health;
    private NavMeshAgent _agent;
    private bool _running = false;

    private void Start()
    {
        _health = GetComponent<MegaEnemyHealth>();
        _agent = GetComponent<NavMeshAgent>();

        if (_agent != null)
        {
            _agent.speed = moveSpeed;
            _agent.angularSpeed = 0f;
            _agent.acceleration = 999f;
            _agent.stoppingDistance = stoppingDistance;
            _agent.updateRotation = false;
        }
        else
        {
            Debug.LogError("[BossAI] NavMeshAgent component not found on Enemy1!");
        }

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            else Debug.LogError("[BossAI] Player NOT found!");
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (!_running)
        {
            _running = true;
            StartCoroutine(BossLoop());
        }
    }

    private void Update()
    {
        if (player == null) return;

        // Record player position for delay buffer
        _posBuffer.Add((Time.time, player.position));
        float cutoff = Time.time - trackingDelay - 0.5f;
        while (_posBuffer.Count > 1 && _posBuffer[0].time < cutoff)
            _posBuffer.RemoveAt(0);

        // Drive walking animation from actual agent velocity every frame
        // so it never lags behind or gets out of sync
        if (!_isBackpedaling && _agent != null && _agent.enabled)
        {
            bool isMoving = _agent.velocity.sqrMagnitude > walkThreshold * walkThreshold;
            SetAnimBool(AnimIsWalking, isMoving);

            // Face movement direction when walking
            if (isMoving)
                SnapRotateToward(_agent.velocity.normalized);
        }
    }

    private Vector3 GetDelayedPlayerPos()
    {
        if (player == null) return transform.position;
        if (_posBuffer.Count == 0) return player.position;

        float targetTime = Time.time - trackingDelay;

        if (_posBuffer[0].time >= targetTime)
            return _posBuffer[0].pos;

        for (int i = 1; i < _posBuffer.Count; i++)
        {
            if (_posBuffer[i].time >= targetTime)
            {
                float span = _posBuffer[i].time - _posBuffer[i - 1].time;
                float t = span > 0f
                    ? (targetTime - _posBuffer[i - 1].time) / span
                    : 0f;
                return Vector3.Lerp(_posBuffer[i - 1].pos, _posBuffer[i].pos, t);
            }
        }

        return _posBuffer[^1].pos;
    }

    private IEnumerator BossLoop()
    {
        Debug.Log("[BossAI] BossLoop started.");

        while (true)
        {
            if (IsDead()) yield break;
            if (player == null) yield break;

            float distToPlayer = GetXZDistance(transform.position, player.position);
            if (distToPlayer < backpedalDistance && Time.time >= _backpedalCooldownUntil)
            {
                yield return StartCoroutine(BackpedalRoutine());
                continue;
            }

            // Pick destination and let NavMesh path to it
            Vector3 destination = PickCircleSpot(GetDelayedPlayerPos());
            Debug.Log("[BossAI] Moving to: " + destination);

            SetAgentMovement(true, moveSpeed);
            _agent.SetDestination(destination);

            // Wait until agent arrives
            while (true)
            {
                if (IsDead()) yield break;

                if (GetXZDistance(transform.position, player.position) < backpedalDistance
                    && Time.time >= _backpedalCooldownUntil)
                    break;

                if (!_agent.pathPending && _agent.remainingDistance <= stoppingDistance)
                    break;

                yield return null;
            }

            SetAgentMovement(false, 0f);

            yield return new WaitForSeconds(0.2f);

            // Face delayed player position before firing
            float faceTimer = 1f;
            while (faceTimer > 0f)
            {
                if (IsDead()) yield break;
                FacePosition(GetDelayedPlayerPos());
                faceTimer -= Time.deltaTime;
                yield return null;
            }

            Debug.Log("[BossAI] Firing volley.");
            for (int i = 0; i < fireballCount; i++)
            {
                if (IsDead()) yield break;
                FacePosition(GetDelayedPlayerPos());
                ThrowFireball(GetDelayedPlayerPos());
                Debug.Log($"[BossAI] Fired {i + 1}/{fireballCount}");
                yield return new WaitForSeconds(delayBetweenFireballs);
            }

            Debug.Log("[BossAI] Volley done. Cooling down.");
            yield return new WaitForSeconds(attackCooldown);
        }
    }

    private IEnumerator BackpedalRoutine()
    {
        Debug.Log("[BossAI] Backpedal triggered.");
        _isBackpedaling = true;
        SetAnimBool(AnimIsWalking, false);
        SetAnimBool(AnimIsTooClose, true);

        SetAgentMovement(false, 0f);

        float timer = backpedalHoldTime;
        while (timer > 0f)
        {
            if (IsDead()) yield break;

            Vector3 awayDir = new Vector3(
                transform.position.x - player.position.x, 0f,
                transform.position.z - player.position.z).normalized;

            // Face toward player so back faces escape direction
            SnapRotateToward(-awayDir);

            Vector3 newPos = transform.position + awayDir * backpedalSpeed * Time.fixedDeltaTime;
            _agent.Warp(newPos);

            timer -= Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        SetAnimBool(AnimIsTooClose, false);
        _isBackpedaling = false;

        SetAgentMovement(true, moveSpeed);

        _backpedalCooldownUntil = Time.time + backpedalCooldown;
        Debug.Log($"[BossAI] Backpedal cooldown until {_backpedalCooldownUntil:F1}s");
    }

    private void SetAgentMovement(bool enabled, float speed)
    {
        if (_agent == null) return;
        _agent.speed = speed;
        if (enabled)
        {
            _agent.isStopped = false;
        }
        else
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            _agent.ResetPath();
        }
    }

    public void OnBossDied()
    {
        SetAnimBool(AnimIsWalking, false);
        SetAnimBool(AnimIsTooClose, false);
        SetAnimBool(AnimIsDead, true);
        if (_agent != null)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }
    }

    private bool IsDead() => _health != null && _health.isDead;

    private float GetXZDistance(Vector3 a, Vector3 b)
        => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    private void FacePosition(Vector3 pos)
    {
        Vector3 dir = (pos - transform.position).normalized;
        dir.y = 0f;
        SnapRotateToward(dir);
    }

    private Vector3 PickCircleSpot(Vector3 anchor)
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * circleRadius;
        Vector3 spot = anchor + offset;
        spot.y = transform.position.y;
        return spot;
    }

    private void SnapRotateToward(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        Quaternion target = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, -90f, 0f);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, target, rotationSpeed * 60f * Time.deltaTime);
    }

    private void ThrowFireball(Vector3 targetPos)
    {
        if (fireballPrefab == null) { Debug.LogError("[BossAI] No fireball prefab!"); return; }

        Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.5f;
        GameObject fb = Instantiate(fireballPrefab, origin, Quaternion.identity);

        Rigidbody rb = fb.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 velocity = CalculateArcVelocity(origin, targetPos, fireballArcHeight);
            rb.velocity = velocity;
            if (velocity.sqrMagnitude > 0.01f)
                fb.transform.rotation = Quaternion.LookRotation(velocity);
        }
    }

    private Vector3 CalculateArcVelocity(Vector3 start, Vector3 end, float arcHeight)
    {
        float gravity = Mathf.Abs(Physics.gravity.y);
        float h = Mathf.Max(arcHeight, 0.5f);
        float timeToPeak = Mathf.Sqrt(2f * h / gravity);
        float heightDiff = end.y - start.y;
        float timeToLand = timeToPeak + Mathf.Sqrt(Mathf.Max(0.001f, 2f * (h - heightDiff) / gravity));
        Vector3 velocity = (end - start) / timeToLand;
        velocity.y = Mathf.Sqrt(2f * gravity * h);
        return velocity;
    }

    private void SetAnimBool(string param, bool value)
    {
        if (animator != null) animator.SetBool(param, value);
        else Debug.LogError("[BossAI] Animator not assigned!");
    }

    private void OnDrawGizmosSelected()
    {
        if (player == null) return;
        int seg = 64;

        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Vector3 prev = player.position + new Vector3(circleRadius, 0f, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float a = i * 360f / seg * Mathf.Deg2Rad;
            Vector3 next = player.position + new Vector3(
                Mathf.Cos(a) * circleRadius, 0f, Mathf.Sin(a) * circleRadius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }

        Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
        prev = player.position + new Vector3(backpedalDistance, 0f, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float a = i * 360f / seg * Mathf.Deg2Rad;
            Vector3 next = player.position + new Vector3(
                Mathf.Cos(a) * backpedalDistance, 0f, Mathf.Sin(a) * backpedalDistance);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
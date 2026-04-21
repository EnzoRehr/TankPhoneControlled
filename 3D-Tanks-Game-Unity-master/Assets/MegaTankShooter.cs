using System.Collections;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class MegaTankShooter : MonoBehaviour
{
    [Header("=== GUN BARRELS ===")]
    public Transform[] gunBarrels;

    [Header("=== GATLING ANIMATORS ===")]
    public Animator[] gattlingAnimators;

    [Header("=== SHOOTING PARAMETERS ===")]
    [Range(1f, 30f)] public float fireRate = 8f;
    [Range(10f, 2000f)] public float maxRange = 500f;
    [Range(1f, 100f)] public float damagePerBullet = 10f;
    [Range(0.02f, 0.5f)] public float trailDuration = 0.08f;

    [Header("=== BULLET TRAIL ===")]
    public GameObject bulletTrailPrefab;
    public Color trailColor = new Color(1f, 0.9f, 0.3f, 1f);
    [Range(0.005f, 0.1f)] public float trailWidth = 0.02f;

    [Header("=== TARGETING SPHERE ===")]
    [Range(0.1f, 2f)] public float targetSphereRadius = 0.4f;
    public Color targetColorTerrain = Color.yellow;
    public Color targetColorEnemy = Color.red;

    [Header("=== AIM BLEND ===")]
    [Tooltip("0 = pure camera direction, 1 = pure barrel direction")]
    [Range(0f, 1f)] public float barrelInfluence = 0.5f;

    [Header("=== LAYER MASKS ===")]
    public LayerMask enemyLayer;
    public LayerMask terrainLayer;

    // ?? Runtime ??????????????????????????????????????????????????????
    private MegaTankHealth _health;
    private float[] barrelCooldowns;
    private int nextBarrelIndex = 0;

    private GameObject targetSphere;
    private Renderer targetSphereRenderer;
    private MaterialPropertyBlock mpb;

   
    private int shootMask;

    private Vector3 currentAimPoint;
    private bool aimPointIsEnemy;

    // ?????????????????????????????????????????????????????????????????
    void Start()
    {
        _health = GetComponent<MegaTankHealth>();
        if (_health == null)
            Debug.LogError("MegaTankShooter: No MegaTankHealth component found on " + gameObject.name);

        shootMask = ~LayerMask.GetMask("Players");

        if (gunBarrels != null && gunBarrels.Length > 0)
            barrelCooldowns = new float[gunBarrels.Length];
        else
            Debug.LogWarning("[MegaTankShooter] No gun barrels assigned!");

        BuildTargetSphere();
        SetGattlingSpinning(false);
    }

    void BuildTargetSphere()
    {
        targetSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        targetSphere.name = "TargetingSphere";
        targetSphere.transform.localScale = Vector3.one * targetSphereRadius * 2f;
        Destroy(targetSphere.GetComponent<Collider>());

        targetSphereRenderer = targetSphere.GetComponent<Renderer>();

        Shader shader =
            Shader.Find("Universal Render Pipeline/Lit") ??
            Shader.Find("Legacy Shaders/Transparent/Diffuse") ??
            Shader.Find("Transparent/Diffuse") ??
            Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetFloat("_Mode", 3f);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        targetSphereRenderer.material = mat;
        mpb = new MaterialPropertyBlock();
        SetTargetSphereColor(targetColorTerrain);
    }

    // ?????????????????????????????????????????????????????????????????
    void Update()
    {
        if (_health == null || _health.isDead) return;

        UpdateBarrelCooldowns();
        UpdateAimPoint();
        UpdateTargetingSphere();

        // Accept both mouse and remote fire input
        bool holdingFire = Input.GetMouseButton(0) || NetworkInputBridge.RemoteFireHeld;
        SetGattlingSpinning(holdingFire);

        if (holdingFire)
            TryFire();
    }

    void SetGattlingSpinning(bool spinning)
    {
        if (gattlingAnimators == null) return;
        foreach (Animator anim in gattlingAnimators)
            if (anim != null)
                anim.enabled = spinning;
    }

    void UpdateAimPoint()
    {
        if (gunBarrels == null || gunBarrels.Length == 0 || Camera.main == null)
        {
            currentAimPoint = transform.position + transform.forward * maxRange;
            aimPointIsEnemy = false;
            return;
        }

        Vector3 avgBarrelDir = Vector3.zero;
        foreach (Transform b in gunBarrels)
            avgBarrelDir += b.forward;
        avgBarrelDir = avgBarrelDir.normalized;

        Vector3 camDir = Camera.main.transform.forward;
        Vector3 blendDir = (avgBarrelDir * barrelInfluence + camDir * (1f - barrelInfluence)).normalized;

        Vector3 camOrigin = Camera.main.transform.position;

        if (Physics.Raycast(camOrigin, blendDir, out RaycastHit hit, maxRange, shootMask))
        {
            currentAimPoint = hit.point;
            aimPointIsEnemy = ((1 << hit.collider.gameObject.layer) & enemyLayer) != 0;
        }
        else
        {
            currentAimPoint = camOrigin + blendDir * maxRange;
            aimPointIsEnemy = false;
        }
    }

    void UpdateTargetingSphere()
    {
        targetSphere.transform.position = currentAimPoint;
        SetTargetSphereColor(aimPointIsEnemy ? targetColorEnemy : targetColorTerrain);
        targetSphere.SetActive(true);
    }

    void SetTargetSphereColor(Color c)
    {
        c.a = 0.6f;
        mpb.SetColor("_BaseColor", c);
        mpb.SetColor("_Color", c);
        targetSphereRenderer.SetPropertyBlock(mpb);
    }

    void UpdateBarrelCooldowns()
    {
        if (barrelCooldowns == null) return;
        for (int i = 0; i < barrelCooldowns.Length; i++)
            barrelCooldowns[i] = Mathf.Max(0f, barrelCooldowns[i] - Time.deltaTime);
    }

    void TryFire()
    {
        if (gunBarrels == null || gunBarrels.Length == 0) return;
        float interval = 1f / fireRate;

        for (int attempt = 0; attempt < gunBarrels.Length; attempt++)
        {
            int idx = (nextBarrelIndex + attempt) % gunBarrels.Length;
            if (barrelCooldowns[idx] <= 0f)
            {
                FireFromBarrel(idx);
                barrelCooldowns[idx] = interval * gunBarrels.Length;
                nextBarrelIndex = (idx + 1) % gunBarrels.Length;
                break;
            }
        }
    }

    void FireFromBarrel(int barrelIndex)
    {
        Transform barrel = gunBarrels[barrelIndex];
        Vector3 origin = barrel.position + barrel.forward * 0.5f;

        Vector3 direction = (currentAimPoint - origin).normalized;
        Vector3 hitPoint = currentAimPoint;

        if (Physics.Raycast(origin, direction, out RaycastHit barrelHit, maxRange, shootMask))
        {
            hitPoint = barrelHit.point;
            int hitLayer = barrelHit.collider.gameObject.layer;

            if (((1 << hitLayer) & enemyLayer) != 0)
            {
                MegaEnemyHealth enemyHP = barrelHit.collider.GetComponentInParent<MegaEnemyHealth>();
                if (enemyHP != null)
                {
                    enemyHP.TakeDamage(damagePerBullet);
                    Debug.Log($"[MegaTankShooter] Hit {barrelHit.collider.name} for {damagePerBullet} dmg");
                }
                else
                {
                    MegaTankHealth tankHP = barrelHit.collider.GetComponentInParent<MegaTankHealth>();
                    if (tankHP != null) tankHP.TakeDamage(damagePerBullet);
                    else Debug.LogWarning($"[MegaTankShooter] No health script on {barrelHit.collider.name}!");
                }
            }
            else
            {
                Debug.Log($"[MegaTankShooter] Hit {barrelHit.collider.name} — layer: {LayerMask.LayerToName(hitLayer)}");
            }
        }

        StartCoroutine(SpawnTrail(origin, hitPoint));
    }

    IEnumerator SpawnTrail(Vector3 from, Vector3 to)
    {
        GameObject trailObj;

        if (bulletTrailPrefab != null)
        {
            trailObj = Instantiate(bulletTrailPrefab, Vector3.zero, Quaternion.identity);
        }
        else
        {
            trailObj = new GameObject("BulletTrail");
            LineRenderer lr = trailObj.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = trailColor;
            lr.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            lr.startWidth = trailWidth;
            lr.endWidth = trailWidth * 0.1f;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
        }

        LineRenderer line = trailObj.GetComponent<LineRenderer>();
        if (line != null)
        {
            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }

        yield return new WaitForSeconds(trailDuration);
        Destroy(trailObj);
    }

    
}
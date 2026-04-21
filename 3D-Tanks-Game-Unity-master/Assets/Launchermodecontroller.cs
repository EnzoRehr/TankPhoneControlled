using UnityEngine;
using Debug = UnityEngine.Debug;

public class LauncherModeController : MonoBehaviour
{
    [Header("References")]
    public Transform launcherPoint;
    public GameObject rocketPrefab;

    [Header("Firing")]
    public float fireDelay = 0.7f;
    public float rocketSpeed = 30f;

    [Header("Crosshair")]
    public Texture2D crosshairTexture;
    public float crosshairSize = 32f;

    [Header("Remote Aim")]
    [Tooltip("How fast the crosshair moves across the screen from joystick input")]
    public float aimSensitivity = 300f;

    [Header("Scope HUD")]
    [Tooltip("Assign your LauncherHUD Canvas GameObject here")]
    public GameObject scopeHUD;

    private MonoBehaviour _megaTankShooter;
    private Camera _mainCamera;
    private bool _inLauncherMode = false;
    private float _nextFireTime = 0f;
    private Vector3 _savedCamPos;
    private Quaternion _savedCamRot;
    private Transform _savedCamParent;
    private MonoBehaviour[] _camScripts;

    private Vector2 _remoteCrosshairPos;
    private bool _useRemoteAim = false;

    private void Start()
    {
        _mainCamera = Camera.main;
        _megaTankShooter = GetComponent("MegaTankShooter") as MonoBehaviour;

        if (_mainCamera != null)
        {
            _camScripts = _mainCamera.GetComponents<MonoBehaviour>();
            Debug.Log("[LauncherMode] Scripts on camera:");
            foreach (var s in _camScripts)
                Debug.Log("  - " + s.GetType().Name);
        }

        _remoteCrosshairPos = new Vector2(Screen.width / 2f, Screen.height / 2f);

        if (launcherPoint == null)
            Debug.LogWarning("[LauncherMode] LauncherPoint not assigned.");

        // Hide scope HUD at start
        if (scopeHUD != null)
            scopeHUD.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1) || NetworkInputBridge.RemoteLauncherToggle)
        {
            if (_inLauncherMode) ExitLauncherMode();
            else EnterLauncherMode();
        }

        if (_inLauncherMode)
        {
            bool remoteAiming = Mathf.Abs(NetworkInputBridge.RemoteAimX) > 0.05f
                             || Mathf.Abs(NetworkInputBridge.RemoteAimY) > 0.05f;
            _useRemoteAim = remoteAiming;

            if (remoteAiming)
            {
                _remoteCrosshairPos.x += NetworkInputBridge.RemoteAimX * aimSensitivity * Time.deltaTime;
                _remoteCrosshairPos.y += NetworkInputBridge.RemoteAimY * aimSensitivity * Time.deltaTime;
                _remoteCrosshairPos.x = Mathf.Clamp(_remoteCrosshairPos.x, 0f, Screen.width);
                _remoteCrosshairPos.y = Mathf.Clamp(_remoteCrosshairPos.y, 0f, Screen.height);
            }

            bool fireDown = Input.GetMouseButtonDown(0) || NetworkInputBridge.RemoteFirePressed;
            if (fireDown && Time.time >= _nextFireTime)
            {
                FireRocket();
                _nextFireTime = Time.time + fireDelay;
            }
        }
    }

    private void LateUpdate()
    {
        if (!_inLauncherMode || launcherPoint == null || _mainCamera == null) return;
        _mainCamera.transform.position = launcherPoint.position;
        _mainCamera.transform.rotation = launcherPoint.rotation;
    }

    private void OnGUI()
    {
        if (!_inLauncherMode) return;

        Vector2 pos = _useRemoteAim
            ? new Vector2(_remoteCrosshairPos.x, Screen.height - _remoteCrosshairPos.y)
            : Event.current.mousePosition;

        if (crosshairTexture != null)
            GUI.DrawTexture(
                new Rect(pos.x - crosshairSize / 2f, pos.y - crosshairSize / 2f, crosshairSize, crosshairSize),
                crosshairTexture
            );
        else
            DrawDefaultCrosshair(pos);
    }

    private void FireRocket()
    {
        if (rocketPrefab == null || _mainCamera == null || launcherPoint == null) return;

        Vector2 screenPos = _useRemoteAim
            ? _remoteCrosshairPos
            : (Vector2)Input.mousePosition;

        Ray ray = _mainCamera.ScreenPointToRay(screenPos);
        Vector3 targetPoint = Physics.Raycast(ray, out RaycastHit hit, 1000f)
            ? hit.point
            : ray.origin + ray.direction * 500f;

        Vector3 direction = (targetPoint - launcherPoint.position).normalized;
        GameObject rocket = Instantiate(rocketPrefab, launcherPoint.position, Quaternion.LookRotation(direction));

        Rigidbody rb = rocket.GetComponent<Rigidbody>();
        if (rb != null) rb.velocity = direction * rocketSpeed;

        Debug.Log($"[LauncherMode] Rocket fired toward {targetPoint} from {(_useRemoteAim ? "remote aim" : "mouse")}");
    }

    private void EnterLauncherMode()
    {
        if (launcherPoint == null || _mainCamera == null) return;
        _inLauncherMode = true;

        _remoteCrosshairPos = new Vector2(Screen.width / 2f, Screen.height / 2f);
        _useRemoteAim = false;

        if (_megaTankShooter != null) _megaTankShooter.enabled = false;
        if (_camScripts != null) foreach (var s in _camScripts) s.enabled = false;

        _savedCamPos = _mainCamera.transform.position;
        _savedCamRot = _mainCamera.transform.rotation;
        _savedCamParent = _mainCamera.transform.parent;

        _mainCamera.transform.SetParent(null);
        _mainCamera.transform.position = launcherPoint.position;
        _mainCamera.transform.rotation = launcherPoint.rotation;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        // Show scope HUD
        if (scopeHUD != null)
            scopeHUD.SetActive(true);

        Debug.Log("[LauncherMode] Entered. Crosshair reset to center.");
    }

    private void ExitLauncherMode()
    {
        _inLauncherMode = false;

        if (_mainCamera != null)
        {
            _mainCamera.transform.SetParent(_savedCamParent);
            _mainCamera.transform.position = _savedCamPos;
            _mainCamera.transform.rotation = _savedCamRot;
        }

        if (_camScripts != null) foreach (var s in _camScripts) s.enabled = true;
        if (_megaTankShooter != null) _megaTankShooter.enabled = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Hide scope HUD
        if (scopeHUD != null)
            scopeHUD.SetActive(false);

        Debug.Log("[LauncherMode] Exited.");
    }

    private void DrawDefaultCrosshair(Vector2 center)
    {
        float half = crosshairSize / 2f, thick = 2f, gap = 5f;
        Color prev = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(center.x - half, center.y - thick / 2f, half - gap, thick), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(center.x + gap, center.y - thick / 2f, half - gap, thick), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(center.x - thick / 2f, center.y - half, thick, half - gap), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(center.x - thick / 2f, center.y + gap, thick, half - gap), Texture2D.whiteTexture);
        GUI.color = prev;
    }
}
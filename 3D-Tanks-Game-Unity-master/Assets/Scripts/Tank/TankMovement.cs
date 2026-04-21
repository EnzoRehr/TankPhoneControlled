using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;
using Image = UnityEngine.UI.Image;
using Text = UnityEngine.UI.Text;
using RenderMode = UnityEngine.RenderMode;

public class TankMovement : MonoBehaviour
{
    public int m_PlayerNumber = 1;
    public float m_Speed = 12f;
    public float m_TurnSpeed = 180f;
    public AudioSource m_MovementAudio;
    public AudioClip m_EngineIdling;
    public AudioClip m_EngineDriving;
    public float m_PitchRange = 0.2f;

    private string m_MovementAxisName;
    private string m_TurnAxisName;
    private Rigidbody m_Rigidbody;
    private float m_MovementInputValue;
    private float m_TurnInputValue;
    private float m_OriginalPitch;

    [Header("=== BOOST MODE ===")]
    public float m_BoostMultiplier = 2f;
    public float m_MaxFuel = 100f;
    public float m_FuelDrainRate = 20f;
    public float m_FuelRechargeRate = 10f;
    public Transform m_RunnerCameraPoint;

    [Header("=== BOOST HUD ===")]
    [Tooltip("Assign your fuel meter UI Canvas here")]
    public Canvas m_FuelCanvas;
    [Tooltip("The fuel bar fill Image (set to Fill mode)")]
    public Image m_FuelBarFill;
    [Tooltip("The fuel percentage Text component")]
    public Text m_FuelText;

    private float m_CurrentFuel;
    private bool m_IsBoosting = false;
    private float m_BaseSpeed;
    private Transform m_SavedCamParent;
    private Vector3 m_SavedCamLocalPos;
    private Quaternion m_SavedCamLocalRot;
    private float m_BaseFOV = 60f;
    private MonoBehaviour[] m_CamScripts;

    private Transform m_SavedCanvasParent;
    private Vector3 m_SavedCanvasLocalPos;
    private Quaternion m_SavedCanvasLocalRot;
    private Vector3 m_SavedCanvasLocalScale;

    public static float FuelPercent = 1f;
    public static bool IsBoostActive = false;

    private void Awake()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
        m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotationX
                                 | RigidbodyConstraints.FreezeRotationZ;
        m_Rigidbody.drag = 5f;
        m_Rigidbody.angularDrag = 10f;
    }

    private void OnEnable()
    {
        m_Rigidbody.isKinematic = false;
        m_MovementInputValue = 0f;
        m_TurnInputValue = 0f;
    }

    private void OnDisable()
    {
        m_Rigidbody.isKinematic = true;
    }

    private void Start()
    {
        m_MovementAxisName = "Vertical" + m_PlayerNumber;
        m_TurnAxisName = "Horizontal" + m_PlayerNumber;
        m_OriginalPitch = m_MovementAudio.pitch;
        m_CurrentFuel = m_MaxFuel;
        m_BaseSpeed = m_Speed;

        if (Camera.main != null)
            m_BaseFOV = Camera.main.fieldOfView;

        if (m_FuelCanvas != null)
            m_FuelCanvas.gameObject.SetActive(false);
    }

    private void Update()
    {
        // Track buttons take highest priority
        if (NetworkInputBridge.RemoteTrackLeft && !NetworkInputBridge.RemoteTrackRight)
        {
            m_MovementInputValue = 0f;
            m_TurnInputValue = -1f;
        }
        else if (NetworkInputBridge.RemoteTrackRight && !NetworkInputBridge.RemoteTrackLeft)
        {
            m_MovementInputValue = 0f;
            m_TurnInputValue = 1f;
        }
        else if (NetworkInputBridge.RemoteTrackLeft && NetworkInputBridge.RemoteTrackRight)
        {
            m_MovementInputValue = NetworkInputBridge.RemoteThrottle;
            m_TurnInputValue = 0f;
        }
        else
        {
            bool usingRemote = Mathf.Abs(NetworkInputBridge.RemoteThrottle) > 0.01f
                            || Mathf.Abs(NetworkInputBridge.RemoteSteering) > 0.01f;
            if (usingRemote)
            {
                m_MovementInputValue = NetworkInputBridge.RemoteThrottle;
                m_TurnInputValue = NetworkInputBridge.RemoteSteering;
            }
            else
            {
                m_MovementInputValue = Input.GetAxis(m_MovementAxisName);
                m_TurnInputValue = Input.GetAxis(m_TurnAxisName);
            }
        }

        // Boost input
        bool wantsBoost = (Input.GetKey(KeyCode.Space) || NetworkInputBridge.RemoteBoost)
                          && m_CurrentFuel > 0f;

        if (wantsBoost && !m_IsBoosting)
            EnterBoost();
        else if (!wantsBoost && m_IsBoosting)
            ExitBoost();

        // Fuel management
        if (m_IsBoosting)
        {
            m_CurrentFuel -= m_FuelDrainRate * Time.deltaTime;
            if (m_CurrentFuel <= 0f)
            {
                m_CurrentFuel = 0f;
                ExitBoost();
            }
        }
        else
        {
            m_CurrentFuel = Mathf.Min(m_CurrentFuel + m_FuelRechargeRate * Time.deltaTime, m_MaxFuel);
        }

        FuelPercent = m_CurrentFuel / m_MaxFuel;
        IsBoostActive = m_IsBoosting;

        // Broadcast real fuel value to phone
        NetworkInputBridge.BroadcastFuel(FuelPercent);

        UpdateFuelHUD();
        EngineAudio();
        Move();
        Turn();
    }

    private void UpdateFuelHUD()
    {
        if (!m_IsBoosting) return;

        if (m_FuelBarFill != null)
        {
            m_FuelBarFill.fillAmount = FuelPercent;
            m_FuelBarFill.color = FuelPercent > 0.5f
                ? Color.Lerp(Color.yellow, Color.green, (FuelPercent - 0.5f) * 2f)
                : Color.Lerp(Color.red, Color.yellow, FuelPercent * 2f);
        }

        if (m_FuelText != null)
            m_FuelText.text = Mathf.RoundToInt(FuelPercent * 100f) + "%";
    }

    private void EnterBoost()
    {
        m_IsBoosting = true;
        m_Speed = m_BaseSpeed * m_BoostMultiplier;

        if (Camera.main != null)
        {
            m_CamScripts = Camera.main.GetComponents<MonoBehaviour>();
            foreach (var s in m_CamScripts) s.enabled = false;

            m_SavedCamParent = Camera.main.transform.parent;
            m_SavedCamLocalPos = Camera.main.transform.localPosition;
            m_SavedCamLocalRot = Camera.main.transform.localRotation;

            if (m_RunnerCameraPoint != null)
            {
                Camera.main.transform.SetParent(m_RunnerCameraPoint);
                Camera.main.transform.localPosition = Vector3.zero;
                Camera.main.transform.localRotation = Quaternion.identity;
            }

            StartCoroutine(ZoomFOV(Camera.main.fieldOfView, 50f, 0.4f));

            if (m_FuelCanvas != null)
            {
                m_SavedCanvasParent = m_FuelCanvas.transform.parent;
                m_SavedCanvasLocalPos = m_FuelCanvas.transform.localPosition;
                m_SavedCanvasLocalRot = m_FuelCanvas.transform.localRotation;
                m_SavedCanvasLocalScale = m_FuelCanvas.transform.localScale;

                m_FuelCanvas.renderMode = (RenderMode)2;
                m_FuelCanvas.transform.SetParent(Camera.main.transform);
                m_FuelCanvas.transform.localPosition = new Vector3(-0.35f, -0.25f, 0.8f);
                m_FuelCanvas.transform.localRotation = Quaternion.identity;
                m_FuelCanvas.transform.localScale = Vector3.one * 0.001f;
                m_FuelCanvas.gameObject.SetActive(true);
            }
        }

        Debug.Log("[Boost] ENGAGED — speed: " + m_Speed);
    }

    private void ExitBoost()
    {
        m_IsBoosting = false;
        m_Speed = m_BaseSpeed;

        if (Camera.main != null)
        {
            StartCoroutine(ZoomFOV(Camera.main.fieldOfView, m_BaseFOV, 0.4f));

            Camera.main.transform.SetParent(m_SavedCamParent);
            Camera.main.transform.localPosition = m_SavedCamLocalPos;
            Camera.main.transform.localRotation = m_SavedCamLocalRot;

            if (m_CamScripts != null)
                foreach (var s in m_CamScripts) s.enabled = true;
        }

        if (m_FuelCanvas != null)
        {
            m_FuelCanvas.transform.SetParent(m_SavedCanvasParent);
            m_FuelCanvas.transform.localPosition = m_SavedCanvasLocalPos;
            m_FuelCanvas.transform.localRotation = m_SavedCanvasLocalRot;
            m_FuelCanvas.transform.localScale = m_SavedCanvasLocalScale;
            m_FuelCanvas.renderMode = (RenderMode)0;
            m_FuelCanvas.gameObject.SetActive(false);
        }

        Debug.Log("[Boost] DISENGAGED — speed restored: " + m_Speed);
    }

    private IEnumerator ZoomFOV(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            if (Camera.main) Camera.main.fieldOfView = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        if (Camera.main) Camera.main.fieldOfView = to;
    }

    private void EngineAudio()
    {
        if (Mathf.Abs(m_MovementInputValue) < 0.1f && Mathf.Abs(m_TurnInputValue) < 0.1f)
        {
            if (m_MovementAudio.clip == m_EngineDriving)
            {
                m_MovementAudio.clip = m_EngineIdling;
                m_MovementAudio.pitch = Random.Range(m_OriginalPitch - m_PitchRange, m_OriginalPitch + m_PitchRange);
                m_MovementAudio.Play();
            }
        }
        else
        {
            if (m_MovementAudio.clip == m_EngineIdling)
            {
                m_MovementAudio.clip = m_EngineDriving;
                m_MovementAudio.pitch = Random.Range(m_OriginalPitch - m_PitchRange, m_OriginalPitch + m_PitchRange);
                m_MovementAudio.Play();
            }
        }
    }

    private void Move()
    {
        Vector3 movement = transform.forward * m_MovementInputValue * m_Speed * Time.deltaTime;
        m_Rigidbody.MovePosition(m_Rigidbody.position + movement);
    }

    private void Turn()
    {
        float turn = m_TurnInputValue * m_TurnSpeed * Time.deltaTime;
        Quaternion turnRotation = Quaternion.Euler(0f, turn, 0f);
        m_Rigidbody.MoveRotation(m_Rigidbody.rotation * turnRotation);
    }

    private void KeepUpright()
    {
        Vector3 currentEuler = m_Rigidbody.rotation.eulerAngles;
        m_Rigidbody.rotation = Quaternion.Euler(0f, currentEuler.y, 0f);
    }
}
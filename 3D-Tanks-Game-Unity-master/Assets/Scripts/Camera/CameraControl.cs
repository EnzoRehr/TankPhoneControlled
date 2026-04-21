using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform m_Target;

    [Header("Follow Settings")]
    public Vector3 m_PositionOffset = Vector3.zero;

    [Header("Rotation Settings")]
    public bool m_FollowRotation = true;

    private Vector3 m_InitialOffset;

    private void Start()
    {
        if (m_Target != null)
            m_InitialOffset = transform.position - m_Target.position;
        else
            UnityEngine.Debug.LogWarning("CameraFollow: No target assigned.");
    }

    // FixedUpdate syncs with physics so the world stops jittering
    private void FixedUpdate()
    {
        if (m_Target == null) return;

        transform.position = m_Target.position + m_InitialOffset + m_PositionOffset;

        if (m_FollowRotation)
            transform.rotation = m_Target.rotation;
    }

    public void SetTarget(Transform newTarget)
    {
        m_Target = newTarget;
        if (m_Target != null)
            m_InitialOffset = transform.position - m_Target.position;
    }

    public void SnapToTarget()
    {
        if (m_Target == null) return;
        transform.position = m_Target.position + m_InitialOffset + m_PositionOffset;
        if (m_FollowRotation)
            transform.rotation = m_Target.rotation;
    }

    private void OnDrawGizmosSelected()
    {
        if (m_Target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, m_Target.position);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(m_Target.position, 0.5f);
        }
    }
}
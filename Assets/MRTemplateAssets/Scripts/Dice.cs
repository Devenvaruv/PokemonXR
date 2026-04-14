using System;
using UnityEngine.Events;

namespace UnityEngine.XR.Templates.MR
{
    /// <summary>
    /// Detects when a die has settled and reports the face that is facing up.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Dice : MonoBehaviour
    {
        [Serializable]
        public struct Face
        {
            public Vector3 normal;
            public int value;
        }

        [SerializeField, Tooltip("Invoked once when the die settles; passes the face value that is up.")]
        UnityEvent<int> m_OnRolled = new UnityEvent<int>();

        public UnityEvent<int> onRolled => m_OnRolled;

        [SerializeField, Tooltip("Velocity magnitude below which the die is considered still.")]
        float m_VelocityThreshold = 0.05f;

        [SerializeField, Tooltip("Angular velocity magnitude below which the die is considered still.")]
        float m_AngularVelocityThreshold = 1.0f;

        [SerializeField, Tooltip("Time in seconds the die must remain still before reporting a value.")]
        float m_SettleTime = 0.4f;

        [SerializeField, Tooltip("Local-space face normals mapped to pip values. Adjust if your mesh orientation differs.")]
        Face[] m_Faces =
        {
            new Face { normal = Vector3.up, value = 1 },
            new Face { normal = Vector3.down, value = 6 },
            new Face { normal = Vector3.right, value = 3 },
            new Face { normal = Vector3.left, value = 4 },
            new Face { normal = Vector3.forward, value = 2 },
            new Face { normal = Vector3.back, value = 5 },
        };

        Rigidbody m_Rigidbody;
        bool m_Settled;
        float m_StillTimer;

        void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
        }

        void Update()
        {
            if (m_Rigidbody == null)
                return;

            var isStill = m_Rigidbody.linearVelocity.sqrMagnitude < m_VelocityThreshold * m_VelocityThreshold &&
                          m_Rigidbody.angularVelocity.sqrMagnitude < m_AngularVelocityThreshold * m_AngularVelocityThreshold;

            if (isStill)
            {
                m_StillTimer += Time.deltaTime;
                if (!m_Settled && m_StillTimer >= m_SettleTime)
                {
                    m_Settled = true;
                    m_OnRolled?.Invoke(GetTopValue());
                }
            }
            else
            {
                m_StillTimer = 0f;
                m_Settled = false;
            }
        }

        int GetTopValue()
        {
            if (m_Faces == null || m_Faces.Length == 0)
                return 0;

            var bestDot = float.NegativeInfinity;
            var bestValue = 0;
            foreach (var face in m_Faces)
            {
                var worldNormal = transform.TransformDirection(face.normal);
                var dot = Vector3.Dot(worldNormal, Vector3.up);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestValue = face.value;
                }
            }

            return bestValue;
        }
    }
}

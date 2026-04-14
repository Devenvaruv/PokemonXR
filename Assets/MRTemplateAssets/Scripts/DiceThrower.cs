using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace UnityEngine.XR.Templates.MR
{
    /// <summary>
    /// Spawns and throws a dice toward a detected plane hit.
    /// </summary>
    public class DiceThrower : MonoBehaviour
    {
        [SerializeField, Tooltip("Prefab that contains a Rigidbody and the Dice component.")]
        GameObject m_DicePrefab;

        [SerializeField, Tooltip("ARRaycastManager used to find planes for spawning.")]
        ARRaycastManager m_RaycastManager;

        [SerializeField, Tooltip("Spawn the dice this far above the hit pose.")]
        float m_SpawnHeight = 0.1f;

        [SerializeField, Tooltip("Impulse applied to the dice forward/up on spawn.")]
        float m_ThrowForce = 3f;

        [SerializeField, Tooltip("Random torque impulse applied on spawn.")]
        float m_TorqueForce = 1.5f;

        readonly List<ARRaycastHit> m_Hits = new();

        /// <summary>
        /// Call this with a screen position (touch or reticle) to throw the dice at the nearest plane.
        /// </summary>
        public void ThrowAt(Vector2 screenPosition)
        {
            if (m_DicePrefab == null || m_RaycastManager == null)
                return;

            if (!m_RaycastManager.Raycast(screenPosition, m_Hits, TrackableType.PlaneWithinPolygon))
                return;

            var hit = m_Hits[0];
            var pose = hit.pose;
            var spawnPos = pose.position + pose.up * m_SpawnHeight;
            var spawnRot = Random.rotation;

            var dice = Instantiate(m_DicePrefab, spawnPos, spawnRot);
            if (!dice.TryGetComponent(out Rigidbody rb))
                return;

            var forward = Camera.main ? Camera.main.transform.forward : pose.up;
            var direction = (pose.up + forward).normalized;
            rb.AddForce(direction * m_ThrowForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * m_TorqueForce, ForceMode.Impulse);
        }
    }
}

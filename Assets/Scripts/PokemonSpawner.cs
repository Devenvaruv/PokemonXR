using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns 1–2 instances of each listed Pokemon prefab within a simple hardcoded 4m x 4m area around this spawner.
/// Attach to an empty GameObject; no bounds setup required.
/// </summary>
public class PokemonSpawner : MonoBehaviour
{
    [Tooltip("Prefabs of Pokemon to spawn. Each prefab will spawn between Min/Max per prefab.")]
    public List<GameObject> pokemonPrefabs = new List<GameObject>();

    [Tooltip("Spawn area width (meters). Default 4m.")]
    public float areaWidth = 4f;
    [Tooltip("Spawn area depth (meters). Default 4m.")]
    public float areaDepth = 4f;
    [Tooltip("Vertical offset from spawner position if no ground is found.")]
    public float heightOffset = 0f;

    [Tooltip("Minimum number spawned per prefab.")]
    public int minPerPrefab = 1;

    [Tooltip("Maximum number spawned per prefab.")]
    public int maxPerPrefab = 2;

    [Tooltip("Minimum separation between spawned Pokemon (in meters).")]
    public float separationRadius = 0.6f;

    [Tooltip("Maximum random attempts per spawn to find a free spot.")]
    public int maxPlacementAttempts = 25;

    [Tooltip("If true, spawns automatically in Start().")]
    public bool spawnOnStart = true;

    [Tooltip("Optional parent for spawned instances; defaults to this transform.")]
    public Transform spawnParent;

    [Tooltip("LayerMask used for grounding (raycast down to find floor). Leave empty to skip grounding.")]
    public LayerMask groundMask;

    private readonly List<Vector3> _placed = new List<Vector3>();

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnAll();
        }
    }

    public void SpawnAllDelayed(float delaySeconds = 4f)
    {
        StartCoroutine(SpawnAfterDelay(delaySeconds));
    }

    private System.Collections.IEnumerator SpawnAfterDelay(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        SpawnAll();
    }

    [ContextMenu("Spawn All Now")]
    public void SpawnAll()
    {
        _placed.Clear();

        if (spawnParent == null) spawnParent = transform;

        foreach (var prefab in pokemonPrefabs)
        {
            if (prefab == null) continue;
            int count = Random.Range(minPerPrefab, maxPerPrefab + 1);
            for (int i = 0; i < count; i++)
            {
                if (TrySpawn(prefab, out GameObject instance))
                {
                    instance.transform.SetParent(spawnParent, worldPositionStays: true);
                }
                else
                {
                    Debug.LogWarning($"[PokemonSpawner] Failed to place {prefab.name} after {maxPlacementAttempts} attempts.");
                }
            }
        }
    }

    private bool TrySpawn(GameObject prefab, out GameObject instance)
    {
        instance = null;
        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            Vector3 pos = RandomPointInArea();

            // optional grounding
            if (groundMask.value != 0)
            {
                if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    pos = hit.point;
                }
                else
                {
                    pos.y = transform.position.y + heightOffset;
                }
            }
            else
            {
                pos.y = transform.position.y + heightOffset;
            }

            if (IsFarEnough(pos))
            {
                instance = Instantiate(prefab, pos, Quaternion.identity);
                _placed.Add(pos);
                return true;
            }
        }
        return false;
    }

    private Vector3 RandomPointInArea()
    {
        float x = Random.Range(-areaWidth * 0.5f, areaWidth * 0.5f);
        float z = Random.Range(-areaDepth * 0.5f, areaDepth * 0.5f);
        Vector3 local = new Vector3(x, 0f, z);
        return transform.TransformPoint(local);
    }

    private bool IsFarEnough(Vector3 position)
    {
        float minSqr = separationRadius * separationRadius;
        foreach (var placed in _placed)
        {
            if ((placed - position).sqrMagnitude < minSqr)
            {
                return false;
            }
        }
        return true;
    }
}

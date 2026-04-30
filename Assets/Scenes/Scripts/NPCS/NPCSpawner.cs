using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NPCSpawner : MonoBehaviour
{
    [Header("NPC Spawning Settings")]
    [Tooltip("List of NPC Prefabs to spawn randomly.")]
    public GameObject[] npcPrefabs;

    [Tooltip("How many NPCs to spawn in the initial area (only used if CityTileManager is NOT handling per-tile spawning).")]
    public int numberOfNPCsToSpawn = 50;

    [Tooltip("The center point of the spawning area. Defaults to this object if empty.")]
    public Transform spawnCenter;

    [Tooltip("The size of the area to spawn NPCs in (X and Z axis).")]
    public Vector2 spawnAreaSize = new Vector2(100f, 100f);

    [Header("Ground Detection")]
    [Tooltip("Height from which to raycast down to find the ground.")]
    public float raycastStartHeight = 100f;

    [Tooltip("Which layers count as 'Ground' for NPCs to stand on.")]
    public LayerMask groundLayer = Physics.DefaultRaycastLayers;

    [Tooltip("Offset added to the Y position so the NPC doesn't spawn halfway in the floor (e.g., 1 for a default Unity Capsule).")]
    public float yOffset = 1f;

    void Start()
    {
        if (spawnCenter == null)
            spawnCenter = this.transform;
        // NOTE: Initial one-shot spawning is disabled here.
        // CityTileManager now calls SpawnNPCsForTile() per tile so NPCs
        // appear across the whole infinite city, not just at startup.
    }

    /// <summary>
    /// Called by CityTileManager each time a new city tile is created.
    /// NPCs are parented to the tile so they are automatically destroyed
    /// when the tile is despawned — no manual cleanup needed.
    /// A one-frame delay is used so the tile's colliders are registered
    /// by PhysX before we raycast against them.
    /// </summary>
    public void SpawnNPCsForTile(Vector3 tileCenter, Vector2 tileArea, int count, Transform tileParent)
    {
        if (npcPrefabs == null || npcPrefabs.Length == 0)
        {
            Debug.LogWarning("NPCSpawner: No NPC prefabs assigned.");
            return;
        }
        StartCoroutine(SpawnNPCsForTileCoroutine(tileCenter, tileArea, count, tileParent));
    }

    private IEnumerator SpawnNPCsForTileCoroutine(Vector3 center, Vector2 area, int count, Transform parent)
    {
        // Wait two frames so the freshly instantiated tile's colliders are
        // fully registered with the physics engine before we raycast.
        yield return null;
        yield return new WaitForFixedUpdate();

        // Bail out if the tile was already despawned while we were waiting
        if (parent == null) yield break;

        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            if (TrySpawnNPC(center, area, parent))
                spawned++;
        }
        Debug.Log($"NPCSpawner: Spawned {spawned}/{count} NPCs on tile at {center}.");
    }

    private bool TrySpawnNPC(Vector3 center, Vector2 area, Transform parent)
    {
        float randomX = Random.Range(-area.x / 2f, area.x / 2f);
        float randomZ = Random.Range(-area.y / 2f, area.y / 2f);

        Vector3 rayStart = center + new Vector3(randomX, raycastStartHeight, randomZ);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastStartHeight * 2f, groundLayer))
        {
            int prefabIndex = Random.Range(0, npcPrefabs.Length);
            Quaternion randomRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Vector3 spawnPos = hit.point + new Vector3(0f, yOffset, 0f);

            GameObject npc = Instantiate(npcPrefabs[prefabIndex], spawnPos, randomRot);

            // Parent to the tile — when the tile is destroyed so is this NPC.
            npc.transform.SetParent(parent);
            return true;
        }
        return false;
    }

    // Draws a green outline in the Scene view to help visualize the spawn area
    private void OnDrawGizmosSelected()
    {
        Vector3 center = spawnCenter != null ? spawnCenter.position : transform.position;
        Gizmos.color = new Color(0, 1, 0, 0.5f);
        Gizmos.DrawWireCube(center, new Vector3(spawnAreaSize.x, 1f, spawnAreaSize.y));
    }
}

using UnityEngine;
using System.Collections.Generic;

public class NPCSpawner : MonoBehaviour
{
    [Header("NPC Spawning Settings")]
    [Tooltip("List of NPC Prefabs to spawn randomly.")]
    public GameObject[] npcPrefabs;
    
    [Tooltip("How many NPCs to spawn.")]
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

    [Header("Infinite Map Settings")]
    [Tooltip("If true, NPCs that fall far behind the spawn center will be continuously respawned near it.")]
    public bool keepInRadius = true;
    [Tooltip("How far an NPC can be from the spawn center before being respawned.")]
    public float maxDistance = 150f;

    private List<GameObject> spawnedNPCs = new List<GameObject>();

    void Start()
    {
        if (spawnCenter == null || spawnCenter == this.transform) 
        {
            // Attempt to automatically find the car so the spawner follows it
            GameObject playerCar = GameObject.FindGameObjectWithTag("PlayerCar");
            if (playerCar != null)
            {
                spawnCenter = playerCar.transform;
            }
            else
            {
                spawnCenter = this.transform;
            }
        }
        SpawnNPCs();
    }

    void Update()
    {
        if (!keepInRadius || spawnCenter == null) return;

        // Loop backwards because we remove items from the list
        for (int i = spawnedNPCs.Count - 1; i >= 0; i--)
        {
            GameObject npc = spawnedNPCs[i];
            
            // Replace dead/missing NPCs
            if (npc == null) 
            {
                spawnedNPCs.RemoveAt(i);
                SpawnSingleNPC(); 
                continue;
            }

            // Check distance
            float dist = Vector3.Distance(npc.transform.position, spawnCenter.position);
            if (dist > maxDistance)
            {
                Destroy(npc);
                spawnedNPCs.RemoveAt(i);
                SpawnSingleNPC();
            }
        }
    }

    public void SpawnNPCs()
    {
        if (npcPrefabs == null || npcPrefabs.Length == 0)
        {
            Debug.LogWarning("No NPC prefabs assigned to the NPCSpawner.");
            return;
        }

        int successfullySpawned = 0;

        for (int i = 0; i < numberOfNPCsToSpawn; i++)
        {
            if (SpawnSingleNPC())
            {
                successfullySpawned++;
            }
        }

        Debug.Log($"Successfully spawned {successfullySpawned} NPCs out of {numberOfNPCsToSpawn} attempted.");
    }

    private bool SpawnSingleNPC()
    {
        // Pick a random position within the defined X and Z area
        float randomX = Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f);
        float randomZ = Random.Range(-spawnAreaSize.y / 2f, spawnAreaSize.y / 2f);
        
        // Start the raycast high up above the spawn center
        Vector3 rayStartPos = spawnCenter.position + new Vector3(randomX, raycastStartHeight, randomZ);

        // Raycast straight down to find the ground
        if (Physics.Raycast(rayStartPos, Vector3.down, out RaycastHit hit, raycastStartHeight * 2f, groundLayer))
        {
            // Pick a random NPC prefab from the list
            int prefabIndex = Random.Range(0, npcPrefabs.Length);
            GameObject prefabToSpawn = npcPrefabs[prefabIndex];

            // Give the NPC a random Y rotation so they face different directions
            Quaternion randomRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            // Offset the hit point so the pivot isn't stuck inside the ground
            Vector3 spawnPos = hit.point + new Vector3(0f, yOffset, 0f);

            // Instantiate the selected prefab at the hit point on the ground
            GameObject spawnedNPC = Instantiate(prefabToSpawn, spawnPos, randomRot);
            
            // Organize under this spawner object
            spawnedNPC.transform.SetParent(this.transform);
            spawnedNPCs.Add(spawnedNPC);
            return true;
        }
        else
        {
            return false;
        }
    }

    // Draws a green outline in the Scene view to help visualize the spawn area
    private void OnDrawGizmosSelected()
    {
        if (spawnCenter != null)
        {
            Gizmos.color = new Color(0, 1, 0, 0.5f);
            Vector3 center = spawnCenter.position;
            // Draw a flat box indicating the size of the spawning bounds
            Vector3 size = new Vector3(spawnAreaSize.x, 1f, spawnAreaSize.y);
            Gizmos.DrawWireCube(center, size);
        }
    }
}

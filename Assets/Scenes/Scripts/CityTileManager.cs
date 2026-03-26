using UnityEngine;
using System.Collections.Generic;

public class CityTileManager : MonoBehaviour
{
    [Header("References")]
    public Transform car;
    public GameObject cityPrefab;

    [Header("Settings")]
    public int viewDistance = 2;

    private Dictionary<Vector2Int, GameObject> activeTiles = new();
    private Vector3 citySize;

    void Start()
    {
        // Auto calculate city size
        Bounds bounds = new Bounds(cityPrefab.transform.position, Vector3.zero);
        foreach (var r in cityPrefab.GetComponentsInChildren<Renderer>())
            bounds.Encapsulate(r.bounds);

        citySize = new Vector3(bounds.size.x, 0, bounds.size.z);
        Debug.Log($"Auto-detected city size: {citySize}");

        UpdateTiles();
    }

    void Update()
    {
        UpdateTiles();
    }

    Vector2Int GetTileCoord(Vector3 pos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(pos.x / citySize.x),
            Mathf.FloorToInt(pos.z / citySize.z)
        );
    }

    void UpdateTiles()
    {
        Vector2Int carTile = GetTileCoord(car.position);
        HashSet<Vector2Int> neededTiles = new();

        for (int x = -viewDistance; x <= viewDistance; x++)
            for (int z = -viewDistance; z <= viewDistance; z++)
                neededTiles.Add(carTile + new Vector2Int(x, z));

        // Spawn missing tiles
        foreach (var coord in neededTiles)
        {
            if (!activeTiles.ContainsKey(coord))
            {
                Vector3 spawnPos = new Vector3(
                    coord.x * citySize.x,
                    0,
                    coord.y * citySize.z
                );
                GameObject tile = Instantiate(cityPrefab, spawnPos, Quaternion.identity);
                tile.name = $"City_{coord.x}_{coord.y}";
                activeTiles[coord] = tile;
            }
        }

        // Despawn far tiles
        List<Vector2Int> toRemove = new();
        foreach (var kv in activeTiles)
        {
            if (!neededTiles.Contains(kv.Key))
            {
                Destroy(kv.Value);
                toRemove.Add(kv.Key);
            }
        }
        foreach (var key in toRemove) activeTiles.Remove(key);
    }
}
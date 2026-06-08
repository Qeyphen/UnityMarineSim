using UnityEngine;
using System.IO;
using System.Collections.Generic;

[System.Serializable]
public class EnvironmentConfig
{
    public float  wind_speed;
    public float  wave_height;
    public string time_of_day;
}

[System.Serializable]
public class ObjectConfig
{
    public string  id;
    public string  type;
    public bool    dynamic;
    public string  ros2_topic;
    public float[] position;
    public float[] rotation;
}

[System.Serializable]
public class SceneConfig
{
    public EnvironmentConfig  environment;
    public List<ObjectConfig> objects;
}

public class SceneBuilder : MonoBehaviour
{
    [Header("Config")]
    public string configFileName = "scene.json";

    [Header("Prefabs")]
    public GameObject boatPrefab;
    public GameObject buoyPrefab;

    private SceneConfig                    config;
    private Dictionary<string, GameObject> spawnedObjects
        = new Dictionary<string, GameObject>();

    void Start()
    {
        LoadConfig();
        if (config == null) return;
        SpawnObjects();
    }

    void LoadConfig()
    {
        string[] searchPaths = {
            Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "config", configFileName)),
            Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "config", configFileName)),
            Path.Combine(Application.dataPath, "Config", configFileName),
        };

        string json      = null;
        string foundPath = null;

        foreach (string path in searchPaths)
        {
            if (File.Exists(path))
            {
                json      = File.ReadAllText(path);
                foundPath = path;
                break;
            }
        }

        if (json == null)
        {
            Debug.LogError("[SceneLoader] scene.json not found! Searched:\n" +
                string.Join("\n", searchPaths));
            return;
        }

        config = JsonUtility.FromJson<SceneConfig>(json);
        Debug.Log($"[SceneLoader] Loaded {config.objects.Count} objects from:\n{foundPath}");
    }

    void SpawnObjects()
    {
        foreach (ObjectConfig obj in config.objects)
        {
            GameObject prefab = GetPrefab(obj.type);
            if (prefab == null)
            {
                Debug.LogWarning($"[SceneLoader] No prefab for type '{obj.type}'" +
                                 $" — skipping {obj.id}");
                continue;
            }

            Vector3 pos = new Vector3(
                obj.position[0],
                obj.position[1],
                obj.position[2]
            );
            Quaternion rot = Quaternion.Euler(
                obj.rotation[0],
                obj.rotation[1],
                obj.rotation[2]
            );

            GameObject spawned = Instantiate(prefab, pos, rot);
            spawned.name       = obj.id;

            if (obj.dynamic)
            {
                Debug.Log($"[SceneLoader] DYNAMIC: {obj.id} ({obj.type})" +
                          $" at {pos} | topic: {obj.ros2_topic}");
            }
            else
            {
                Rigidbody rb = spawned.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
                Debug.Log($"[SceneLoader] STATIC: {obj.id} ({obj.type}) at {pos}");
            }

            spawnedObjects[obj.id] = spawned;
        }

        Debug.Log($"[SceneLoader] Done — {spawnedObjects.Count} objects spawned.");
    }

    GameObject GetPrefab(string type)
    {
        switch (type.ToLower())
        {
            case "boat": return boatPrefab;
            case "buoy": return buoyPrefab;
            default:     return null;
        }
    }

    public GameObject GetSpawnedObject(string id)
    {
        return spawnedObjects.ContainsKey(id) ? spawnedObjects[id] : null;
    }
}
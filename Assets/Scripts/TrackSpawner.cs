using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.N3New;

/// <summary>
/// Renders the scenario generator's traffic. Subscribes to /sim/tracks
/// (n3_new_msgs/TrackArray) and, for each track, spawns a GameObject the first time it is
/// seen, moves it every message, and despawns it when its id stops appearing.
///
/// Tracks are GROUND TRUTH from ROS, so they are position-driven, not physics-simulated:
/// any Rigidbody on the spawned prefab is made kinematic and the transform is written directly.
/// (The ego boat keeps its own physics + controller — this only owns the dynamic traffic.)
///
/// Coordinate convention — matches the map layer (see DynamicObstaclePublisher):
///   ROS ENU (x = East, y = North) -> Unity (x, z); Unity y = up = 0.
///   ENU yaw (from the pose quaternion about ROS z) -> rotation about Unity y.
/// </summary>
public class TrackSpawner : MonoBehaviour
{
    /// <summary>Mirror of the n3_new_msgs/Track type constants, for a friendly Inspector dropdown.</summary>
    public enum TrackType : byte
    {
        Unknown = 0, Sailboat = 1, Motorboat = 2, Jetski = 3, Kayak = 4, Paddleboard = 5,
        Swimmer = 6, Dinghy = 7, FishingBoat = 8, Ferry = 9, Cargo = 10, Buoy = 11,
        Debris = 12, Windsurf = 13, Kitesurf = 14, Pedalo = 15,
    }

    [System.Serializable]
    public struct TypePrefab
    {
        public TrackType type;
        public GameObject prefab;
    }

    [Header("ROS")]
    [Tooltip("Topic the scenario generator publishes TrackArray on.")]
    public string tracksTopic = "/sim/tracks";

    [Header("Prefabs (placeholders OK)")]
    [Tooltip("Used for any track type without an explicit override below (e.g. the catamaran).")]
    public GameObject defaultPrefab;
    [Tooltip("Per-type prefab overrides, e.g. Buoy + Debris -> buoy prefab.")]
    public TypePrefab[] prefabOverrides;

    [Header("Container")]
    [Tooltip("Optional parent for spawned tracks (keeps the hierarchy tidy). Defaults to this object.")]
    public Transform container;

    private ROSConnection ros;
    private readonly Dictionary<uint, GameObject> spawned = new Dictionary<uint, GameObject>();
    private readonly Dictionary<byte, GameObject> prefabByType = new Dictionary<byte, GameObject>();
    private readonly HashSet<uint> seenThisMsg = new HashSet<uint>();

    void Awake()
    {
        if (container == null) container = transform;
        prefabByType.Clear();
        if (prefabOverrides != null)
            foreach (var tp in prefabOverrides)
                if (tp.prefab != null) prefabByType[(byte)tp.type] = tp.prefab;
    }

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<TrackArrayMsg>(tracksTopic, OnTracks);
        Debug.Log($"[TrackSpawner] Subscribed to '{tracksTopic}'. default=" +
                  $"{(defaultPrefab ? defaultPrefab.name : "NONE")}, overrides={prefabByType.Count}");
    }

    void OnTracks(TrackArrayMsg msg)
    {
        if (msg.tracks == null) return;
        seenThisMsg.Clear();

        foreach (var t in msg.tracks)
        {
            seenThisMsg.Add(t.id);

            if (!spawned.TryGetValue(t.id, out GameObject go) || go == null)
            {
                GameObject prefab = PrefabFor(t.type);
                if (prefab == null) continue;             // nothing mapped -> skip this track
                go = Instantiate(prefab, container);
                go.name = $"track_{t.id}_{(TrackType)t.type}";
                MakeKinematic(go);
                spawned[t.id] = go;
            }

            // Position: ROS ENU (x, y) -> Unity (x, 0, z).
            go.transform.position = new Vector3(
                (float)t.pose.position.x, 0f, (float)t.pose.position.y);

            // Heading: ENU yaw from the yaw-only quaternion -> Unity rotation about Y.
            float qz = (float)t.pose.orientation.z;
            float qw = (float)t.pose.orientation.w;
            float headingRad = 2f * Mathf.Atan2(qz, qw);
            Vector3 fwd = new Vector3(Mathf.Cos(headingRad), 0f, Mathf.Sin(headingRad));
            if (fwd.sqrMagnitude > 1e-6f)
                go.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        // Despawn tracks that disappeared from the scenario.
        if (spawned.Count != seenThisMsg.Count)
        {
            var toRemove = new List<uint>();
            foreach (var kv in spawned)
                if (!seenThisMsg.Contains(kv.Key)) toRemove.Add(kv.Key);
            foreach (var id in toRemove)
            {
                if (spawned[id] != null) Destroy(spawned[id]);
                spawned.Remove(id);
            }
        }
    }

    GameObject PrefabFor(byte type)
    {
        if (prefabByType.TryGetValue(type, out GameObject p) && p != null) return p;
        return defaultPrefab;
    }

    static void MakeKinematic(GameObject go)
    {
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>())
            rb.isKinematic = true;
    }
}

using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

/// <summary>
/// Publishes the live positions of the moving (dynamic) boats as the "dynamic layer"
/// that complements the static /map occupancy grid.
///
/// Two outputs, at <see cref="publishRate"/> Hz:
///   * a GLOBAL topic (<see cref="globalTopic"/>, default /dynamic_obstacles) with
///     EVERY agent — used for visualisation/monitoring (RViz) and a live "all objects" view.
///   * per-agent "/{id}/dynamic_obstacles" with every OTHER agent — so a boat's own
///     avoidance logic reads its topic without seeing itself.
///
/// Coordinate convention for the MAP layer (matches the occupancy grid so RViz aligns):
/// the ground plane is ROS XY -> Unity x = ROS x, Unity z = ROS y, ROS z = 0 (up).
/// (Note this differs from the /target_pose control channel, which uses x/z.)
/// </summary>
public class DynamicObstaclePublisher : MonoBehaviour
{
    [Header("ROS")]
    [Tooltip("Per-agent topic is /{agentId}/{topicSuffix} (every OTHER agent).")]
    public string topicSuffix = "dynamic_obstacles";
    [Tooltip("Global topic with ALL agents (for RViz / live all-objects view). Empty = off.")]
    public string globalTopic = "/dynamic_obstacles";
    public string frameId     = "map";

    [Header("Rate")]
    public float publishRate = 10f;   // Hz

    private class Agent { public string id; public Transform tf; public string topic; }

    private readonly List<Agent> agents = new List<Agent>();
    private ROSConnection ros;
    private float accumulator;

    /// <summary>Registers the dynamic agents and their topics. Called by SceneBuilder.</summary>
    public void SetAgents(Dictionary<string, GameObject> dynamicAgents)
    {
        ros = ROSConnection.GetOrCreateInstance();
        agents.Clear();

        foreach (KeyValuePair<string, GameObject> kv in dynamicAgents)
        {
            if (kv.Value == null) continue;
            string topic = $"/{kv.Key}/{topicSuffix}";
            ros.RegisterPublisher<PoseArrayMsg>(topic);
            agents.Add(new Agent { id = kv.Key, tf = kv.Value.transform, topic = topic });
        }

        if (!string.IsNullOrEmpty(globalTopic))
            ros.RegisterPublisher<PoseArrayMsg>(globalTopic);

        Debug.Log($"[DynamicObstaclePublisher] Tracking {agents.Count} dynamic agents " +
                  $"at {publishRate} Hz (global topic '{globalTopic}').");
    }

    void Update()
    {
        if (agents.Count == 0 || publishRate <= 0f) return;

        accumulator += Time.deltaTime;
        if (accumulator < 1f / publishRate) return;
        accumulator = 0f;

        PublishAll();
    }

    void PublishAll()
    {
        // Snapshot every live agent's pose once.
        List<Agent>   live  = new List<Agent>(agents.Count);
        List<PoseMsg> poses = new List<PoseMsg>(agents.Count);
        foreach (Agent a in agents)
        {
            if (a.tf == null) continue;
            live.Add(a);
            poses.Add(ToPose(a.tf.position));
        }

        // Global: all agents (for RViz / monitoring).
        if (!string.IsNullOrEmpty(globalTopic))
            ros.Publish(globalTopic, MakeMsg(poses));

        // Per-agent: every OTHER agent (self excluded), for avoidance.
        for (int i = 0; i < live.Count; i++)
        {
            List<PoseMsg> others = new List<PoseMsg>(live.Count - 1);
            for (int j = 0; j < live.Count; j++)
                if (j != i) others.Add(poses[j]);
            ros.Publish(live[i].topic, MakeMsg(others));
        }
    }

    // Unity ground plane (x, z) -> ROS ground plane (x, y), z = up = 0.
    static PoseMsg ToPose(Vector3 p)
    {
        PoseMsg pose = new PoseMsg();
        pose.position.x    = p.x;   // Unity x -> ROS x
        pose.position.y    = p.z;   // Unity z -> ROS y
        pose.position.z    = 0f;    // up
        pose.orientation.w = 1.0;   // identity
        return pose;
    }

    PoseArrayMsg MakeMsg(List<PoseMsg> poses)
    {
        PoseArrayMsg msg = new PoseArrayMsg();
        msg.header.frame_id = frameId;
        msg.poses = poses.ToArray();
        return msg;
    }
}

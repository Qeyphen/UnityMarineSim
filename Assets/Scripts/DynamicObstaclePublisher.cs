using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

/// <summary>
/// Publishes the live positions of the moving (dynamic) boats as the "dynamic layer"
/// that complements the static /map occupancy grid.
///
/// For each agent it publishes, at <see cref="publishRate"/> Hz, a geometry_msgs/PoseArray
/// of every OTHER agent on the per-agent topic "/{id}/dynamic_obstacles" — so a boat's
/// own avoidance logic can read its topic directly without seeing itself.
///
/// Coordinate convention (same as everywhere else): position.x = Unity x,
/// position.z = Unity z, y = 0. Orientation is identity (position is what matters
/// for avoidance).
/// </summary>
public class DynamicObstaclePublisher : MonoBehaviour
{
    [Header("ROS")]
    [Tooltip("Topic is /{agentId}/{topicSuffix}.")]
    public string topicSuffix = "dynamic_obstacles";
    public string frameId     = "map";

    [Header("Rate")]
    public float publishRate = 10f;   // Hz

    private class Agent { public string id; public Transform tf; public string topic; }

    private readonly List<Agent> agents = new List<Agent>();
    private ROSConnection ros;
    private float accumulator;

    /// <summary>Registers the dynamic agents and their per-agent topics. Called by SceneBuilder.</summary>
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

        Debug.Log($"[DynamicObstaclePublisher] Tracking {agents.Count} dynamic agents " +
                  $"at {publishRate} Hz.");
    }

    void Update()
    {
        if (agents.Count == 0 || publishRate <= 0f) return;

        accumulator += Time.deltaTime;
        float interval = 1f / publishRate;
        if (accumulator < interval) return;
        accumulator = 0f;

        PublishAll();
    }

    void PublishAll()
    {
        // For each agent, publish the poses of all the OTHER agents.
        foreach (Agent self in agents)
        {
            List<PoseMsg> others = new List<PoseMsg>(agents.Count - 1);
            foreach (Agent other in agents)
            {
                if (other == self || other.tf == null) continue;
                Vector3 p = other.tf.position;
                PoseMsg pose = new PoseMsg();
                pose.position.x    = p.x;   // Unity x
                pose.position.y    = 0f;
                pose.position.z    = p.z;   // Unity z
                pose.orientation.w = 1.0;   // identity
                others.Add(pose);
            }

            PoseArrayMsg msg = new PoseArrayMsg();
            msg.header.frame_id = frameId;
            msg.poses = others.ToArray();
            ros.Publish(self.topic, msg);
        }
    }
}

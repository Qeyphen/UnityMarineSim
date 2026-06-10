using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.N3New;

/// <summary>
/// Publishes EVERY object SceneBuilder spawned (the ego boat + the static buoys) as a single
/// TrackArray on /scene/objects — a live ROS view of the authored scene: id, type, pose and
/// velocity per object. Together with /sim/tracks (the procedural traffic) this enumerates the
/// whole scene over ROS, so one can query "all objects and where they are."
///
/// (Repurposed from the old PoseArray /dynamic_obstacles publisher, which became obsolete once
///  the moving traffic started originating in ROS as /sim/tracks.)
///
/// Convention (map layer): Unity x -> ROS x, Unity z -> ROS y, Unity y = up = 0; heading from
/// the object's forward as ENU yaw about ROS z. Scene-object ids start at 9000 (logged with
/// their names) so they don't collide with the generator's track ids.
/// </summary>
public class DynamicObstaclePublisher : MonoBehaviour
{
    [Header("ROS")]
    public string topic   = "/scene/objects";
    public string frameId = "map";

    [Header("Rate")]
    public float publishRate = 10f;   // Hz

    /// <summary>One authored scene object to publish.</summary>
    private class SceneObject
    {
        public uint      id;
        public byte      type;      // n3_new_msgs/Track type constant
        public Transform tf;
        public bool      dynamic;   // compute velocity if true
        public Vector3   lastPos;
    }

    private readonly List<SceneObject> objects = new List<SceneObject>();
    private ROSConnection ros;
    private float accumulator;

    /// <summary>Register every spawned object. Called by SceneBuilder.
    /// Each tuple: (name, n3_new_msgs/Track type byte, transform, isDynamic).</summary>
    public void SetObjects(List<(string name, byte type, Transform tf, bool dynamic)> sceneObjects)
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<TrackArrayMsg>(topic);
        objects.Clear();

        uint nextId = 9000;
        foreach (var o in sceneObjects)
        {
            if (o.tf == null) continue;
            objects.Add(new SceneObject
            {
                id = nextId, type = o.type, tf = o.tf,
                dynamic = o.dynamic, lastPos = o.tf.position
            });
            Debug.Log($"[SceneObjects] id {nextId} = {o.name} (type {o.type})");
            nextId++;
        }
        Debug.Log($"[SceneObjects] Publishing {objects.Count} scene objects on '{topic}' " +
                  $"at {publishRate} Hz.");
    }

    void Update()
    {
        if (objects.Count == 0 || publishRate <= 0f) return;
        accumulator += Time.deltaTime;
        float interval = 1f / publishRate;
        if (accumulator < interval) return;
        float dt = accumulator;
        accumulator = 0f;
        Publish(dt);
    }

    void Publish(float dt)
    {
        List<TrackMsg> tracks = new List<TrackMsg>(objects.Count);
        foreach (SceneObject o in objects)
        {
            if (o.tf == null) continue;
            Vector3 p = o.tf.position;

            TrackMsg t = new TrackMsg();
            t.id   = o.id;
            t.type = o.type;
            t.pose.position.x = p.x;   // Unity x -> ROS x
            t.pose.position.y = p.z;   // Unity z -> ROS y
            t.pose.position.z = 0.0;

            Vector3 f = o.tf.forward;
            float yaw = Mathf.Atan2(f.z, f.x);   // ENU yaw (East = 0, North = +y)
            t.pose.orientation.z = Mathf.Sin(yaw / 2f);
            t.pose.orientation.w = Mathf.Cos(yaw / 2f);

            if (o.dynamic && dt > 0f)
            {
                Vector3 v = (p - o.lastPos) / dt;
                t.twist.linear.x = v.x;   // Unity x -> ROS x
                t.twist.linear.y = v.z;   // Unity z -> ROS y
            }
            o.lastPos = p;

            tracks.Add(t);
        }

        TrackArrayMsg msg = new TrackArrayMsg();
        msg.header.frame_id = frameId;
        msg.tracks = tracks.ToArray();
        ros.Publish(topic, msg);
    }
}

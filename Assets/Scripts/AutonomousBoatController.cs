using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class AutonomousBoatController
{
    private readonly string        topic;
    private readonly ROSConnection ros;

    public AutonomousBoatController(string topic)
    {
        this.topic = topic;

        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<PoseStampedMsg>(topic, OnTargetPoseReceived);

        Debug.Log($"[AutonomousBoatController] Subscribed to '{topic}'.");
    }

    private void OnTargetPoseReceived(PoseStampedMsg msg)
    {
        Debug.Log($"[AutonomousBoatController] {topic} | {msg}");
    }
}

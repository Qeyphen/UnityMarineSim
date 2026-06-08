using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

/// <summary>
/// Drives a boat toward a target pose received from the ROS target publisher.
///
/// On a new target it turns the bow toward the target first; once the heading is
/// within <see cref="headingTolerance"/> it thrusts forward (still steering, so it
/// arcs naturally); within <see cref="arrivalRadius"/> it brakes and settles.
///
/// MonoBehaviour so it can run physics in FixedUpdate and surface its <see cref="status"/>
/// in the Inspector. Topic is set via <see cref="Configure"/> (it's added at runtime by
/// BoatControlSwitcher). The force model mirrors ManualBoatController, which is proven.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class AutonomousBoatController : MonoBehaviour
{
    public enum BoatStatus { Settled, Turning, Thrusting }

    [Header("Status (runtime, read-only)")]
    public BoatStatus status = BoatStatus.Settled;

    [Header("References")]
    public Transform motor;   // stern motor; auto-pulled from ManualBoatController if unset

    [Header("Tuning")]
    public float steerPower       = 500f;
    public float power            = 5f;
    public float maxSpeed         = 10f;
    public float drag             = 0.1f;
    public float fullSteerAngle   = 30f;   // |heading error| >= this -> full steer
    public float headingTolerance = 9f;    // deg (~5% of 180) -> aligned, start thrusting
    public float arrivalRadius    = 3f;    // within this distance -> Settled

    [Header("Logging")]
    public int logInterval = 10;   // log telemetry every N FixedUpdate steps while moving

    private int          logCounter;
    private Rigidbody    rb;
    private ROSConnection ros;
    private string       topic;
    private bool         subscribed;

    private bool    hasTarget;
    private Vector3 targetPos;

    /// <summary>Sets the topic to subscribe to. Called by BoatControlSwitcher.</summary>
    public void Configure(string topic)
    {
        this.topic = topic;
        if (isActiveAndEnabled) Subscribe();
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (motor == null)
        {
            ManualBoatController manual = GetComponent<ManualBoatController>();
            if (manual != null) motor = manual.motor;
        }
    }

    void OnEnable()  { Subscribe(); }
    void OnDisable() { Unsubscribe(); status = BoatStatus.Settled; }

    void Subscribe()
    {
        if (subscribed || string.IsNullOrEmpty(topic)) return;
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<PoseStampedMsg>(topic, OnTargetPoseReceived);
        subscribed = true;
        Debug.Log($"[AutonomousBoatController] Subscribed to '{topic}'.");
    }

    void Unsubscribe()
    {
        if (!subscribed) return;
        if (ros != null) ros.Unsubscribe(topic);
        subscribed = false;
    }

    void OnTargetPoseReceived(PoseStampedMsg msg)
    {
        // Published in Unity convention: position.x = right, position.z = forward, y unused.
        targetPos = new Vector3((float)msg.pose.position.x, 0f, (float)msg.pose.position.z);
        hasTarget = true;
        Debug.Log($"[AutonomousBoatController] {topic} | new target ({targetPos.x:F1}, {targetPos.z:F1})");
    }

    void FixedUpdate()
    {
        if (rb == null || !hasTarget) return;

        Vector3 toTarget = targetPos - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (distance <= arrivalRadius)
        {
            status = BoatStatus.Settled;
            ApplyForceToReachVelocity(Vector3.zero);  // brake to a stop
            ApplyDriftCorrection();
            return;
        }

        // Heading error from current forward to the direction toward the target.
        Vector3 forwardFlat = Vector3.Scale(new Vector3(1f, 0f, 1f), transform.forward);
        float   headingError = Vector3.SignedAngle(forwardFlat, toTarget, Vector3.up);

        // Steer toward the target. Mirrors ManualBoatController: +steer turns to port,
        // so negate the error to turn toward it. Eases off as it aligns.
        float steer = Mathf.Clamp(-headingError / fullSteerAngle, -1f, 1f);
        if (motor != null)
            rb.AddForceAtPosition(
                steer * transform.right * steerPower * 0.01f,
                motor.position
            );

        // Thrust only once roughly aligned, otherwise just keep turning.
        if (Mathf.Abs(headingError) <= headingTolerance)
        {
            status = BoatStatus.Thrusting;
            Vector3 fwd = forwardFlat.sqrMagnitude > 0f ? forwardFlat.normalized : transform.forward;
            ApplyForceToReachVelocity(fwd * maxSpeed);
        }
        else
        {
            status = BoatStatus.Turning;
        }

        ApplyDriftCorrection();
        LogTelemetry(distance, headingError);
    }

    // Periodic telemetry while moving (not every frame, not when Settled).
    void LogTelemetry(float distance, float headingError)
    {
        if (logInterval <= 0) return;
        if (++logCounter < logInterval) return;
        logCounter = 0;

        Debug.Log(
            $"[AutonomousBoatController] {name} {status} | " +
            $"distance error={distance:F2} | heading error={Mathf.Abs(headingError):F1}°"
        );
    }

    void ApplyForceToReachVelocity(Vector3 targetVelocity)
    {
        Vector3 force = (targetVelocity - rb.linearVelocity) * power;
        rb.AddForce(force, ForceMode.Acceleration);
    }

    // Rotates velocity toward the heading to kill lateral sliding (same as ManualBoatController).
    void ApplyDriftCorrection()
    {
        if (rb.linearVelocity.sqrMagnitude < 0.0001f) return;

        bool    movingForward = Vector3.Dot(transform.forward, rb.linearVelocity) > 0f;
        Vector3 targetDir     = movingForward ? transform.forward : -transform.forward;
        float   angle         = Vector3.SignedAngle(rb.linearVelocity, targetDir, Vector3.up);

        rb.linearVelocity = Quaternion.AngleAxis(angle * drag, Vector3.up) * rb.linearVelocity;
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Perception.GroundTruth;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

/// <summary>
/// Records a Perception dataset on demand: it only captures while "recording" is on,
/// at a fixed real-time rate (e.g. 3 Hz). Start/stop is controlled either by a ROS
/// message or a keyboard hotkey.
///
/// Manual capture (not Perception's Scheduled mode) is deliberate: Scheduled mode fixes
/// the simulation timestep and decouples from real time, which would wreck the live
/// physics-driven boat. We let the sim run normally and snapshot it N times/second only
/// while recording.
///
/// Control:
///   ROS topic <see cref="controlTopic"/> (std_msgs/Bool): data=true starts, data=false stops.
///   Hotkey <see cref="toggleKey"/> toggles on/off.
///
/// Perception API used (UnityEngine.Perception.GroundTruth):
///   PerceptionCamera.RequestCapture();   // queues one capture for end of frame
/// Set the camera's "Capture Trigger Mode = Manual" in the Inspector (the enum name
/// varies across Perception versions, so we don't set it from code).
/// </summary>
[RequireComponent(typeof(PerceptionCamera))]
public class DatasetCaptureScheduler : MonoBehaviour
{
    [Header("Capture")]
    public float captureHz = 3f;
    [Tooltip("Recording right now? Toggle live in the Inspector, or via ROS / hotkey.")]
    public bool capturing = false;

    [Header("Control")]
    [Tooltip("ROS topic (std_msgs/Bool): true = start recording, false = stop.")]
    public string controlTopic = "/dataset/control";
    [Tooltip("Keyboard key that toggles recording on/off.")]
    public Key toggleKey = Key.R;

    [Header("Ego vessel")]
    [Tooltip("Clear the Labeling on this camera's own vessel so the boat doesn't " +
             "label its own hull as an obstacle in its captures.")]
    public bool excludeOwnVessel = true;

    private PerceptionCamera perceptionCamera;
    private ROSConnection    ros;
    private float            timer;
    private int              frameCount;

    void Awake()
    {
        perceptionCamera = GetComponent<PerceptionCamera>();
        // NOTE: set "Capture Trigger Mode = Manual" on the Perception Camera in the
        // Inspector. We don't set it here because the enum's name/namespace differs
        // between Perception versions.
        Debug.Log($"[DatasetCapture] Awake — PerceptionCamera found: {perceptionCamera != null}");
    }

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<BoolMsg>(controlTopic, OnControl);
        Debug.Log($"[DatasetCapture] Ready. ROS '{controlTopic}' (true=start/false=stop), " +
                  $"hotkey '{toggleKey}', rate {captureHz} Hz.");

        if (excludeOwnVessel)
        {
            // The Labeling sits on the boat root; this camera is a child of it.
            Labeling own = GetComponentInParent<Labeling>();
            if (own != null)
            {
                own.labels.Clear();
                own.RefreshLabeling();   // re-evaluate: no labels -> not captured by any labeler
                Debug.Log($"[DatasetCapture] Cleared ego-vessel labels on '{own.name}' (won't self-label).");
            }
        }
    }

    void OnControl(BoolMsg msg) => SetRecording(msg.data);

    void Update()
    {
        // Hotkey toggle.
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            SetRecording(!capturing);

        if (!capturing || captureHz <= 0f || perceptionCamera == null) return;

        timer += Time.deltaTime;
        float interval = 1f / captureHz;
        if (timer < interval) return;
        timer -= interval;

        perceptionCamera.RequestCapture();
        frameCount++;
    }

    void SetRecording(bool on)
    {
        if (on == capturing) return;
        capturing = on;
        timer = 0f;
        if (on)
        {
            frameCount = 0;
            Debug.Log("[DatasetCapture] ▶ START recording.");
        }
        else
        {
            Debug.Log($"[DatasetCapture] ■ STOP recording — {frameCount} frames captured.");
        }
    }
}

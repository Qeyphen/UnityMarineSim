using UnityEngine;
using UnityEngine.Perception.GroundTruth;

/// <summary>
/// Drives a PerceptionCamera to capture at a fixed real-time rate (e.g. 3 Hz) during
/// normal gameplay.
///
/// Why manual triggering instead of Perception's "Scheduled" mode: Scheduled capture
/// sets a fixed simulation delta time, which hijacks the physics timestep and decouples
/// the sim from real time — bad for a live, physics-driven boat. Manual triggering on a
/// wall-clock timer lets the sim run normally and just snapshots it N times per second.
///
/// API used (Unity Perception, namespace UnityEngine.Perception.GroundTruth):
///   PerceptionCamera.captureTriggerMode  (enum CaptureTriggerMode { Scheduled, Manual })
///   PerceptionCamera.RequestCapture()    queues a capture for the end of the current frame
/// If your Perception version names these differently, adjust here.
/// </summary>
[RequireComponent(typeof(PerceptionCamera))]
public class DatasetCaptureScheduler : MonoBehaviour
{
    [Tooltip("Captures per second.")]
    public float captureHz = 3f;

    [Tooltip("Skip capturing until the sim has run this long (lets the scene settle).")]
    public float warmupSeconds = 1f;

    private PerceptionCamera perceptionCamera;
    private float            timer;
    private float            elapsed;

    void Awake()
    {
        perceptionCamera = GetComponent<PerceptionCamera>();
        // Take control of when frames are captured.
        perceptionCamera.captureTriggerMode = CaptureTriggerMode.Manual;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed < warmupSeconds || captureHz <= 0f) return;

        timer += Time.deltaTime;
        float interval = 1f / captureHz;
        if (timer < interval) return;
        timer -= interval;

        perceptionCamera.RequestCapture();
    }
}

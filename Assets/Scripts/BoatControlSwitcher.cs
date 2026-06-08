using UnityEngine;

/// <summary>
/// Lives on a dynamic boat and decides which controller is active: Manual
/// (keyboard via ManualBoatController) or Auto (ROS target-following via
/// AutonomousBoatController).
///
/// SceneBuilder seeds <see cref="mode"/> from the scene config at spawn, but the
/// field is exposed in the Inspector and re-checked every frame — so you can
/// select the spawned boat during Play and switch modes live, overriding config.
///
/// Both controllers are components on the boat; switching just toggles which is
/// enabled. The autonomous one is added at runtime if the prefab lacks it.
/// </summary>
public class BoatControlSwitcher : MonoBehaviour
{
    [Tooltip("Active controller. Seeded from config; change at runtime to switch.")]
    public ControlMode mode = ControlMode.Manual;

    private string                  targetTopic;
    private ManualBoatController    manual;
    private AutonomousBoatController autonomous;
    private ControlMode?            applied;       // last-applied mode (null = none yet)

    /// <summary>Called by SceneBuilder right after spawn to seed the mode + topic.</summary>
    public void Configure(ControlMode initialMode, string targetTopic)
    {
        mode             = initialMode;
        this.targetTopic = targetTopic;
        if (autonomous != null) autonomous.Configure(targetTopic);
    }

    void Awake()
    {
        manual     = GetComponent<ManualBoatController>();
        autonomous = GetComponent<AutonomousBoatController>();
        if (autonomous == null)
            autonomous = gameObject.AddComponent<AutonomousBoatController>();

        // Start disabled; Apply() enables whichever the mode selects.
        autonomous.enabled = false;
    }

    void Update()
    {
        if (applied != mode) Apply();
    }

    void Apply()
    {
        bool auto = mode == ControlMode.Auto;

        if (manual != null)     manual.enabled     = !auto;
        if (autonomous != null) autonomous.enabled = auto;

        applied = mode;
        Debug.Log($"[BoatControlSwitcher] {name}: {(auto ? "AUTO" : "MANUAL")} control");
    }
}

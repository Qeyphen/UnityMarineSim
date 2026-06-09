# Headless dataset capture

Run N3moSim **without the editor / no window**, controlled entirely over ROS, to capture
Perception datasets unattended. This is the foundation for scaled (Phase 3) generation.

## How it works (why it can capture with no window)
"Headless" here means **no visible window — but the GPU still renders.** The Perception
camera renders into an off-screen buffer in GPU memory; Perception reads that buffer back
and writes images to disk. The screen/window was never the source of the images. On Linux
we give the GPU a display *context* with **Xvfb** (a virtual, in-memory framebuffer) — it
doesn't show anything, it just lets the graphics device initialise.

> Requires a **GPU** (HDRP/Perception can't render on CPU). An NVIDIA Quadro RTX 4000 (or
> similar) is plenty. `-nographics` is NOT used — we need rendering.

## Prerequisites
- Linux box with an **NVIDIA GPU + driver** installed (`nvidia-smi` works).
- **Xvfb**: `sudo apt install -y xvfb`
- The **ROS bridge** (this repo's `docker compose` stack).
- A **built Linux player** of the project (below).
- A Python venv with `pillow` for previewing (see README §9).

## 1. Build the Linux player (one-time)
In the Unity Editor:
1. **File → Build Settings** → add your capture scene to *Scenes In Build*.
2. Platform = **Linux** (Standalone / Dedicated) → **Build** → e.g. `Build/N3moSim.x86_64`.
3. Confirm in the scene before building:
   - **ROSConnectionPrefab** points at the bridge host (`127.0.0.1`, port `10000`).
   - `config/Scene.json` boat is **`"control_mode": "auto"`** (no keyboard headless — you drive it via `/agent_01/target_pose`).
   - The boat prefab has the **POVCamera + PerceptionCamera + DatasetCaptureScheduler**, Capture Trigger Mode = **Manual**.

The player reads `config/Scene.json` relative to its own folder — keep `config/` next to the
binary (or beside `..` as SceneBuilder searches), same as in the editor.

## 2. Run it
```bash
# launch headless and drive/record yourself over ROS:
./run_headless.sh Build/N3moSim.x86_64

# OR launch + auto-record 8s + stop (quick smoke test of the whole loop):
./run_headless.sh Build/N3moSim.x86_64 --record 8
```
The script: starts the bridge → launches the player under Xvfb (GPU rendering, no window) →
waits for Unity to connect → (optionally) records via `/dataset/control` → finalises the
dataset by stopping the player.

Player log (for debugging rendering): `headless_player.log`.

## 3. Control it over ROS (when not auto-recording)
```bash
# drive the boat toward the buoys (Auto mode)
docker compose exec ros_bridge bash -lc \
 "source /opt/ros/humble/setup.bash && source /root/ros2_ws/install/setup.bash && \
  ros2 launch n3mo_control target_pose.launch.py x:=-150 z:=-120"

# start / stop recording
docker compose exec ros_bridge bash -lc \
 "source /opt/ros/humble/setup.bash && ros2 topic pub --once /dataset/control std_msgs/Bool '{data: true}'"
docker compose exec ros_bridge bash -lc \
 "source /opt/ros/humble/setup.bash && ros2 topic pub --once /dataset/control std_msgs/Bool '{data: false}'"
```
Note: the boat starts at z≈−300 facing the buoys (z≈−110), so they're already in view — you
can capture a valid frame **without moving** for a first test.

## 4. Output + preview
SOLO output goes to the player's persistent data path
(`~/.config/unity3d/<Company>/<Product>/solo`). Preview:
```bash
source .venv/bin/activate
python3 tools/solo_preview.py        # auto-finds the latest SOLO dataset
```

## Troubleshooting
- **Player exits immediately / black or no captures** — check `headless_player.log`. Usually
  the GPU couldn't get a context under Xvfb.
  - Make sure `nvidia-smi` works and the driver matches the kernel.
  - Try without `-batchmode` (just run under `xvfb-run`), or set a real depth: the screen is
    `${RES}x24`.
  - Fallback to **EGL headless** (no X server at all): launch the player with
    `-force-glcore` or run on a desktop session's `DISPLAY` instead of Xvfb. EGL avoids Xvfb
    entirely but needs the NVIDIA EGL libs.
- **Unity never connects** — bridge not up, or ROSConnectionPrefab host/port wrong in the build.
- **Records but 0 frames** — Perception Camera not set to **Manual**, or the scene/boat
  doesn't carry the DatasetCaptureScheduler in the built scene.
- **No GPU at all** — won't work; HDRP requires a GPU.

## Next
Once a single headless `--record` run produces correct frames, Phase 3 scales this: a mission
node loops over randomized scenarios, recording each, fully unattended.

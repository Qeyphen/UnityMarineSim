# N3moSim

Unity marine simulator with a ROS 2 bridge. Boats are spawned from a JSON scene,
driven either manually (keyboard) or autonomously (toward a ROS target pose), and
the scene is exposed to ROS 2 as an occupancy grid (`/map`) plus live dynamic
obstacles (`/dynamic_obstacles`).

```
Unity  ──TCP(10000)──►  ros_tcp_endpoint (Docker)  ──DDS──►  ROS 2 nodes / RViz / CLI
```

Unity never speaks ROS directly — it talks to the `ros_bridge` container, which is
the real ROS 2 node that owns all publishers/subscribers.

---

## 1. Prerequisites

- **Docker** (Docker Desktop on macOS, Docker Engine on Linux).
- **Unity** with the project open (ROS-TCP-Connector package already included).
- The Unity **ROSConnectionPrefab** in the scene, pointing at `127.0.0.1` port `10000`.

---

## 2. Start / stop the ROS side (Docker)

```bash
docker compose build          # first time, or after editing the Dockerfile / ROS package
docker compose up -d          # start the bridge (port 10000)
docker compose ps             # check it's running
docker compose logs -f ros_bridge   # follow bridge logs
docker compose down           # stop everything
```

Then press **Play** in Unity. The bridge and Unity connect automatically.

> If you change the Python ROS node, rebuild it without a full image rebuild:
> ```bash
> docker compose exec ros_bridge bash -lc \
>   "cd /root/ros2_ws && source /opt/ros/humble/setup.bash && colcon build --packages-select n3mo_control"
> ```

---

## 3. Topics

| Topic | Type | Direction | Notes |
|---|---|---|---|
| `/agent_01/target_pose` | `geometry_msgs/PoseStamped` | → Unity | goal for the boat; published on demand |
| `/map` | `nav_msgs/OccupancyGrid` | Unity → | static obstacles (buoys); latched, sent once |
| `/dynamic_obstacles` | `geometry_msgs/PoseArray` | Unity → | ALL moving boats; 10 Hz |
| `/{id}/dynamic_obstacles` | `geometry_msgs/PoseArray` | Unity → | other boats (self-excluded); 10 Hz |

**Coordinate conventions (important):**
- Control channel (`target_pose`): `position.x` = Unity x, `position.z` = Unity z, `y = 0`.
- Map layer (`/map`, dynamic obstacles): ROS ground plane = XY, so Unity x → ROS x,
  **Unity z → ROS y**, ROS z = 0.

---

## 4. Send a target pose (autonomous control)

Set `agent_01` to **Auto** (in `config/Scene.json` `"control_mode": "auto"`, or live in the
boat's `BoatControlSwitcher` Inspector), then publish a target. `x`/`z` are Unity
coordinates; the publisher sends once and exits.

```bash
docker compose exec ros_bridge bash -lc \
 "source /opt/ros/humble/setup.bash && source /root/ros2_ws/install/setup.bash && \
  ros2 launch n3mo_control target_pose.launch.py x:=-190.0 z:=-110.0"
```

Other options:
```bash
# pick a different agent's topic
docker compose exec ros_bridge bash -lc \
 "source /opt/ros/humble/setup.bash && source /root/ros2_ws/install/setup.bash && \
  ros2 launch n3mo_control target_pose.launch.py topic:=/agent_02/target_pose x:=10 z:=25"

# run the node directly instead of the launch file
docker compose exec ros_bridge bash -lc \
 "source /opt/ros/humble/setup.bash && source /root/ros2_ws/install/setup.bash && \
  ros2 run n3mo_control target_pose_publisher --ros-args -p x:=-190.0 -p z:=-110.0"
```

The boat turns toward the target, then thrusts; watch its `AutonomousBoatController.Status`
in the Inspector cycle **Turning → Thrusting → Settled**.

---

## 5. Inspect topics (CLI)

These use only standard message types, so they need just the base ROS source.

```bash
# what's being published
docker compose exec ros_bridge bash -lc \
 "source /opt/ros/humble/setup.bash && ros2 topic list"

# watch target poses
docker compose exec ros_bridge bash -lc \
 "source /opt/ros/humble/setup.bash && ros2 topic echo /agent_01/target_pose"

# watch live boat positions
docker compose exec ros_bridge bash -lc \
 "source /opt/ros/humble/setup.bash && ros2 topic echo /dynamic_obstacles"

# map metadata only (don't echo the huge data array); latched, so --once returns it
docker compose exec ros_bridge bash -lc \
 "source /opt/ros/humble/setup.bash && ros2 topic echo /map --field info --once"

# publish rate
docker compose exec ros_bridge bash -lc \
 "source /opt/ros/humble/setup.bash && ros2 topic hz /dynamic_obstacles"
```

> **Tip — shell shortcut.** Add this to `~/.zshrc` (or `~/.bashrc`) to skip the boilerplate.
> It sources both setups, which works for every command (the workspace overlay re-exposes
> base ROS):
> ```bash
> dros() { docker exec -it n3mo_bridge bash -lc \
>   "source /opt/ros/humble/setup.bash && source /root/ros2_ws/install/setup.bash && ros2 $*"; }
> ```
> Then: `dros topic list`, `dros topic echo /dynamic_obstacles`,
> `dros launch n3mo_control target_pose.launch.py x:=-190 z:=-110`.

---

## 6. View the occupancy grid

### Terminal (ASCII) — quickest, headless
```bash
docker compose exec ros_bridge bash -lc \
 "source /opt/ros/humble/setup.bash && python3 /root/ros2_ws/src/n3mo_control/tools/view_map.py"
```
`#` = occupied, `.` = free.

### Save to an image
```bash
docker compose exec ros_bridge bash -lc \
 "source /opt/ros/humble/setup.bash && python3 /root/ros2_ws/src/n3mo_control/tools/save_map.py"
```
Writes `recordings/map.png` + `recordings/map.yaml` (host-visible). Open the PNG in any
image viewer.

---

## 7. RViz2 (live map of static + dynamic objects)

Opt-in GUI service (profile `gui`). Shows buoys as grid cells and boats as live arrows.
Preloaded config: `config/n3mo.rviz`.

### Linux
```bash
xhost +local:docker                      # allow containers to use your X server (per session)
docker compose up -d                     # bridge
docker compose --profile gui up rviz     # RViz window opens
```

### macOS (needs XQuartz)
```bash
brew install --cask xquartz
open -a XQuartz                          # Settings → Security → ✅ "Allow connections from network clients", then reopen
xhost + 127.0.0.1
export DISPLAY=host.docker.internal:0
docker compose --profile gui up rviz
```

In RViz the Fixed Frame is `map`; the **Map** (`/map`) and **PoseArray**
(`/dynamic_obstacles`) displays are already added. Press **Play** in Unity first so the
topics exist.

Stop RViz: `docker compose --profile gui down rviz` (or Ctrl-C).

---

## 8. Control modes

Each dynamic boat picks its controller via `config/Scene.json` `control_mode`
(`"manual"` | `"auto"`), and you can switch it live in the boat's `BoatControlSwitcher`
Inspector while playing.

- **Manual** — keyboard: `W` forward, `S` back, `A`/`D` steer.
- **Auto** — follows `/{id}/target_pose` (see §4).

---

## 9. Layout

```
config/Scene.json         scene definition (objects, positions, control_mode)
config/n3mo.rviz          preloaded RViz layout
Assets/Scripts/           Unity controllers + publishers
  SceneBuilder.cs           spawns objects, wires controllers + map publishers
  ManualBoatController.cs    keyboard control
  AutonomousBoatController.cs ROS target-following control
  BoatControlSwitcher.cs     manual/auto switch (per boat)
  OccupancyGridPublisher.cs  static /map
  DynamicObstaclePublisher.cs live /dynamic_obstacles
ros2_ws/src/n3mo_control/  ROS 2 package (target_pose_publisher, tools/)
docker-compose.yml         ros_bridge (+ opt-in rviz)
Dockerfile                 ROS 2 Humble + ROS-TCP-Endpoint (+ patches)
```

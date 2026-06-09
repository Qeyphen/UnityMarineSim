#!/usr/bin/env bash
#
# Run N3moSim headless: no window, but the GPU still renders (via Xvfb) so Perception
# can capture. Optionally records a short dataset to validate the whole loop end to end.
#
# See doc/headless.md for full setup (built Linux player, NVIDIA driver, xvfb, bridge).
#
# Usage:
#   ./run_headless.sh /path/to/N3moSim.x86_64               # launch headless; control over ROS yourself
#   ./run_headless.sh /path/to/N3moSim.x86_64 --record 8    # launch, record 8s, stop, finalize dataset
#
# Env overrides:  RES=1280x720  BRIDGE_SVC=ros_bridge
set -euo pipefail

PLAYER="${1:-./Build/N3moSim.x86_64}"
RECORD_SECONDS=0
[[ "${2:-}" == "--record" ]] && RECORD_SECONDS="${3:-8}"

RES="${RES:-1280x720}"
BRIDGE_SVC="${BRIDGE_SVC:-ros_bridge}"
CONTROL_TOPIC="/dataset/control"
LOG="$(pwd)/headless_player.log"

# ---- preflight ----
[[ -x "$PLAYER" ]]            || { echo "[headless] player not found/executable: $PLAYER"; exit 1; }
command -v xvfb-run >/dev/null || { echo "[headless] xvfb-run missing -> sudo apt install -y xvfb"; exit 1; }
command -v docker   >/dev/null || { echo "[headless] docker missing"; exit 1; }

ros2_control() {   # publish a std_msgs/Bool to the control topic, inside the bridge container
  docker compose exec -T "$BRIDGE_SVC" bash -lc \
    "source /opt/ros/humble/setup.bash && ros2 topic pub --once $CONTROL_TOPIC std_msgs/Bool '{data: $1}'" >/dev/null
}

# ---- 1. bridge ----
echo "[headless] starting ROS bridge ($BRIDGE_SVC)..."
docker compose up -d "$BRIDGE_SVC"

# ---- 2. launch player headless (batchmode + Xvfb, GPU renders, NO -nographics) ----
echo "[headless] launching player under Xvfb at $RES (log: $LOG)..."
xvfb-run -a -s "-screen 0 ${RES}x24" \
  "$PLAYER" -batchmode -screen-fullscreen 0 \
  -screen-width "${RES%x*}" -screen-height "${RES#*x}" -logFile "$LOG" &
PLAYER_PID=$!
echo "[headless] player PID $PLAYER_PID"

# ---- 3. wait for Unity to connect to the bridge ----
echo "[headless] waiting for Unity to connect to the bridge..."
for _ in $(seq 1 40); do
  docker compose logs "$BRIDGE_SVC" 2>/dev/null | grep -q "Connection from" && { echo "[headless] connected."; break; }
  kill -0 "$PLAYER_PID" 2>/dev/null || { echo "[headless] player exited early — check $LOG"; exit 1; }
  sleep 1
done
sleep 3   # let the scene load + the /dataset/control subscription register

# ---- 4. optional record ----
if [[ "$RECORD_SECONDS" -gt 0 ]]; then
  echo "[headless] recording ${RECORD_SECONDS}s..."
  ros2_control true
  sleep "$RECORD_SECONDS"
  ros2_control false
  echo "[headless] stopping player to finalize the dataset..."
  kill -TERM "$PLAYER_PID" 2>/dev/null || true
  wait "$PLAYER_PID" 2>/dev/null || true
  echo "[headless] done. Preview the captures:"
  echo "    source .venv/bin/activate && python3 tools/solo_preview.py"
else
  echo "[headless] running headless (PID $PLAYER_PID). Control it over ROS, e.g.:"
  echo "    docker compose exec $BRIDGE_SVC bash -lc \"source /opt/ros/humble/setup.bash && ros2 topic pub --once $CONTROL_TOPIC std_msgs/Bool '{data: true}'\""
  echo "    # ...later: {data: false}, then  kill $PLAYER_PID"
fi

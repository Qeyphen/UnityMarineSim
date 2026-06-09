"""Minimal TrackArray -> MarkerArray converter for RViz.

Self-contained: no geo / projector / map-origin dependencies (unlike
track_foxglove_converter). One colored CUBE marker per track at its pose, with
stale tracks deleted. Publishes on SIM_TRACKS_MARKERS so RViz can show the traffic
straight from ROS.
"""

from __future__ import annotations

import n3_common.ros as ros
import rclpy
from n3_common.topics.sim_topics import SIM_TRACKS, SIM_TRACKS_MARKERS
from n3_common.topics.topics_model import RELIABLE_QOS
from rclpy.node import Node

# track type -> (r, g, b)
_COLORS: dict[int, tuple[float, float, float]] = {
    0: (0.8, 0.8, 0.8),   # unknown
    1: (0.2, 0.6, 1.0),   # sailboat
    2: (1.0, 0.5, 0.0),   # motorboat
    3: (1.0, 0.1, 0.1),   # jetski
    4: (0.2, 0.9, 0.3),   # kayak
    5: (0.0, 0.7, 0.7),   # paddleboard
    6: (1.0, 0.1, 0.8),   # swimmer
    7: (0.1, 0.9, 0.9),   # dinghy
    8: (0.6, 0.4, 0.2),   # fishing_boat
    9: (0.6, 0.2, 0.9),   # ferry
    10: (0.3, 0.3, 0.3),  # cargo
    11: (1.0, 1.0, 0.0),  # buoy
    12: (0.5, 0.5, 0.5),  # debris
    13: (1.0, 0.4, 0.7),  # windsurf
    14: (0.6, 1.0, 0.3),  # kitesurf
    15: (0.4, 0.7, 1.0),  # pedalo
}
_DEFAULT_COLOR = (0.8, 0.8, 0.8)


class TracksMarkersNode(Node):
    def __init__(self) -> None:
        super().__init__("tracks_markers_node")
        self.pub = self.create_publisher(
            ros.MarkerArray, SIM_TRACKS_MARKERS.name, RELIABLE_QOS
        )
        self.create_subscription(
            ros.TrackArray, SIM_TRACKS.name, self.on_tracks, SIM_TRACKS.qos
        )
        self.prev_ids: set[int] = set()
        self.get_logger().info(
            f"tracks_markers_node ready ({SIM_TRACKS.name} -> {SIM_TRACKS_MARKERS.name})"
        )

    def on_tracks(self, msg: ros.TrackArray) -> None:
        markers = []
        ids: set[int] = set()
        for t in msg.tracks:
            ids.add(t.id)
            m = ros.Marker()
            m.header = msg.header
            m.ns = "tracks"
            m.id = int(t.id)
            m.type = ros.Marker.CUBE
            m.action = ros.Marker.ADD
            m.pose = t.pose
            m.scale.x, m.scale.y, m.scale.z = 4.0, 2.0, 2.0
            r, g, b = _COLORS.get(t.type, _DEFAULT_COLOR)
            m.color.r, m.color.g, m.color.b, m.color.a = r, g, b, 1.0
            markers.append(m)

        # Delete markers for tracks that disappeared.
        for old in self.prev_ids - ids:
            d = ros.Marker()
            d.header = msg.header
            d.ns = "tracks"
            d.id = int(old)
            d.action = ros.Marker.DELETE
            markers.append(d)
        self.prev_ids = ids

        self.pub.publish(ros.MarkerArray(markers=markers))


def main() -> None:
    rclpy.init()
    node = TracksMarkersNode()
    try:
        rclpy.spin(node)
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Subscribe to /map (latched) and save it as a PNG + ROS map .yaml under /recordings.

Run inside the bridge container:
  python3 /root/ros2_ws/src/n3mo_control/tools/save_map.py
Outputs (host-visible via the ./recordings mount):
  recordings/map.png   grayscale image (occupied=black, free=white, unknown=grey)
  recordings/map.yaml  standard ROS map metadata (resolution + origin)

Pure stdlib (zlib) PNG encoder — no Pillow/nav2 required.
"""

import struct
import sys
import zlib

import rclpy
from rclpy.node import Node
from rclpy.qos import (QoSProfile, QoSDurabilityPolicy,
                       QoSReliabilityPolicy, QoSHistoryPolicy)
from nav_msgs.msg import OccupancyGrid

OUT_PNG  = "/recordings/map.png"
OUT_YAML = "/recordings/map.yaml"

# ROS map_server greyscale convention.
OCCUPIED = 0      # black
FREE     = 254    # white
UNKNOWN  = 205    # grey


def write_png(path, width, height, gray_rows):
    """gray_rows: list of bytes objects, one per image row (top to bottom)."""
    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data +
                struct.pack(">I", zlib.crc32(tag + data) & 0xffffffff))

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 0, 0, 0, 0)  # 8-bit grayscale
    raw = bytearray()
    for row in gray_rows:
        raw.append(0)          # filter type 0 (none)
        raw.extend(row)
    png = (b"\x89PNG\r\n\x1a\n" +
           chunk(b"IHDR", ihdr) +
           chunk(b"IDAT", zlib.compress(bytes(raw), 9)) +
           chunk(b"IEND", b""))
    with open(path, "wb") as f:
        f.write(png)


class MapSaver(Node):
    def __init__(self):
        super().__init__('map_saver')
        qos = QoSProfile(
            depth=1,
            history=QoSHistoryPolicy.KEEP_LAST,
            durability=QoSDurabilityPolicy.TRANSIENT_LOCAL,
            reliability=QoSReliabilityPolicy.RELIABLE,
        )
        self.create_subscription(OccupancyGrid, '/map', self.on_map, qos)
        self.got = False

    def on_map(self, msg):
        self.got = True
        w, h, data = msg.info.width, msg.info.height, msg.data

        # Build rows top-down (row 0 of the grid is the origin / bottom).
        rows = []
        for y in range(h - 1, -1, -1):
            base = y * w
            row = bytearray(w)
            for x in range(w):
                v = data[base + x]
                row[x] = OCCUPIED if v >= 50 else (UNKNOWN if v < 0 else FREE)
            rows.append(bytes(row))
        write_png(OUT_PNG, w, h, rows)

        o = msg.info.origin.position
        with open(OUT_YAML, "w") as f:
            f.write(
                f"image: map.png\n"
                f"resolution: {msg.info.resolution}\n"
                f"origin: [{o.x}, {o.y}, 0.0]\n"
                f"negate: 0\noccupied_thresh: 0.65\nfree_thresh: 0.196\n"
            )

        occupied = sum(1 for v in data if v >= 50)
        self.get_logger().info(
            f"Saved {w}x{h} map -> {OUT_PNG} ({occupied} occupied cells).")


def main():
    rclpy.init()
    node = MapSaver()
    deadline = node.get_clock().now().nanoseconds + 15_000_000_000
    while rclpy.ok() and not node.got and node.get_clock().now().nanoseconds < deadline:
        rclpy.spin_once(node, timeout_sec=0.2)
    if not node.got:
        print("No /map received within 15s — is Unity running and publishing?",
              file=sys.stderr)
    node.destroy_node()
    rclpy.shutdown()


if __name__ == '__main__':
    main()

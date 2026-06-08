#!/usr/bin/env python3
"""Publishes a target pose for an agent on /<agent>/target_pose, once, then exits.

This is the first node in the control chain: it emits a goal pose that the
ROS-TCP-Endpoint relays into Unity, where a C# subscriber reads it and drives
the boat controller toward it.

The target is expressed in Unity's horizontal plane: x (right) and z (forward),
with y (up) fixed at 0. Orientation is left identity — the boat's heading is
derived on the Unity side from (target - current) position. This keeps the goal
a pure 2D point on the water surface.

Run it on demand (Unity already in Auto mode, so its subscriber exists):
the node waits briefly for a subscriber, publishes the target once, lets the
reliable delivery flush, then shuts down. The short wait + settle is what makes
a single publish actually reach Unity before the process exits.
"""

import time

import rclpy
from rclpy.node import Node
from rclpy.qos import QoSProfile, QoSDurabilityPolicy, QoSReliabilityPolicy
from geometry_msgs.msg import PoseStamped


class TargetPosePublisher(Node):
    def __init__(self):
        super().__init__('target_pose_publisher')

        self.declare_parameter('topic', '/agent_01/target_pose')
        self.declare_parameter('frame_id', 'map')
        self.declare_parameter('x', 0.0)               # Unity right
        self.declare_parameter('z', 0.0)               # Unity forward
        self.declare_parameter('wait_timeout', 5.0)    # s — wait for a subscriber
        self.declare_parameter('settle_time', 0.5)     # s — let delivery flush

        self.topic = self.get_parameter('topic').value

        qos = QoSProfile(
            depth=1,
            durability=QoSDurabilityPolicy.TRANSIENT_LOCAL,
            reliability=QoSReliabilityPolicy.RELIABLE,
        )
        self.publisher = self.create_publisher(PoseStamped, self.topic, qos)

    def publish_once(self):
        # Wait for a subscriber (the ROS-TCP endpoint, on Unity's behalf) so the
        # one-shot isn't published into the void.
        timeout = float(self.get_parameter('wait_timeout').value)
        waited = 0.0
        while self.count_subscribers(self.topic) == 0 and waited < timeout:
            time.sleep(0.1)
            waited += 0.1

        subs = self.count_subscribers(self.topic)
        if subs == 0:
            self.get_logger().warn(
                f"No subscriber on '{self.topic}' after {timeout:.1f}s — "
                f"publishing anyway; Unity may miss it (is it in Auto mode?)."
            )

        x = float(self.get_parameter('x').value)
        z = float(self.get_parameter('z').value)

        msg = PoseStamped()
        msg.header.stamp = self.get_clock().now().to_msg()
        msg.header.frame_id = self.get_parameter('frame_id').value
        msg.pose.position.x = x
        msg.pose.position.y = 0.0  # up — water surface
        msg.pose.position.z = z
        msg.pose.orientation.w = 1.0  # identity; heading derived on Unity side

        self.publisher.publish(msg)
        self.get_logger().info(
            f"Published target pose once on '{self.topic}': "
            f"x={x}, z={z} (subscribers={subs})."
        )

        # Give the reliable middleware time to deliver before the process exits.
        time.sleep(float(self.get_parameter('settle_time').value))


def main(args=None):
    rclpy.init(args=args)
    node = TargetPosePublisher()
    try:
        node.publish_once()
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()

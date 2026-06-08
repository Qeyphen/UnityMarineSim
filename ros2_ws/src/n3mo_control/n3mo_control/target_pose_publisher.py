#!/usr/bin/env python3
"""Publishes a target pose for an agent on /<agent>/target_pose.

This is the first node in the control chain: it emits a goal pose that the
ROS-TCP-Endpoint relays into Unity, where a C# subscriber reads it and drives
the boat controller toward it.

The target is expressed in Unity's horizontal plane: x (right) and z (forward),
with y (up) fixed at 0. Orientation is left identity — the boat's heading is
derived on the Unity side from (target - current) position. This keeps the goal
a pure 2D point on the water surface.

The same target is re-published at a low rate (not just once) so a late-joining
subscriber still receives it. This is required because the Unity ROS-TCP-Connector
always subscribes with default (volatile) QoS and cannot pick up a latched sample,
so order-independent delivery has to come from periodic re-publishing.
"""

import rclpy
from rclpy.node import Node
from rclpy.qos import QoSProfile, QoSDurabilityPolicy, QoSReliabilityPolicy
from geometry_msgs.msg import PoseStamped


class TargetPosePublisher(Node):
    def __init__(self):
        super().__init__('target_pose_publisher')

        self.declare_parameter('topic', '/agent_01/target_pose')
        self.declare_parameter('frame_id', 'map')
        self.declare_parameter('publish_rate', 2.0)    # Hz — re-publish the target
        self.declare_parameter('x', 0.0)               # Unity right
        self.declare_parameter('z', 0.0)               # Unity forward

        self.topic = self.get_parameter('topic').value
        rate = float(self.get_parameter('publish_rate').value)

        # TRANSIENT_LOCAL still helps native ROS subscribers; the periodic
        # re-publish below is what covers the Unity (volatile) subscriber.
        qos = QoSProfile(
            depth=1,
            durability=QoSDurabilityPolicy.TRANSIENT_LOCAL,
            reliability=QoSReliabilityPolicy.RELIABLE,
        )
        self.publisher = self.create_publisher(PoseStamped, self.topic, qos)
        self.timer = self.create_timer(1.0 / rate, self._publish_target)

        self.get_logger().info(
            f"Re-publishing target pose on '{self.topic}' at {rate:.1f} Hz."
        )

    def _publish_target(self):
        msg = PoseStamped()
        msg.header.stamp = self.get_clock().now().to_msg()
        msg.header.frame_id = self.get_parameter('frame_id').value

        msg.pose.position.x = float(self.get_parameter('x').value)
        msg.pose.position.y = 0.0  # up — water surface
        msg.pose.position.z = float(self.get_parameter('z').value)

        # Identity orientation; heading is derived on the Unity side.
        msg.pose.orientation.w = 1.0

        self.publisher.publish(msg)


def main(args=None):
    rclpy.init(args=args)
    node = TargetPosePublisher()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()

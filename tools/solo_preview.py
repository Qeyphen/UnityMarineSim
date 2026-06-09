#!/usr/bin/env python3
"""Overlay 2D bounding boxes from a Unity Perception SOLO dataset onto its RGB frames,
so you can eyeball label quality.

Setup:  pip install pillow
Usage:
  python3 solo_preview.py /path/to/solo              # all sequences under the SOLO root
  python3 solo_preview.py /path/to/solo/sequence.0   # a single sequence

Writes annotated images next to each frame, in a `preview/` subfolder.
Red rectangle = 2D bbox, yellow text = labelName.
"""

import glob
import json
import os
import sys

from PIL import Image, ImageDraw

BBOX2D = "BoundingBox2DAnnotation"


def process_frame(frame_json_path):
    with open(frame_json_path) as f:
        data = json.load(f)
    base = os.path.dirname(frame_json_path)
    out_dir = os.path.join(base, "preview")
    os.makedirs(out_dir, exist_ok=True)

    for cap in data.get("captures", []):
        rgb_name = cap.get("filename")
        if not rgb_name:
            continue
        rgb_path = os.path.join(base, rgb_name)
        if not os.path.exists(rgb_path):
            print(f"  ! missing RGB {rgb_path}")
            continue

        img = Image.open(rgb_path).convert("RGB")
        draw = ImageDraw.Draw(img)

        boxes = []
        for ann in cap.get("annotations", []):
            if BBOX2D in ann.get("@type", ""):
                boxes = ann.get("values", [])
                break

        for b in boxes:
            x, y = b["origin"]
            w, h = b["dimension"]
            draw.rectangle([x, y, x + w, y + h], outline=(255, 0, 0), width=3)
            draw.text((x + 3, y + 3), b.get("labelName", ""), fill=(255, 255, 0))

        out_path = os.path.join(out_dir, os.path.basename(rgb_name))
        img.save(out_path)
        print(f"  wrote {out_path}  ({len(boxes)} boxes)")


def main():
    if len(sys.argv) < 2:
        print("usage: solo_preview.py <solo_or_sequence_dir>")
        return
    root = sys.argv[1]
    frames = glob.glob(os.path.join(root, "**", "*frame_data.json"), recursive=True)
    if not frames:
        print(f"no *frame_data.json found under {root}")
        return
    print(f"found {len(frames)} frame(s)")
    for fp in sorted(frames):
        print(os.path.relpath(fp, root))
        process_frame(fp)


if __name__ == "__main__":
    main()

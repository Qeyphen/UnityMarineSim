#!/usr/bin/env python3
"""Overlay 2D bounding boxes from a Unity Perception SOLO dataset onto its RGB frames,
so you can eyeball label quality.

Setup:  pip install pillow
Usage:
  python3 solo_preview.py                  # auto-find the latest SOLO dataset
  python3 solo_preview.py /path/to/solo    # a specific SOLO root or sequence

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


def find_latest_solo():
    """Find the most recently written SOLO dataset under Unity's persistent data path."""
    home = os.path.expanduser("~")
    patterns = [
        os.path.join(home, ".config/unity3d/*/*/solo*"),               # Linux
        os.path.join(home, "Library/Application Support/*/*/solo*"),   # macOS
    ]
    dirs = [d for pat in patterns for d in glob.glob(pat) if os.path.isdir(d)]
    return max(dirs, key=os.path.getmtime) if dirs else None


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else find_latest_solo()
    if not root:
        print("No SOLO dataset found automatically. Pass the path: solo_preview.py <dir>")
        return

    frames = glob.glob(os.path.join(root, "**", "*frame_data.json"), recursive=True)
    if not frames:
        print(f"no *frame_data.json found under {root}")
        return
    print(f"dataset: {root}\nfound {len(frames)} frame(s)")
    for fp in sorted(frames):
        print(os.path.relpath(fp, root))
        process_frame(fp)
    print("done — open the 'preview/' subfolder(s) to view annotated frames.")


if __name__ == "__main__":
    main()

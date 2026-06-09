# Synthetic Perception Dataset — TODO

## Goal
Generate a **production-grade synthetic labeled perception dataset** from the Unity marine
sim to train an ML model for **obstacle detection / segmentation on an autonomous sailboat**,
with **sim-to-real transfer** in mind. The sim's ground truth gives perfect, automatic labels
(2D/3D boxes + instance/semantic segmentation + depth) from the boat's own camera POV, recorded
on demand and packaged for training.

---

## Done
- [x] **Phase 0 — Spike.** Verified Unity Perception works on HDRP / Unity 6.3 (boxes +
      instance + semantic + EXR depth all render correctly).
- [x] **Phase 1 — Vertical slice.** Boat-POV Perception camera; on-demand recording via ROS
      topic `/dataset/control` (std_msgs/Bool), `R` hotkey, or Inspector toggle; captures RGB +
      2D/3D boxes + instance/semantic seg + depth at 3 Hz; `tools/solo_preview.py` overlays
      boxes to verify labels. *(on `main`)*

---

## Phase 2 — Enrich the labels   *(WIP parked on branch `phase-2`)*
- [ ] Ego-vessel exclusion — stop the boat labeling its own hull
- [ ] Water + sky semantic classes (full set: water / sky / static_obstacle / dynamic_obstacle)
- [ ] Per-object metadata: range, bearing, relative velocity
- [ ] Camera intrinsics + extrinsics into each frame record
- [ ] Depth sanity — confirm EXR values are metres; add depth view to preview tool

## Phase 3 — Scale + realism (domain randomization)
- [ ] Randomize: sea state/waves, time-of-day/sun, fog/weather, water color
- [ ] Randomize: obstacle types/counts/positions, camera jitter, sensor noise
- [ ] Seeded for reproducibility
- [ ] Headless batch generation (`-batchmode`) — thousands of frames across scenarios

## Phase 4 — ROS temporal layer + packaging
- [ ] Record rosbag (poses, controls, agents) synced to frames via `/dataset/frame` marker
- [ ] Package each session: images + masks + depth + rosbag + manifest → versioned zip

## Phase 5 — Export + dataset hygiene
- [ ] Exporters: SOLO → COCO/YOLO (detection), palette PNG (seg), depth tensors, npz/WebDataset
- [ ] Leakage-free train/val/test splits (split by **scenario**, not frame)
- [ ] Dataset manifest / data card
- [ ] QC: class balance, label-overlay audits

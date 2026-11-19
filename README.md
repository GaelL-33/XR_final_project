# Pocket Labyrinthe — Le passage des lanternes

A complete single-level VR spatial puzzle demo for **Meta Quest 3** built with **Unity 6000.3.22f1**, **URP 17.3.0**, **OpenXR 1.16.1**, and **XR Interaction Toolkit 3.7.0**.

The player explores a 15 m × 15 m starry night stone maze while holding a synchronized **1:100 miniature** above the left palm. Changes on the miniature transform full-scale architecture, and discoveries at full scale unlock new mechanisms on the miniature.

## Main Scene

- **Launch scene:** `Assets/Scenes/SampleScene.unity` (configured in `ProjectSettings/EditorBuildSettings.asset` and ready from a fresh checkout; make sure Git LFS assets are checked out with `git lfs pull` so the maze FBX/PNG meshes and XRI Starter Assets models are present).
- **Authored maze definition:** `Assets/Data/Mazes/PassageDesLanternesDefinition.asset`.

## Controls (Meta Quest 3 Touch / Touch Plus Controllers)

| Action | Control |
| --- | --- |
| **Teleport** | Thumbstick forward + release on illuminated floor zones (smooth locomotion disabled for comfort) |
| **Snap Turn** | Thumbstick left / right (continuous turn disabled) |
| **Consult 1:100 Miniature** | Hold **Left Controller** palm-up (local pose offset & `-90°` roll are serialized on `XRControllerMiniatureInteraction`) |
| **Operate Miniature Gate / Partition (Ray)** | Aim **Right Controller** curved blue pointer at a miniature target and press **B** (`secondaryButton`) |
| **Slide Atrium Partition (Direct Grab)** | Reach with **Right Controller** onto the unlocked miniature brass handle, hold **Grip** (`Select`), slide North/South between snap detents, and release |
| **Grab & Dock Lantern** | Hold **Grip** (`Select`) with the right hand on the lantern; place into any of the 36 bay niches (`XRSocketInteractor`) |
| **Recall Lantern** | Press the **"Rappeler"** button on the Vestibule pedestal (automatic out-of-bounds recovery is also active) |
| **Pull Full-Size Exit Lever** | Reach and pull down with **Grip** (`Select`) or click via far ray on the Atrium Far Side lever |
| **Optional Escalating Hint (3 tiers)** | Press **A** (`primaryButton`) on the right controller, or aim at the miniature corner **Hint Orb** + **B** |
| **Replay** | Step into the unlocked Exit Sanctuary and press the **"Recommencer"** pedestal button |

## Player Journey Overview

1. **Corridor Tutorial (`intro_gate`):** Match the carved golden triple-diamond emblem on the closed wall to its miniature counterpart, lower it from the miniature, cross the threshold while watching the live player marker, and glimpse the sealed **Porte de l'Aube** exit landmark to the east.
2. **Lantern Gallery (6 Bays × 6 Niches = 36 Sockets):** Pick up the portable lantern in the Vestibule and explore six distinguishable bays (`I • Croissant`, `II • Pilier`, `III • Étoile`, `IV • Sablier`, `V • Arche`, `VI • Cadran`). Use the overhead miniature to deduce that the Atrium rail terminates behind **Bay V (Arche)**. Bringing the lantern near Bay V reveals a glowing carved stone seam leading to the top-right niche before committing. Docking solves the gallery, retracts `atrium_lock`, and reveals the grab handle on the miniature partition.
3. **Atrium Sliding Partition:** A single sliding wall with one arched opening connects either **Entry ↔ Central Island** or **Central Island ↔ Far Side**, while a fixed barrier blocks direct Entry ↔ Far Side travel. Explicit zone teleport filters and a doorway travel safety band prevent crossing closed/moving routes or moving the wall onto the player.
4. **Return & Finale:** Pull the full-size lever on the Far Side to unlock the **Porte de l'Aube** seen at spawn, trigger a spatialized lighting and sound cascade across the solved route at both scales, reach the Exit Sanctuary completion board, and optionally reset the entire level with **"Recommencer"**.

## Opening, Validating, and Regenerating in Unity

1. Ensure Git LFS objects are materialized (`git lfs pull`).
2. Open the repository in **Unity 6000.3.22f1** and load `Assets/Scenes/SampleScene.unity`.
3. To verify all scene references, 36 sockets, teleport zone filters, partition wiring, and Quest lighting rules in Edit Mode:
   - Run **Pocket Labyrinthe > Validate Demo Scene**.
4. To cleanly regenerate `Assets/Scenes/SampleScene.unity`, modular prefabs under `Assets/Prefabs/`, materials under `Assets/Art/`, and `PassageDesLanternesDefinition.asset`:
   - Run **Pocket Labyrinthe > Generate Demo Scene (Le passage des lanternes)**.
   - Regeneration only updates and saves `SampleScene.unity`; it never starts a player build.
5. For Meta Quest 3 builds, use Unity's existing **Android build profile Build and Run UI** as documented in `Docs/DevelopmentWorkflow.md`.

## Asset Credits & Licenses

- **Maze modular meshes & palette (`Assets/Maze/`):** Built with **Imphenzia PixPal Palette v1.1** — CC0 Public Domain (`Assets/Maze/Third-Party Notices.txt`, https://www.imphenzia.com/).
- **XR Origin rig & interaction baseline (`Assets/Samples/XR Interaction Toolkit/3.7.0/Starter Assets/`):** Unity XR Interaction Toolkit 3.7.0 Starter Assets (Unity Companion License).
- **Spatial audio (`Assets/Scripts/Audio/ProceduralMazeAudio.cs`):** Original runtime procedural `AudioClip` synthesis (no third-party audio packs required).
- **UI typography:** Unity built-in `LegacyRuntime.ttf` on World Space `Canvas` plaques (no external font packages required).

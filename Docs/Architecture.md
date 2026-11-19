# Pocket Labyrinthe architecture

## Product constraints
- Target: Meta Quest 3, Android, fully immersive VR for the first release.
- Core loop: explore a compact maze, consult a live 1:100 miniature, manipulate a miniature mechanism, observe the corresponding full-scale change, and unlock the exit.
- MVP: left-hand miniature, player-position marker, at least one miniature-to-world interaction, an end-to-end route, and a comfortable short session.
- The miniature and world must represent the same maze state, including doors/walls that change during puzzles.

## Runtime boundaries

```text
XR Provider (OpenXR / optional Meta XR)
        |
XR Input Adapters (hands, controllers, locomotion)
        |
Interaction Actions (grab, poke, activate, teleport)
        |
Gameplay Domain (MazeState, PuzzleState, Progression)
       / \
World View   Miniature View
       \       /
        Maze Mapping (stable cell/object IDs, scale and coordinate transform)
```

`MazeState` is the source of truth. The full-scale environment and miniature subscribe to state changes and render them at their own scale. The player marker is derived by transforming the XR rig's tracking-space position into maze-local coordinates and then into miniature-local coordinates. Keep handedness, offsets, and scale in serialized configuration rather than scattered constants.

The completed **Le passage des lanternes** demo expands this exact boundary across all four puzzle beats:
- `MazeDefinition` (`Assets/Data/Mazes/PassageDesLanternesDefinition.asset`) defines the 1:100 scale, spawn/pedestal coordinates, lowering mechanisms (`center_gate`, `atrium_lock`, `exit_gate`), six lantern bays (`bay_1`–`bay_6`, 36 total niches), the Atrium sliding partition bounds/positions, and 3-tier French escalating hints.
- `MazeState` owns authoritative runtime state for all gates, lantern deduction, Atrium lock retraction, sliding partition position/motion/safety blocking, exit lever activation, completion, and deterministic `ResetAllState()`.
- `MazeSessionState` tracks `ProgressionStage`, elapsed time, and orchestrates full replay reset (`ReplayRequested`).
- World views (`WallMechanismView`, `LanternBayView`, `LanternNicheSocket`, `LanternItem`, `LanternPedestalController`, `AtriumPartitionWorldView`, `AtriumCrossingController`, `ExitLeverView`, `CompletionSequenceView`) and miniature views (`MiniatureMechanismView`, `MiniatureWallTarget`, `MiniaturePartitionView`, `MiniatureLanternView`, `PlayerMarkerView`) subscribe independently to `MazeState` via explicit IDs.
- XR input stays behind adapters (`XRControllerMiniatureInteraction`, `ZoneTeleportFilter`, `RightHandOnlyFilter`, `XRWorldButtonInteractable`, `XRHapticsService`), keeping gameplay logic decoupled from hardware APIs.
- Spatial feedback is handled by `ProceduralMazeAudio` (pooled 3D procedural clips) and diegetic World Space French plaques (`DiegeticTextPlaque`, `HintConsoleView`, `CompletionBoardView`) using Unity's built-in `LegacyRuntime.ttf`.

## Suggested folder layout

```text
Assets/
  Scenes/
    Bootstrap.unity
    Maze.unity
  Scripts/
    Core/             # contracts, events, IDs, shared math
    Maze/             # maze definition and authoritative runtime state
    XR/               # provider setup and device-specific input adapters
    Interaction/      # reusable grab/poke/activate interactions
    Miniature/        # scale mapping, held model, marker, model presentation
    Puzzles/          # puzzle rules and progression
    World/            # full-scale maze view and mechanisms
    Comfort/          # locomotion and comfort settings
    UI/               # world-space prompts and session flow
    Audio/            # spatial cues
  Prefabs/
  Settings/
    Build Profiles/
  Data/
    Mazes/
    Puzzles/
  Art/
  Audio/
```

Create folders as features arrive; avoid empty scaffolding assets. Keep XR SDK types inside `Scripts/XR` or dedicated adapters so domain logic can run in editor simulations without a headset. Add assembly definitions when subsystem boundaries become real compilation boundaries.

## Data and state ownership
- `MazeDefinition` (ScriptableObject): cells/rooms, stable IDs, links, initial blockers, scale ratio, and authored puzzle references.
- `MazeState` (runtime): current topology and mechanism states. All world/model changes go through this state.
- `PuzzleDefinition` (ScriptableObject): conditions, actions, hints, and completion signal.
- `SessionState` (runtime): start, tutorial completion, elapsed time, and exit status.
- Views map stable IDs to scene objects; missing mappings should produce clear validation errors.

## XR and comfort
Start with OpenXR and XR Interaction Toolkit for portable controller interactions. Choose Meta XR All-in-One SDK only if the required hand-tracking/features or device tooling justify it; avoid enabling two active XR providers for the same Android target. Plan for tracked hands and controllers behind the same interaction actions. Default to teleport or stationary play, stable horizon, no forced camera movement, and clear physical play-space guidance. Confirm final provider, hand tracking, and Quest runtime behavior on hardware.

## Rendering and performance
Use URP and the Mobile quality level for Android. Favor baked/static lighting, simple opaque geometry, restrained shadow distance, and minimal post-processing. Profile frame time and thermals on Quest 3; editor frame rate is not evidence of device performance.

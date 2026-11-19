# Agent development workflow

## Task loop
1. Read `AGENTS.md`, this guide, the proposal, and current git status.
2. Inspect relevant scenes, prefabs, scripts, package versions, and Unity editor state before editing.
3. Write a short implementation plan in the task conversation when a change crosses subsystem boundaries.
4. Make the smallest coherent change. Use Unity MCP for editor changes when connected; use normal file edits for source-controlled text/code where appropriate.
5. After scripts change, wait for Unity compilation and inspect the console for errors. Do not claim a feature works on Quest without device validation.
6. Review the diff for generated files, local identifiers, accidental asset churn, and `.meta` consistency.
7. Summarize changed files, verification performed, and any remaining hardware or SDK checks.

## Source control
- Work on the current branch unless the user requests otherwise.
- Keep commits focused, imperative, and short (for example `Add Quest XR setup`).
- Stage explicit paths; never use `git add -A` without checking all untracked/modified files first.
- Do not rewrite shared history or force-push. Push the current branch to its configured remote after a successful commit when the user asked for it.
- If push is rejected, inspect remote history and integrate safely before trying again.

## Unity editor practices
- Read `mcpforunity://editor/state`, `mcpforunity://project/info`, and `mcpforunity://custom-tools` before editor mutations.
- Check related package/build/scene resources before changing those areas.
- Use stable asset paths and preserve `.meta` GUIDs.
- Keep scenes/prefabs readable and avoid large opaque setup scripts.
- Do not run Unity tests unless asked. Compilation checks after code changes are still required.

## Current setup notes
- Unity Editor: 6000.3.22f1.
- Render pipeline: URP.
- Intended build target: Android / Meta Quest 3. The user confirmed the app builds and runs on Quest 3 using Unity's Android build profile. Use that existing profile for later Quest builds.
- XR Management 4.5.3, OpenXR 1.16.1, and XR Interaction Toolkit 3.7.0 are pinned in `Packages/manifest.json`. The Android XR loader is assigned and initializes on start. OpenXR has the Quest and Touch controller profiles enabled.
- XRI Starter Assets are imported under `Assets/Samples/XR Interaction Toolkit/3.7.0/Starter Assets`. Use the included XR Origin as the player rig baseline. Keep gameplay independent of XRI types as described above.
- A Meta XR scoped registry is configured. The Meta XR All-in-One package was not installed through that registry; add a Meta package only when a concrete feature requires it and its supported source/version is verified.
- For requested Quest builds, use only the existing Android build profile's Build and Run UI. Do not create/use a custom profile or trigger a build through MCP unless the user changes this instruction. Do not commit generated builds or signing material.
- `Assets/Scenes/QuestPlayerTest.unity` is the basic empty-environment player scene. Teleportation and snap turn are enabled; smooth move and continuous turn are disabled. The user-confirmed run validates installation and launch, while tracking and interaction behavior still need direct headset validation as the player is developed.
- The finished **Pocket Labyrinthe — Le passage des lanternes** demo is authored in `Assets/Scenes/SampleScene.unity`, which remains the launch scene in `ProjectSettings/EditorBuildSettings.asset` and the existing Android build profile. Its authored definition is `Assets/Data/Mazes/PassageDesLanternesDefinition.asset` (legacy `PrototypeMazeDefinition.asset` and `BuildCorridorPrototype.cs` are preserved). To regenerate the demo scene after editing `Assets/Editor/BuildPassageDesLanternesScene.cs` or `Assets/Editor/PassageDesLanternesAssets.cs`, use **Pocket Labyrinthe > Generate Demo Scene (Le passage des lanternes)**; to inspect references/layers/sockets in Edit Mode, use **Pocket Labyrinthe > Validate Demo Scene**. Neither menu item starts a player build.
- Complete demo loop: Spawn faces `center_gate` (matching triple-diamond emblem on world wall and miniature) with the sealed **Porte de l'Aube** exit landmark visible to the east. Opening `center_gate` via right curved pointer + B leads to the Vestibule lantern pedestal (`LanternItem` + manual "Rappeler" button + automatic safe recovery). Six modular bays (`bay_1`–`bay_6`, 36 `LanternNicheSocket`s) loop around the gallery; the miniature's Atrium rail connects directly to the back of `bay_5` (V • Arche), where approaching with the lantern illuminates a carved seam to Niche #5 before docking. Solving the lantern lowers `atrium_lock` and reveals the forgiving direct grab-to-slide handle (`MiniaturePartitionView`, `RightHandOnlyFilter`) on the miniature sliding partition (also operable via curved pointer + B). `AtriumCrossingController` and `ZoneTeleportFilter` enforce valid crossings and doorway threshold safety. Pulling `ExitLeverView` on the Far Side lowers `exit_gate` and plays a dual-scale lighting/audio route cascade into the Exit Sanctuary (`CompletionBoardView` + reliable "Recommencer" replay reset). Optional 3-tier French hints (`EscalatingHintController`) are triggered via right A (`primaryButton`) or the miniature Hint Orb.
- The left-hand miniature continues to follow the full left controller pose from `XRControllerMiniatureInteraction` with a serialized local offset (`(0.06, 0.01, 0.065)`) and `-90°` roll so the 15 cm × 15 cm board levels above the palm when held palm-up. Tune on headset if needed.
- Lighting uses `Assets/Art/NightSkyboxCube.png`, a soft shadowless moonlight directional light (`intensity = 0.34`), Trilight night ambient, and 10 shadowless warm point lights (8 wall lamps + portable lantern + sanctuary beacon) to stay within Quest 3 Mobile URP budgets. All interactive meshes use opaque instanced URP Lit materials with `MaterialPropertyBlock` emission feedback.
- On-headset validation is still required for the completed demo (comfort, controller miniature ergonomics, grab-to-slide feel, teleport filtering, spatial audio mix, and Quest 3 frame timing).
- Do not trigger a player build unless the current task explicitly requests it. If asked, use only the existing Android build profile's Build and Run UI; never create a custom profile or trigger a build through MCP.

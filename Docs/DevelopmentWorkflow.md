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
- Intended build target: Android / Meta Quest 3. The user confirmed a successful Build and Run on the connected Quest 3 using Unity's Android build profile.
- XR Management 4.5.3, OpenXR 1.16.1, and XR Interaction Toolkit 3.7.0 are pinned in `Packages/manifest.json`. The Android XR loader is assigned and initializes on start. OpenXR has the Quest and Touch controller profiles enabled.
- XRI Starter Assets are imported under `Assets/Samples/XR Interaction Toolkit/3.7.0/Starter Assets`. Use the included XR Origin as the player rig baseline. Keep gameplay independent of XRI types as described above.
- A Meta XR scoped registry is configured. The Meta XR All-in-One package was not installed through that registry; add a Meta package only when a concrete feature requires it and its supported source/version is verified.
- Build and run only through Unity's Android build profile. Do not create or use a project-local custom build profile or a different build route unless the user changes this instruction. Do not commit generated builds or signing material.
- `Assets/Scenes/QuestPlayerTest.unity` is the basic empty-environment player scene. Teleportation and snap turn are enabled; smooth move and continuous turn are disabled. The user-confirmed run validates installation and launch, while tracking and interaction behavior still need direct headset validation as the player is developed.

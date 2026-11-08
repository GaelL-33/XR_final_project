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
- Intended build target: Android / Meta Quest 3.
- XR Management 4.5.3 and OpenXR 1.16.1 are pinned as Unity 6 released package versions. OpenXR is not ready yet: the Unity editor does not compile/import its runtime assembly (the OpenXR editor hook reports a missing `UnityEngine.XR.OpenXR` namespace), so the Android loader is still unassigned. XRI and XR Hands are not installed; attempts to add them exposed the same missing OpenXR namespace.
- XR Interaction Toolkit and Meta XR All-in-One SDK are not yet part of the project. Resolve the editor package/provider compatibility issue before adding XRI or deciding whether the Meta SDK is needed. Add it only after deciding which Meta-specific features are needed and using a reproducible version/source.
- No custom Build Profile asset is configured yet; Unity MCP exposed profile listing/activation but not profile creation. Create the Quest 3 Android profile in File > Build Profiles once the provider setup is resolved. Keep build output under `Builds/` (ignored by git) and do not commit signing material.

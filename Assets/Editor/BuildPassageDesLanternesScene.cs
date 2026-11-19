using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

public static class BuildPassageDesLanternesScene
{
    private const string DemoRootName = "Pocket Labyrinthe Demo";
    private const string LegacyPrototypeRootName = "Pocket Labyrinthe Prototype";

    [MenuItem("Pocket Labyrinthe/Generate Demo Scene (Le passage des lanternes)")]
    public static void BuildDemoScene()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.path != PassageDesLanternesAssets.ScenePath)
            activeScene = EditorSceneManager.OpenScene(PassageDesLanternesAssets.ScenePath, OpenSceneMode.Single);

        var mats = PassageDesLanternesAssets.LoadOrCreateMaterials();
        var prefabs = PassageDesLanternesAssets.LoadOrCreatePrefabs(mats);
        var definition = PassageDesLanternesAssets.LoadOrCreateDefinition();

        CleanupExistingSceneRoots();
        ConfigureNightAtmosphere(mats);

        var root = new GameObject(DemoRootName);

        // 1. Core services
        var interactionManager = new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
        interactionManager.transform.SetParent(root.transform, false);

        var stateObj = new GameObject("Maze State");
        stateObj.transform.SetParent(root.transform, false);
        var mazeState = stateObj.AddComponent<MazeState>();
        mazeState.Definition = definition;

        var sessionObj = new GameObject("Session State");
        sessionObj.transform.SetParent(root.transform, false);
        var sessionState = sessionObj.AddComponent<MazeSessionState>();
        sessionState.mazeState = mazeState;

        var audioObj = new GameObject("Procedural Spatial Audio");
        audioObj.transform.SetParent(root.transform, false);
        var mazeAudio = audioObj.AddComponent<ProceduralMazeAudio>();

        var hapticsObj = new GameObject("XR Haptics Service");
        hapticsObj.transform.SetParent(root.transform, false);
        var haptics = hapticsObj.AddComponent<XRHapticsService>();

        // 2. Player Rig (Quest 3 XRI Starter Assets baseline, comfort locomotion)
        var rig = BuildPlayerRig(root.transform, prefabs.QuestRig, definition, interactionManager,
            out var xrOrigin, out var headCamera, out var teleportationProvider);
        haptics.playerRigRoot = rig.transform;

        // 3. World Maze Architecture & Puzzles
        var worldMazeRoot = new GameObject("World Maze Mapping Root").transform;
        worldMazeRoot.SetParent(root.transform, false);

        var crossingObj = new GameObject("Atrium Crossing Controller");
        crossingObj.transform.SetParent(root.transform, false);
        var crossingController = crossingObj.AddComponent<AtriumCrossingController>();
        crossingController.mazeState = mazeState;
        crossingController.worldMazeRoot = worldMazeRoot;
        crossingController.trackedHead = headCamera.transform;

        BuildFloorsAndTeleportAreas(worldMazeRoot, prefabs, mats, interactionManager, teleportationProvider, crossingController);
        BuildOuterAndInteriorWalls(worldMazeRoot, prefabs, mats);
        BuildWallLamps(worldMazeRoot, prefabs);

        // Gates (Intro Gate, Atrium Lock, Exit Gate + Start Viewing Bars)
        BuildIntroGate(worldMazeRoot, prefabs, mats, mazeState, sessionState, mazeAudio, haptics, definition);
        BuildAtriumLockGate(worldMazeRoot, prefabs, mats, mazeState, mazeAudio, haptics, definition);
        BuildExitGateAndViewingBars(worldMazeRoot, prefabs, mats, mazeState, mazeAudio, haptics, definition);

        // Portable Lantern & Vestibule Pedestal
        var lanternItem = BuildLanternAndPedestal(worldMazeRoot, prefabs, mats, mazeState, sessionState, mazeAudio, haptics, interactionManager);

        // Lantern Gallery: 6 Bays x 6 Niches = 36 sockets + Bay 5 carved seam
        BuildLanternGalleryBays(worldMazeRoot, prefabs, mats, definition, mazeState, mazeAudio, haptics, lanternItem, interactionManager);

        // Atrium: Fixed Barrier, Doorway Frame, Single Sliding Partition, Safe Central Island, Crossing Guides
        BuildAtriumStructures(worldMazeRoot, mats, definition, mazeState, sessionState, mazeAudio, haptics, crossingController);

        // Atrium Far Side: Full-Size Exit Lever
        BuildExitLever(worldMazeRoot, prefabs, mats, mazeState, sessionState, mazeAudio, haptics, interactionManager);

        // Exit Sanctuary: Beacon, Route Cascade, Completion Board, Replay Button
        var completionSeq = BuildExitSanctuaryAndRouteBeacons(
            root.transform, worldMazeRoot, prefabs, mats, mazeState, sessionState, mazeAudio, haptics,
            teleportationProvider, xrOrigin, interactionManager);

        // 4. Left-Hand 1:100 Miniature + Dual-Scale Views + Controller Adapter + Escalating Hints
        var mapperObj = new GameObject("Maze Scale Mapper");
        mapperObj.transform.SetParent(root.transform, false);
        var mapper = mapperObj.AddComponent<MazeScaleMapper>();
        mapper.worldMazeRoot = worldMazeRoot;
        mapper.metresToMiniatureScale = definition.metresToMiniatureScale;

        var miniatureRoot = BuildMiniature(
            root.transform, mats, definition, mazeState, mazeAudio, haptics, mapper, headCamera.transform,
            lanternItem.transform, interactionManager,
            out var miniatureLanternView, out var hintConsoleView, out var targetLayer);
        mapper.miniatureRoot = miniatureRoot;
        completionSeq.miniatureLanternView = miniatureLanternView;

        var interactionObj = new GameObject("Controller Miniature Interaction");
        interactionObj.transform.SetParent(root.transform, false);
        var interaction = interactionObj.AddComponent<XRControllerMiniatureInteraction>();
        ConfigureControllerAim(xrOrigin, interaction);
        interaction.miniatureRoot = miniatureRoot;
        interaction.mazeState = mazeState;
        interaction.mazeAudio = mazeAudio;
        interaction.haptics = haptics;
        interaction.targetLayers = 1 << targetLayer;
        interaction.rayLength = 1.5f;
        interaction.leftHandMiniatureLocalPosition = new Vector3(0.06f, 0.01f, 0.065f);
        interaction.leftHandMiniatureLocalEulerOffset = new Vector3(0f, 0f, -90f);

        var pointer = interactionObj.AddComponent<LineRenderer>();
        pointer.enabled = false;
        pointer.useWorldSpace = true;
        pointer.positionCount = 2;
        pointer.sharedMaterial = mats.MagicPointer;
        pointer.startColor = new Color(0.25f, 0.84f, 1f, 0.92f);
        pointer.endColor = new Color(0.25f, 0.84f, 1f, 0.25f);
        pointer.startWidth = 0.003f;
        pointer.endWidth = 0.001f;
        pointer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pointer.receiveShadows = false;
        interaction.pointerLine = pointer;

        var hintControllerObj = new GameObject("Escalating Hint Controller");
        hintControllerObj.transform.SetParent(root.transform, false);
        var hintController = hintControllerObj.AddComponent<EscalatingHintController>();
        hintController.mazeState = mazeState;
        hintController.sessionState = sessionState;
        hintController.miniatureInteraction = interaction;
        hintController.miniatureLanternView = miniatureLanternView;
        hintController.lanternItem = lanternItem;
        hintController.hintConsoleView = hintConsoleView;
        hintController.mazeAudio = mazeAudio;
        hintController.haptics = haptics;

        // Wire every interactable and interactor under root to the single XRInteractionManager
        foreach (var interactable in root.GetComponentsInChildren<XRBaseInteractable>(true))
            interactable.interactionManager = interactionManager;
        foreach (var interactor in root.GetComponentsInChildren<XRBaseInteractor>(true))
            interactor.interactionManager = interactionManager;

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Selection.activeGameObject = root;
        Debug.Log("Generated 'Pocket Labyrinthe — Le passage des lanternes' in SampleScene.unity. No player build was started.", root);

        ValidateDemoScene();
    }

    [MenuItem("Pocket Labyrinthe/Validate Demo Scene")]
    public static void ValidateDemoScene()
    {
        var issues = new List<string>();
        var root = GameObject.Find(DemoRootName);
        if (root == null)
        {
            Debug.LogError($"Validation FAILED: '{DemoRootName}' root not found in active scene.");
            return;
        }

        var mazeState = root.GetComponentInChildren<MazeState>(true);
        if (mazeState == null || mazeState.Definition == null)
            issues.Add("Missing MazeState or MazeDefinition asset reference.");

        var sessionState = root.GetComponentInChildren<MazeSessionState>(true);
        if (sessionState == null || sessionState.mazeState == null)
            issues.Add("Missing MazeSessionState or MazeState reference.");

        var interactionManager = root.GetComponentInChildren<XRInteractionManager>(true);
        if (interactionManager == null)
            issues.Add("Missing XRInteractionManager.");

        // Check 6 bays and 36 sockets
        var bays = root.GetComponentsInChildren<LanternBayView>(true);
        if (bays.Length != 6)
            issues.Add($"Expected 6 LanternBayView components, found {bays.Length}.");

        var sockets = root.GetComponentsInChildren<LanternNicheSocket>(true);
        if (sockets.Length != 36)
            issues.Add($"Expected 36 LanternNicheSocket components, found {sockets.Length}.");

        var correctBays = 0;
        foreach (var bay in bays)
        {
            if (bay.mazeState == null || bay.lanternItem == null)
                issues.Add($"LanternBayView '{bay.name}' has missing mazeState or lanternItem.");
            if (bay.backsOntoAtriumRail)
            {
                correctBays++;
                if (bay.carvedSeamRenderers == null || bay.carvedSeamRenderers.Length == 0)
                    issues.Add($"Correct bay '{bay.name}' has no carvedSeamRenderers.");
            }
        }
        if (correctBays != 1)
            issues.Add($"Expected exactly 1 bay backing onto Atrium rail, found {correctBays}.");

        foreach (var socket in sockets)
        {
            if (socket.mazeState == null || socket.ringRenderer == null)
                issues.Add($"LanternNicheSocket '{socket.name}' missing references.");
            var xriSocket = socket.GetComponent<XRSocketInteractor>();
            if (xriSocket == null || xriSocket.interactionManager == null || xriSocket.attachTransform == null)
                issues.Add($"XRSocketInteractor on '{socket.name}' missing manager or attachTransform.");
        }

        // Check teleport areas & filters
        var teleportMask = InteractionLayerMask.GetMask("Teleport");
        var teleportAreas = root.GetComponentsInChildren<TeleportationArea>(true);
        if (teleportAreas.Length < 14)
            issues.Add($"Expected at least 14 TeleportationArea surfaces, found {teleportAreas.Length}.");
        foreach (var area in teleportAreas)
        {
            if (area.interactionManager == null || area.teleportationProvider == null)
                issues.Add($"TeleportationArea '{area.name}' missing manager or provider.");
            if ((area.interactionLayers.value & teleportMask) == 0)
                issues.Add($"TeleportationArea '{area.name}' does not include 'Teleport' interaction layer.");
            var filter = area.GetComponent<ZoneTeleportFilter>();
            if (filter == null || filter.crossingController == null || filter.targetZone == MazeZoneId.Unknown)
                issues.Add($"TeleportationArea '{area.name}' missing configured ZoneTeleportFilter.");
        }

        // Check Atrium partition world & miniature views
        var worldPartition = root.GetComponentInChildren<AtriumPartitionWorldView>(true);
        if (worldPartition == null || worldPartition.mazeState == null || worldPartition.slidingPartitionRoot == null)
            issues.Add("Missing AtriumPartitionWorldView or references.");

        var miniPartition = root.GetComponentInChildren<MiniaturePartitionView>(true);
        if (miniPartition == null || miniPartition.mazeState == null || miniPartition.grabHandleInteractable == null || miniPartition.pointerTarget == null)
            issues.Add("Missing MiniaturePartitionView or grab/pointer references.");
        else if (miniPartition.grabHandleInteractable.GetComponent<RightHandOnlyFilter>() == null)
            issues.Add("Miniature partition handle is missing RightHandOnlyFilter.");

        // Check lever, completion, replay
        var lever = root.GetComponentInChildren<ExitLeverView>(true);
        if (lever == null || lever.mazeState == null || lever.leverPivot == null || lever.handleInteractable == null)
            issues.Add("Missing ExitLeverView or references.");

        var completion = root.GetComponentInChildren<CompletionSequenceView>(true);
        if (completion == null || completion.mazeState == null || completion.sessionState == null ||
            completion.completionBoardView == null || completion.teleportationProvider == null ||
            completion.routeBeaconRenderers == null || completion.routeBeaconRenderers.Length == 0)
            issues.Add("Missing CompletionSequenceView or replay/beacon references.");

        var board = root.GetComponentInChildren<CompletionBoardView>(true);
        if (board == null || board.sessionState == null || board.replayButton == null)
            issues.Add("Missing CompletionBoardView or replayButton.");

        var hintController = root.GetComponentInChildren<EscalatingHintController>(true);
        if (hintController == null || hintController.hintConsoleView == null || hintController.miniatureInteraction == null)
            issues.Add("Missing EscalatingHintController or references.");

        var lantern = root.GetComponentInChildren<LanternItem>(true);
        if (lantern == null || lantern.mazeState == null || lantern.sessionState == null || lantern.homeTransform == null)
            issues.Add("Missing LanternItem or homeTransform reference.");

        var miniInteraction = root.GetComponentInChildren<XRControllerMiniatureInteraction>(true);
        if (miniInteraction == null || miniInteraction.miniatureRoot == null || miniInteraction.pointerLine == null ||
            miniInteraction.targetLayers.value == 0 || miniInteraction.leftHandPose == null ||
            miniInteraction.rightNearFarInteractor == null || miniInteraction.rightTeleportInteractor == null ||
            miniInteraction.rightWorldPointerLine == null)
            issues.Add("Missing XRControllerMiniatureInteraction or its controller/pointer wiring.");

        // Check active real-time lights count for Quest mobile URP budget
        var activePointLights = 0;
        foreach (var l in root.GetComponentsInChildren<Light>(true))
        {
            if (l.type == LightType.Point)
            {
                activePointLights++;
                if (l.shadows != LightShadows.None)
                    issues.Add($"Point light '{l.name}' has shadows enabled (should be None for Quest 3).");
            }
        }

        if (issues.Count == 0)
        {
            Debug.Log($"[Pocket Labyrinthe Validation PASSED] 6 bays, {sockets.Length} sockets, {teleportAreas.Length} teleport zones, {activePointLights} shadowless point lights, all dual-scale/Atrium/replay references verified.");
        }
        else
        {
            foreach (var issue in issues)
                Debug.LogError($"[Validation Issue] {issue}");
            Debug.LogError($"[Pocket Labyrinthe Validation FAILED] {issues.Count} issue(s) found.");
        }
    }

    private static void CleanupExistingSceneRoots()
    {
        foreach (var name in new[] { DemoRootName, LegacyPrototypeRootName })
        {
            var existing = GameObject.Find(name);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);
        }

        var oldCam = GameObject.FindGameObjectWithTag("MainCamera");
        if (oldCam != null)
            UnityEngine.Object.DestroyImmediate(oldCam);
    }

    private static void ConfigureNightAtmosphere(PassageMaterialSet mats)
    {
        RenderSettings.skybox = mats.NightSkybox;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.18f, 0.24f, 0.40f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.18f, 0.19f, 0.26f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.10f, 0.10f, 0.13f, 1f);
        RenderSettings.ambientIntensity = 1.0f;

        // Soft shadowless cool moonlight so dark corners stay legible on Quest 3 without shadow cost
        Light moonLight = null;
        foreach (var sceneLight in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (sceneLight.type == LightType.Directional)
            {
                moonLight = sceneLight;
                break;
            }
        }

        if (moonLight != null)
        {
            moonLight.enabled = true;
            moonLight.color = new Color(0.55f, 0.68f, 0.96f, 1f);
            moonLight.intensity = 0.34f;
            moonLight.shadows = LightShadows.None;
            moonLight.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            RenderSettings.sun = moonLight;
        }

        DynamicGI.UpdateEnvironment();
    }

    private static GameObject BuildPlayerRig(Transform root, GameObject rigPrefab, MazeDefinition definition,
        XRInteractionManager interactionManager, out XROrigin xrOrigin, out Camera headCamera,
        out TeleportationProvider teleportationProvider)
    {
        var rig = PrefabUtility.InstantiatePrefab(rigPrefab) as GameObject;
        if (rig == null)
            throw new InvalidOperationException("Could not instantiate XR Origin prefab.");

        rig.name = "Quest Player Rig";
        rig.transform.SetParent(root, false);
        rig.transform.localPosition = definition.spawnWorldPosition;
        rig.transform.localRotation = Quaternion.Euler(0f, definition.spawnYawDegrees, 0f);
        rig.SetActive(true);

        xrOrigin = rig.GetComponent<XROrigin>();
        if (xrOrigin == null)
            throw new InvalidOperationException("XR rig prefab has no XROrigin.");
        xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

        teleportationProvider = rig.GetComponentInChildren<TeleportationProvider>(true);
        if (teleportationProvider == null)
            throw new InvalidOperationException("XR Origin prefab has no TeleportationProvider.");

        headCamera = rig.GetComponentInChildren<Camera>(true);
        if (headCamera == null)
            throw new InvalidOperationException("XR Origin prefab has no Main Camera.");

        ConfigureComfortableLocomotion(rig);
        ConfigureSpawn(xrOrigin);

        return rig;
    }

    public static void ConfigureSpawn(XROrigin xrOrigin)
    {
        foreach (var gravity in xrOrigin.GetComponentsInChildren<GravityProvider>(true))
        {
            gravity.useGravity = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(gravity);
        }

        if (!xrOrigin.TryGetComponent<QuestSpawnAlignment>(out _))
            xrOrigin.gameObject.AddComponent<QuestSpawnAlignment>();
    }

    public static void ConfigureControllerAim(XROrigin xrOrigin, XRControllerMiniatureInteraction interaction)
    {
        foreach (var interactor in xrOrigin.GetComponentsInChildren<NearFarInteractor>(true))
        {
            if (interactor.handedness == InteractorHandedness.Left)
                interaction.leftHandPose = interactor.transform.parent;
            else if (interactor.handedness == InteractorHandedness.Right)
                interaction.rightNearFarInteractor = interactor;
        }

        foreach (var ray in xrOrigin.GetComponentsInChildren<XRRayInteractor>(true))
        {
            if (ray.handedness == InteractorHandedness.Right &&
                ray.interactionLayers.value == InteractionLayerMask.GetMask("Teleport"))
                interaction.rightTeleportInteractor = ray;
        }

        if (interaction.leftHandPose == null || interaction.rightNearFarInteractor == null ||
            interaction.rightTeleportInteractor == null)
            throw new InvalidOperationException("Could not find the left/right Near-Far and right teleport interactors.");

        var teleport = interaction.rightTeleportInteractor;
        var aim = teleport.rayOriginTransform;
        var stabilizer = aim.GetComponent<XRTransformStabilizer>();
        var farCaster = interaction.rightNearFarInteractor.GetComponent<CurveInteractionCaster>();
        var worldLine = interaction.rightNearFarInteractor.GetComponentInChildren<CurveVisualController>(true);
        var teleportLine = teleport.GetComponent<XRInteractorLineVisual>();
        if (stabilizer == null || farCaster == null || worldLine == null || teleportLine == null)
            throw new InvalidOperationException("Could not find the right-hand aim stabilizer, caster, or line visuals.");

        // The stabilizer and its target share Camera Offset. Local stabilization follows rig turns/teleports immediately.
        stabilizer.useLocalSpace = true;
        stabilizer.aimTarget = null;
        farCaster.castOrigin = aim;
        farCaster.enableStabilization = false;
        worldLine.overrideLineOrigin = false;
        worldLine.curveStartOffset = 0f;
        worldLine.noValidHitProperties.smoothlyCurveLine = false;
        worldLine.hoverHitProperties.smoothlyCurveLine = false;
        worldLine.selectHitProperties.smoothlyCurveLine = false;
        worldLine.uiHitProperties.smoothlyCurveLine = false;
        worldLine.uiPressHitProperties.smoothlyCurveLine = false;
        teleportLine.overrideInteractorLineOrigin = false;
        interaction.rightWorldPointerLine = worldLine.lineRenderer;

        PrefabUtility.RecordPrefabInstancePropertyModifications(stabilizer);
        PrefabUtility.RecordPrefabInstancePropertyModifications(farCaster);
        PrefabUtility.RecordPrefabInstancePropertyModifications(worldLine);
        PrefabUtility.RecordPrefabInstancePropertyModifications(teleportLine);
    }

    private static void ConfigureComfortableLocomotion(GameObject rig)
    {
        foreach (var behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
                continue;

            var typeName = behaviour.GetType().Name;
            if (typeName.Contains("ContinuousMoveProvider") ||
                typeName.Contains("ContinuousTurnProvider") ||
                typeName.Contains("DynamicMoveProvider") ||
                typeName.Contains("JumpProvider") ||
                typeName.Contains("ClimbProvider"))
            {
                behaviour.enabled = false;
                continue;
            }

            if (typeName != "ControllerInputActionManager" && typeName != "ActionBasedControllerManager")
                continue;

            var serialized = new SerializedObject(behaviour);
            var smoothMotion = serialized.FindProperty("m_SmoothMotionEnabled");
            if (smoothMotion != null)
                smoothMotion.boolValue = false;
            var smoothTurn = serialized.FindProperty("m_SmoothTurnEnabled");
            if (smoothTurn != null)
                smoothTurn.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void BuildFloorsAndTeleportAreas(Transform mazeRoot, PassagePrefabSet prefabs, PassageMaterialSet mats,
        XRInteractionManager interactionManager, TeleportationProvider teleportationProvider,
        AtriumCrossingController crossingController)
    {
        var floorsGroup = new GameObject("Floors").transform;
        floorsGroup.SetParent(mazeRoot, false);

        // Standard 3x3 modular corridor/gallery/sanctuary tiles (each owns a single clean MazeZoneId)
        (int cx, int cz, MazeZoneId zone, string label)[] standardCells =
        {
            (-1, -2, MazeZoneId.StartCorridor, "Floor_StartCorridor"),
            (-1, -1, MazeZoneId.Vestibule, "Floor_CorridorB"),
            (-1,  0, MazeZoneId.Vestibule, "Floor_VestibuleHub"),
            (-1,  1, MazeZoneId.AtriumAntechamber, "Floor_AtriumAntechamber"),
            (-2,  0, MazeZoneId.LanternGallery, "Floor_Gallery_SW"),
            (-2,  1, MazeZoneId.LanternGallery, "Floor_Gallery_W"),
            (-2,  2, MazeZoneId.LanternGallery, "Floor_Gallery_NW"),
            (-1,  2, MazeZoneId.LanternGallery, "Floor_Gallery_N1"),
            ( 0,  2, MazeZoneId.LanternGallery, "Floor_Gallery_N2"),
            ( 1,  2, MazeZoneId.LanternGallery, "Floor_Gallery_N3"),
            ( 2,  2, MazeZoneId.LanternGallery, "Floor_Gallery_NE"),
            ( 0, -2, MazeZoneId.ExitSanctuary, "Floor_ExitSanctuary_W"),
            ( 1, -2, MazeZoneId.ExitSanctuary, "Floor_ExitSanctuary_E"),
        };

        foreach (var cell in standardCells)
        {
            var floor = PlaceFloorTile(floorsGroup, prefabs.Floor3M, cell.cx, cell.cz, cell.label);
            ConfigureFloorTeleport(floor, interactionManager, teleportationProvider, crossingController, cell.zone);
        }

        // Atrium visual floor tiles (non-teleportable base visuals; explicit zone TeleportationAreas sit flush at Y=0)
        (int cx, int cz)[] atriumVisualCells =
        {
            (0, 1), (1, 1),
            (0, 0), (1, 0),
            (0, -1), (1, -1),
        };
        foreach (var c in atriumVisualCells)
        {
            var visualTile = PlaceFloorTile(floorsGroup, prefabs.Floor3M, c.cx, c.cz, $"FloorVisual_Atrium_{c.cx}_{c.cz}");
            // Keep solid floor collider for dropped objects (lantern), but do not attach TeleportationArea here
            AlignFloorSurfaceCollider(visualTile);
        }

        // Recessed starry abyssal basin beneath the East Atrium flanks (framing the Central Island!)
        PassageDesLanternesAssets.CreatePrimitive(floorsGroup, PrimitiveType.Cube, "AtriumAbyssBasin",
            new Vector3(5.45f, -0.36f, 0f), Vector3.zero, new Vector3(4.1f, 0.1f, 9.0f), mats.AbyssChasm, false);

        // Central Island raised stone platform (top aligned strictly to Y=0, safe in BOTH partition states!)
        PassageDesLanternesAssets.CreatePrimitive(floorsGroup, PrimitiveType.Cube, "CentralIslandDeck",
            new Vector3(5.38f, -0.18f, 0f), Vector3.zero, new Vector3(3.64f, 0.36f, 4.9f), mats.StoneDark, false);
        // Sculpted Floor_3M stone inlay centered on the island for cohesive visual texture
        var islandTile = PlaceFloorTile(floorsGroup, prefabs.Floor3M, 2, 0, "CentralIslandStoneInlay");
        islandTile.transform.localPosition = new Vector3(6.88f, -0.116f, -1.5f);
        AlignFloorSurfaceCollider(islandTile);

        // Island perimeter carved sandstone curbing + abyssal rail posts
        PassageDesLanternesAssets.CreatePrimitive(floorsGroup, PrimitiveType.Cube, "IslandCurb_North",
            new Vector3(5.38f, 0.08f, 2.42f), Vector3.zero, new Vector3(3.72f, 0.16f, 0.14f), mats.StoneTrim, false);
        PassageDesLanternesAssets.CreatePrimitive(floorsGroup, PrimitiveType.Cube, "IslandCurb_South",
            new Vector3(5.38f, 0.08f, -2.42f), Vector3.zero, new Vector3(3.72f, 0.16f, 0.14f), mats.StoneTrim, false);
        PassageDesLanternesAssets.CreatePrimitive(floorsGroup, PrimitiveType.Cube, "IslandCurb_WestCenter",
            new Vector3(3.60f, 0.06f, 0f), Vector3.zero, new Vector3(0.14f, 0.12f, 0.65f), mats.StoneTrim, false);

        // Threshold stone bridges connecting West Atrium openings (Z=+1.1 and Z=-1.1) cleanly onto the Central Island
        PassageDesLanternesAssets.CreatePrimitive(floorsGroup, PrimitiveType.Cube, "Bridge_NorthThreshold",
            new Vector3(3.05f, -0.05f, 1.1f), Vector3.zero, new Vector3(1.15f, 0.10f, 1.75f), mats.StoneTrim, false);
        PassageDesLanternesAssets.CreatePrimitive(floorsGroup, PrimitiveType.Cube, "Bridge_SouthThreshold",
            new Vector3(3.05f, -0.05f, -1.1f), Vector3.zero, new Vector3(1.15f, 0.10f, 1.75f), mats.StoneTrim, false);

        // Dedicated non-overlapping TeleportationAreas for the 3 Atrium logical zones (zero overlap with Fixed Barrier or Partition Travel Zone!)
        CreateDedicatedTeleportPad(floorsGroup, "TeleportPad_AtriumEntry",
            new Vector3(0.45f, 0f, 2.35f), new Vector3(3.85f, 0.03f, 4.05f),
            MazeZoneId.AtriumEntry, interactionManager, teleportationProvider, crossingController);

        CreateDedicatedTeleportPad(floorsGroup, "TeleportPad_AtriumFarSide",
            new Vector3(0.45f, 0f, -2.35f), new Vector3(3.85f, 0.03f, 4.05f),
            MazeZoneId.AtriumFarSide, interactionManager, teleportationProvider, crossingController);

        CreateDedicatedTeleportPad(floorsGroup, "TeleportPad_AtriumIsland",
            new Vector3(5.38f, 0f, 0f), new Vector3(3.45f, 0.03f, 4.60f),
            MazeZoneId.AtriumIsland, interactionManager, teleportationProvider, crossingController);
    }

    private static void BuildOuterAndInteriorWalls(Transform mazeRoot, PassagePrefabSet prefabs, PassageMaterialSet mats)
    {
        var wallsGroup = new GameObject("Static Walls").transform;
        wallsGroup.SetParent(mazeRoot, false);

        // Horizontal walls (along X across cell cx, at fixed zEdge)
        (int cx, float zEdge, string label)[] horizontalWalls =
        {
            // South outer edge of Start & Exit Sanctuary (Z = -7.5)
            (-1, -7.5f, "Wall_Start_South"),
            ( 0, -7.5f, "Wall_SanctuaryW_South"),
            ( 1, -7.5f, "Wall_SanctuaryE_South"),

            // Atrium South boundary (cx=1, cx=2 at Z = -4.5; cx=0 is ExitGate!)
            ( 1, -4.5f, "Wall_AtriumS_Mid"),
            ( 2, -4.5f, "Wall_AtriumS_East"),

            // Gallery SW South wall (Bay 1 at Z = -1.5)
            (-2, -1.5f, "Wall_GallerySW_South_Bay1"),

            // North Gallery South walls (separating Gallery N from Antechamber & Atrium at Z = +4.5)
            (-1,  4.5f, "Wall_Antechamber_North"),
            ( 0,  4.5f, "Wall_GalleryN2_South_Bay4"),
            ( 1,  4.5f, "Wall_GalleryN3_South_Bay5_RailBack"),
            ( 2,  4.5f, "Wall_GalleryNE_South_Bay6"),

            // North outer perimeter of Gallery (Z = +7.5)
            (-2,  7.5f, "Wall_GalleryNW_North_Bay3"),
            (-1,  7.5f, "Wall_GalleryN1_North"),
            ( 0,  7.5f, "Wall_GalleryN2_North"),
            ( 1,  7.5f, "Wall_GalleryN3_North"),
            ( 2,  7.5f, "Wall_GalleryNE_North"),
        };

        foreach (var w in horizontalWalls)
            PlaceHorizontalWall(wallsGroup, prefabs.Wall3M, w.cx, w.zEdge, w.label);

        // Vertical walls (along Z across cell cz, at fixed xEdge)
        (float xEdge, int cz, string label)[] verticalWalls =
        {
            // West outer perimeter of Start & Spine (X = -4.5)
            (-4.5f, -2, "Wall_Start_West"),
            (-4.5f, -1, "Wall_CorridorB_West"),
            (-4.5f,  1, "Wall_Antechamber_West_Bay2"),

            // West outer perimeter of Lantern Gallery (X = -7.5)
            (-7.5f,  0, "Wall_GallerySW_West"),
            (-7.5f,  1, "Wall_GalleryW_West"),
            (-7.5f,  2, "Wall_GalleryNW_West"),

            // Spine / Atrium West divider (X = -1.5; cz=+1 is AtriumLock, cz=-2 is Viewing Arch!)
            (-1.5f, -1, "Wall_CorridorB_East"),
            (-1.5f,  0, "Wall_Vestibule_East"),

            // Exit Sanctuary East outer wall (X = +4.5, cz = -2)
            ( 4.5f, -2, "Wall_Sanctuary_East"),

            // East outer perimeter of Atrium & Gallery NE (X = +7.5)
            ( 7.5f, -1, "Wall_AtriumEast_S"),
            ( 7.5f,  0, "Wall_AtriumEast_C"),
            ( 7.5f,  1, "Wall_AtriumEast_N"),
            ( 7.5f,  2, "Wall_GalleryNE_East"),
        };

        foreach (var w in verticalWalls)
            PlaceVerticalWall(wallsGroup, prefabs.Wall3M, w.xEdge, w.cz, w.label);

        // Subtle architectural corner pillars at the Vestibule crossroads for rich silhouette
        PassageDesLanternesAssets.CreatePrimitive(wallsGroup, PrimitiveType.Cube, "VestibulePillar_NW",
            new Vector3(-4.42f, 2.0f, 1.42f), Vector3.zero, new Vector3(0.32f, 4.0f, 0.32f), mats.StoneTrim, false);
        PassageDesLanternesAssets.CreatePrimitive(wallsGroup, PrimitiveType.Cube, "VestibulePillar_SW",
            new Vector3(-4.42f, 2.0f, -1.42f), Vector3.zero, new Vector3(0.32f, 4.0f, 0.32f), mats.StoneTrim, false);
    }

    private static void BuildWallLamps(Transform mazeRoot, PassagePrefabSet prefabs)
    {
        var lampsGroup = new GameObject("Wall Lamps & Lighting").transform;
        lampsGroup.SetParent(mazeRoot, false);

        // Restrained Quest-friendly light budget: 8 well-spaced shadowless warm point lights (plus 1 on PortableLantern & 1 on Exit Beacon = 10 total)
        (Vector3 pos, Vector3 normal, bool addLight, float range, float intensity, string name)[] lamps =
        {
            (new Vector3(-4.29f, 2.25f, -6.0f), Vector3.right, true, 5.5f, 1.8f, "Lamp_StartCorridor"),
            (new Vector3(-1.71f, 2.25f, -3.0f), Vector3.left, false, 0f, 0f, "Lamp_CorridorB"),
            (new Vector3(-1.71f, 2.25f,  0.0f), Vector3.left, true, 6.2f, 1.85f, "Lamp_Vestibule"),
            (new Vector3(-7.29f, 2.25f,  1.5f), Vector3.right, true, 6.2f, 1.75f, "Lamp_GalleryWest"),
            (new Vector3(-3.0f,  2.25f,  7.29f), Vector3.back, true, 6.2f, 1.75f, "Lamp_GalleryNorthW"),
            (new Vector3( 3.0f,  2.25f,  7.29f), Vector3.back, true, 6.5f, 1.85f, "Lamp_GalleryNorthE"),
            (new Vector3(-1.29f, 2.35f,  3.0f), Vector3.right, true, 6.8f, 1.95f, "Lamp_AtriumEntry"),
            (new Vector3( 7.29f, 2.35f,  0.0f), Vector3.left, true, 6.8f, 1.9f, "Lamp_AtriumIsland"),
            (new Vector3(-1.29f, 2.35f, -3.0f), Vector3.right, true, 6.8f, 1.95f, "Lamp_AtriumFarSide"),
            (new Vector3( 4.29f, 2.25f, -6.0f), Vector3.left, false, 0f, 0f, "Lamp_ExitSanctuary"),
        };

        foreach (var l in lamps)
        {
            var instance = InstantiatePrefab(prefabs.WallLamp, lampsGroup, l.pos, Quaternion.LookRotation(l.normal, Vector3.up), l.name);
            if (!l.addLight)
                continue;

            var lightObj = new GameObject($"{l.name}_WarmLight");
            lightObj.transform.SetParent(instance.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.14f, 0.22f);
            var point = lightObj.AddComponent<Light>();
            point.type = LightType.Point;
            point.color = new Color(1f, 0.52f, 0.22f, 1f);
            point.intensity = l.intensity;
            point.range = l.range;
            point.shadows = LightShadows.None;
            point.bounceIntensity = 0f;
        }
    }

    private static void BuildIntroGate(Transform mazeRoot, PassagePrefabSet prefabs, PassageMaterialSet mats,
        MazeState mazeState, MazeSessionState sessionState, ProceduralMazeAudio mazeAudio, XRHapticsService haptics,
        MazeDefinition definition)
    {
        var gate = PlaceHorizontalWall(mazeRoot, prefabs.Wall3M, -1, -4.5f, "Intro Gate (Tutorial)");
        definition.TryGetMechanism(MazeMechanismIds.IntroGate, out var def);

        // Matching triple-diamond / chevron emblem on BOTH sides of the world tutorial wall (mirrored on the miniature!)
        var emblemRoot = new GameObject("GateDiamondEmblem").transform;
        emblemRoot.SetParent(gate.transform, false);
        // Remember Wall_3M spans local X in [-3, 0], so center is localX = -1.5!
        emblemRoot.localPosition = new Vector3(-1.5f, 1.85f, 0f);

        foreach (var zSign in new[] { -1f, 1f })
        {
            var faceZ = zSign * 0.215f;
            PassageDesLanternesAssets.CreatePrimitive(emblemRoot, PrimitiveType.Cube, $"DiamondOuter_{zSign}",
                new Vector3(0f, 0f, faceZ), new Vector3(0f, 0f, 45f), new Vector3(0.76f, 0.76f, 0.045f), mats.BronzeMetal);
            PassageDesLanternesAssets.CreatePrimitive(emblemRoot, PrimitiveType.Cube, $"DiamondInner_{zSign}",
                new Vector3(0f, 0f, faceZ + zSign * 0.015f), new Vector3(0f, 0f, 45f), new Vector3(0.46f, 0.46f, 0.045f), mats.EmissiveGold);
            PassageDesLanternesAssets.CreatePrimitive(emblemRoot, PrimitiveType.Cube, $"DiamondFlankL_{zSign}",
                new Vector3(-0.72f, 0f, faceZ), new Vector3(0f, 0f, 45f), new Vector3(0.24f, 0.24f, 0.04f), mats.EmissiveGold);
            PassageDesLanternesAssets.CreatePrimitive(emblemRoot, PrimitiveType.Cube, $"DiamondFlankR_{zSign}",
                new Vector3(0.72f, 0f, faceZ), new Vector3(0f, 0f, 45f), new Vector3(0.24f, 0.24f, 0.04f), mats.EmissiveGold);
        }

        var view = gate.AddComponent<WallMechanismView>();
        view.mazeState = mazeState;
        view.mazeAudio = mazeAudio;
        view.haptics = haptics;
        view.mechanismId = MazeMechanismIds.IntroGate;
        view.raisedLocalPosition = gate.transform.localPosition;
        view.loweredLocalOffset = Vector3.down * (def != null ? def.lowerDistance : 4.6f);
        view.animationSeconds = def != null ? def.animationSeconds : 0.7f;
        view.disableCollidersWhenLowered = true;

        // Concise French tutorial plaque angled cleanly in the NW corner of Start (zero wall or gate overlap)
        var introPlaque = PassageDesLanternesAssets.CreateWorldPlaque(
            mazeRoot, "Plaque_IntroTutorial",
            new Vector3(-3.88f, 1.48f, -5.18f), new Vector3(0f, -42f, 0f),
            0.94f, 0.40f,
            "Le Passage des Lanternes",
            "Pointez la porte sur la maquette • Appuyez sur B\n(Bouton A : indice)",
            mats, 44, 30);
        introPlaque.sessionState = sessionState;
        introPlaque.filterByProgressionStage = true;
        introPlaque.minVisibleStage = ProgressionStage.IntroGateClosed;
        introPlaque.maxVisibleStage = ProgressionStage.IntroGateClosed;
    }

    private static void BuildAtriumLockGate(Transform mazeRoot, PassagePrefabSet prefabs, PassageMaterialSet mats,
        MazeState mazeState, ProceduralMazeAudio mazeAudio, XRHapticsService haptics, MazeDefinition definition)
    {
        definition.TryGetMechanism(MazeMechanismIds.AtriumLock, out var def);

        // Fixed stone archway frame at X = -1.5, Z in [+1.5, +4.5]
        var frame = new GameObject("Atrium Lock Frame").transform;
        frame.SetParent(mazeRoot, false);
        frame.localPosition = new Vector3(-1.5f, 0f, 3.0f);
        PassageDesLanternesAssets.CreatePrimitive(frame, PrimitiveType.Cube, "PillarN", new Vector3(0f, 2f, 1.32f), Vector3.zero, new Vector3(0.42f, 4f, 0.38f), mats.StoneDark, false);
        PassageDesLanternesAssets.CreatePrimitive(frame, PrimitiveType.Cube, "PillarS", new Vector3(0f, 2f, -1.32f), Vector3.zero, new Vector3(0.42f, 4f, 0.38f), mats.StoneDark, false);
        PassageDesLanternesAssets.CreatePrimitive(frame, PrimitiveType.Cube, "ArchLintel", new Vector3(0f, 3.55f, 0f), Vector3.zero, new Vector3(0.44f, 0.9f, 3.0f), mats.StoneTrim, false);

        // Retractable bronze portcullis grate so the player can peer into the Atrium before unlocking it!
        var gateRoot = new GameObject("Atrium Lock Gate");
        gateRoot.transform.SetParent(mazeRoot, false);
        gateRoot.transform.localPosition = new Vector3(-1.5f, 0f, 3.0f);

        PassageDesLanternesAssets.CreatePrimitive(gateRoot.transform, PrimitiveType.Cube, "LockPlinth", new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(0.24f, 0.9f, 2.28f), mats.StoneDark);
        PassageDesLanternesAssets.CreatePrimitive(gateRoot.transform, PrimitiveType.Cube, "CrossBarMid", new Vector3(0f, 1.9f, 0f), Vector3.zero, new Vector3(0.18f, 0.14f, 2.28f), mats.BronzeMetal);
        PassageDesLanternesAssets.CreatePrimitive(gateRoot.transform, PrimitiveType.Cube, "CrossBarTop", new Vector3(0f, 2.95f, 0f), Vector3.zero, new Vector3(0.18f, 0.14f, 2.28f), mats.BronzeMetal);
        for (var i = -3; i <= 3; i++)
        {
            PassageDesLanternesAssets.CreatePrimitive(gateRoot.transform, PrimitiveType.Cylinder, $"GrateBar_{i + 3}",
                new Vector3(0f, 1.95f, i * 0.31f), Vector3.zero, new Vector3(0.06f, 1.1f, 0.06f), mats.BronzeMetal);
        }

        // Single clean solid blocker collider (blocks teleport ray & physical entry while raised)
        var blocker = gateRoot.AddComponent<BoxCollider>();
        blocker.center = new Vector3(0f, 1.8f, 0f);
        blocker.size = new Vector3(0.32f, 3.6f, 2.35f);

        var view = gateRoot.AddComponent<WallMechanismView>();
        view.mazeState = mazeState;
        view.mazeAudio = mazeAudio;
        view.haptics = haptics;
        view.mechanismId = MazeMechanismIds.AtriumLock;
        view.raisedLocalPosition = gateRoot.transform.localPosition;
        view.loweredLocalOffset = Vector3.down * (def != null ? def.lowerDistance : 4.6f);
        view.animationSeconds = def != null ? def.animationSeconds : 0.9f;
        view.disableCollidersWhenLowered = true;

        PassageDesLanternesAssets.CreateWorldPlaque(
            frame, "Plaque_AtriumLock",
            new Vector3(-0.24f, 2.38f, -0.95f), new Vector3(0f, 90f, 0f),
            0.96f, 0.34f,
            "Verrou de l'Atrium",
            "Relié à la galerie des lanternes",
            mats, 40, 28);
    }

    private static void BuildExitGateAndViewingBars(Transform mazeRoot, PassagePrefabSet prefabs, PassageMaterialSet mats,
        MazeState mazeState, ProceduralMazeAudio mazeAudio, XRHapticsService haptics, MazeDefinition definition)
    {
        definition.TryGetMechanism(MazeMechanismIds.ExitGate, out var def);

        // 1. Main Exit Arch between Atrium Far Side and Exit Sanctuary (Z = -4.5, X in [-1.5, +1.5])
        var exitFrame = new GameObject("Exit Arch Frame (Porte de l'Aube)").transform;
        exitFrame.SetParent(mazeRoot, false);
        exitFrame.localPosition = new Vector3(0f, 0f, -4.5f);

        PassageDesLanternesAssets.CreatePrimitive(exitFrame, PrimitiveType.Cube, "PillarW", new Vector3(-1.28f, 2f, 0f), Vector3.zero, new Vector3(0.46f, 4f, 0.44f), mats.StoneTrim, false);
        PassageDesLanternesAssets.CreatePrimitive(exitFrame, PrimitiveType.Cube, "PillarE", new Vector3(1.28f, 2f, 0f), Vector3.zero, new Vector3(0.46f, 4f, 0.44f), mats.StoneTrim, false);
        PassageDesLanternesAssets.CreatePrimitive(exitFrame, PrimitiveType.Cube, "CrownBeam", new Vector3(0f, 3.55f, 0f), Vector3.zero, new Vector3(3.04f, 0.9f, 0.48f), mats.StoneTrim, false);
        // Sun-Lantern golden crown crest on top of the Exit Arch (recognizable landmark!)
        PassageDesLanternesAssets.CreatePrimitive(exitFrame, PrimitiveType.Cube, "DawnCrestDiamond",
            new Vector3(0f, 3.55f, 0.26f), new Vector3(0f, 0f, 45f), new Vector3(0.55f, 0.55f, 0.08f), mats.EmissiveGold);
        PassageDesLanternesAssets.CreatePrimitive(exitFrame, PrimitiveType.Cube, "DawnCrestBack",
            new Vector3(0f, 3.55f, -0.26f), new Vector3(0f, 0f, 45f), new Vector3(0.55f, 0.55f, 0.08f), mats.EmissiveGold);

        var exitGate = new GameObject("Exit Gate Portcullis");
        exitGate.transform.SetParent(mazeRoot, false);
        exitGate.transform.localPosition = new Vector3(0f, 0f, -4.5f);
        PassageDesLanternesAssets.CreatePrimitive(exitGate.transform, PrimitiveType.Cube, "ExitPlinth", new Vector3(0f, 0.35f, 0f), Vector3.zero, new Vector3(2.14f, 0.7f, 0.22f), mats.StoneDark);
        PassageDesLanternesAssets.CreatePrimitive(exitGate.transform, PrimitiveType.Cube, "ExitCrossbar", new Vector3(0f, 1.85f, 0f), Vector3.zero, new Vector3(2.14f, 0.16f, 0.18f), mats.BronzeMetal);
        for (var i = -3; i <= 3; i++)
        {
            PassageDesLanternesAssets.CreatePrimitive(exitGate.transform, PrimitiveType.Cylinder, $"ExitBar_{i + 3}",
                new Vector3(i * 0.29f, 1.8f, 0f), Vector3.zero, new Vector3(0.06f, 1.3f, 0.06f), mats.BronzeMetal);
        }

        var exitBlocker = exitGate.AddComponent<BoxCollider>();
        exitBlocker.center = new Vector3(0f, 1.8f, 0f);
        exitBlocker.size = new Vector3(2.25f, 3.6f, 0.32f);

        var exitView = exitGate.AddComponent<WallMechanismView>();
        exitView.mazeState = mazeState;
        exitView.mazeAudio = mazeAudio;
        exitView.haptics = haptics;
        exitView.mechanismId = MazeMechanismIds.ExitGate;
        exitView.raisedLocalPosition = exitGate.transform.localPosition;
        exitView.loweredLocalOffset = Vector3.down * (def != null ? def.lowerDistance : 4.6f);
        exitView.animationSeconds = def != null ? def.animationSeconds : 1.0f;
        exitView.disableCollidersWhenLowered = true;

        // 2. Start-Side Viewing Archway at X = -1.5, Z = -6.0 (introduces the Exit Sanctuary landmark right from Spawn!)
        var viewFrame = new GameObject("Start Viewing Arch (Exit Landmark Vista)").transform;
        viewFrame.SetParent(mazeRoot, false);
        viewFrame.localPosition = new Vector3(-1.5f, 0f, -6.0f);
        PassageDesLanternesAssets.CreatePrimitive(viewFrame, PrimitiveType.Cube, "ViewPillarN", new Vector3(0f, 2f, 1.30f), Vector3.zero, new Vector3(0.42f, 4f, 0.40f), mats.StoneTrim, false);
        PassageDesLanternesAssets.CreatePrimitive(viewFrame, PrimitiveType.Cube, "ViewPillarS", new Vector3(0f, 2f, -1.30f), Vector3.zero, new Vector3(0.42f, 4f, 0.40f), mats.StoneTrim, false);
        PassageDesLanternesAssets.CreatePrimitive(viewFrame, PrimitiveType.Cube, "ViewLintel", new Vector3(0f, 3.55f, 0f), Vector3.zero, new Vector3(0.44f, 0.9f, 3.0f), mats.StoneTrim, false);
        PassageDesLanternesAssets.CreatePrimitive(viewFrame, PrimitiveType.Cube, "ViewCrest",
            new Vector3(-0.24f, 3.52f, 0f), new Vector3(45f, 0f, 0f), new Vector3(0.08f, 0.46f, 0.46f), mats.EmissiveGold);

        // Retractable bronze viewing bars (lowers together with exit_gate at the climax!)
        var viewBars = new GameObject("Start Viewing Bars");
        viewBars.transform.SetParent(mazeRoot, false);
        viewBars.transform.localPosition = new Vector3(-1.5f, 0f, -6.0f);
        PassageDesLanternesAssets.CreatePrimitive(viewBars.transform, PrimitiveType.Cube, "LowSill", new Vector3(0f, 0.35f, 0f), Vector3.zero, new Vector3(0.24f, 0.7f, 2.2f), mats.StoneDark);
        PassageDesLanternesAssets.CreatePrimitive(viewBars.transform, PrimitiveType.Cube, "ViewCrossBar", new Vector3(0f, 2.0f, 0f), Vector3.zero, new Vector3(0.16f, 0.12f, 2.2f), mats.BronzeMetal);
        for (var i = -3; i <= 3; i++)
        {
            PassageDesLanternesAssets.CreatePrimitive(viewBars.transform, PrimitiveType.Cylinder, $"ViewBar_{i + 3}",
                new Vector3(0f, 1.9f, i * 0.29f), Vector3.zero, new Vector3(0.055f, 1.25f, 0.055f), mats.BronzeMetal);
        }

        var viewBlocker = viewBars.AddComponent<BoxCollider>();
        viewBlocker.center = new Vector3(0f, 1.8f, 0f);
        viewBlocker.size = new Vector3(0.32f, 3.6f, 2.25f);

        var viewBarsMech = viewBars.AddComponent<WallMechanismView>();
        viewBarsMech.mazeState = mazeState;
        viewBarsMech.mechanismId = MazeMechanismIds.ExitGate;
        viewBarsMech.raisedLocalPosition = viewBars.transform.localPosition;
        viewBarsMech.loweredLocalOffset = Vector3.down * (def != null ? def.lowerDistance : 4.6f);
        viewBarsMech.animationSeconds = def != null ? def.animationSeconds : 1.0f;
        viewBarsMech.disableCollidersWhenLowered = true;

        PassageDesLanternesAssets.CreateWorldPlaque(
            viewFrame, "Plaque_ExitLandmark",
            new Vector3(-0.24f, 2.60f, -0.78f), new Vector3(0f, 90f, 0f),
            0.88f, 0.30f,
            "Porte de l'Aube",
            "Sortie scellée",
            mats, 38, 28);
    }

    private static LanternItem BuildLanternAndPedestal(Transform mazeRoot, PassagePrefabSet prefabs, PassageMaterialSet mats,
        MazeState mazeState, MazeSessionState sessionState, ProceduralMazeAudio mazeAudio, XRHapticsService haptics,
        XRInteractionManager interactionManager)
    {
        var pedestalRoot = new GameObject("Lantern Pedestal");
        pedestalRoot.transform.SetParent(mazeRoot, false);
        pedestalRoot.transform.localPosition = new Vector3(-3.0f, 0f, 0.0f);

        // Sculpted stone & bronze pedestal (top surface at Y = 0.96m, comfortable seated or standing)
        PassageDesLanternesAssets.CreatePrimitive(pedestalRoot.transform, PrimitiveType.Cylinder, "PedestalFoot",
            new Vector3(0f, 0.08f, 0f), Vector3.zero, new Vector3(0.68f, 0.08f, 0.68f), mats.StoneTrim, false);
        PassageDesLanternesAssets.CreatePrimitive(pedestalRoot.transform, PrimitiveType.Cylinder, "PedestalColumn",
            new Vector3(0f, 0.50f, 0f), Vector3.zero, new Vector3(0.42f, 0.42f, 0.42f), mats.StoneDark, false);
        PassageDesLanternesAssets.CreatePrimitive(pedestalRoot.transform, PrimitiveType.Cylinder, "PedestalTop",
            new Vector3(0f, 0.93f, 0f), Vector3.zero, new Vector3(0.58f, 0.04f, 0.58f), mats.StoneTrim, false);
        var cradleRing = PassageDesLanternesAssets.CreatePrimitive(pedestalRoot.transform, PrimitiveType.Cylinder, "CradleRing",
            new Vector3(0f, 0.97f, 0f), Vector3.zero, new Vector3(0.28f, 0.012f, 0.28f), mats.EmissiveDormant);

        var homeRestPoint = new GameObject("LanternRestPoint").transform;
        homeRestPoint.SetParent(pedestalRoot.transform, false);
        homeRestPoint.localPosition = new Vector3(0f, 0.985f, 0f);
        homeRestPoint.localRotation = Quaternion.identity;

        // Diegetic "Rappeler" button on the South side of the pedestal for safe manual recovery anytime
        var recallBtnObj = InstantiatePrefab(prefabs.WorldButton, pedestalRoot.transform,
            new Vector3(0f, 0.78f, -0.30f), Quaternion.Euler(-65f, 0f, 0f), "RecallLanternButton");
        var recallBtn = recallBtnObj.GetComponent<XRWorldButtonInteractable>();
        recallBtn.mazeAudio = mazeAudio;
        recallBtn.haptics = haptics;

        PassageDesLanternesAssets.CreateWorldPlaque(
            pedestalRoot.transform, "Plaque_LanternPedestal",
            new Vector3(0f, 0.50f, -0.26f), Vector3.zero,
            0.66f, 0.28f,
            "La Lanterne",
            "Prendre (Grip) • Bouton : Rappeler",
            mats, 36, 24);

        // Instantiate Portable Lantern
        var lanternObj = InstantiatePrefab(prefabs.PortableLantern, mazeRoot,
            homeRestPoint.position, Quaternion.identity, "Portable Lantern");
        var lanternItem = lanternObj.GetComponent<LanternItem>();
        lanternItem.mazeState = mazeState;
        lanternItem.sessionState = sessionState;
        lanternItem.mazeAudio = mazeAudio;
        lanternItem.haptics = haptics;
        lanternItem.homeTransform = homeRestPoint;

        var pedestalCtrl = pedestalRoot.AddComponent<LanternPedestalController>();
        pedestalCtrl.mazeState = mazeState;
        pedestalCtrl.lanternItem = lanternItem;
        pedestalCtrl.recallButton = recallBtn;
        pedestalCtrl.cradleRingRenderer = cradleRing.GetComponent<Renderer>();

        return lanternItem;
    }

    private static void BuildLanternGalleryBays(Transform mazeRoot, PassagePrefabSet prefabs, PassageMaterialSet mats,
        MazeDefinition definition, MazeState mazeState, ProceduralMazeAudio mazeAudio, XRHapticsService haptics,
        LanternItem lanternItem, XRInteractionManager interactionManager)
    {
        var galleryGroup = new GameObject("Lantern Gallery (6 Bays x 6 Niches)").transform;
        galleryGroup.SetParent(mazeRoot, false);

        // 3 columns x 2 rows per bay (from viewer's facing perspective: +X = left, 0 = center, -X = right!)
        float[] colX = { 0.78f, 0.0f, -0.78f };
        float[] rowY = { 1.05f, 1.54f };

        foreach (var bayDef in definition.lanternBays)
        {
            var bayObj = new GameObject($"LanternBay_{bayDef.numeral}_{bayDef.displayNameFr}");
            bayObj.transform.SetParent(galleryGroup, false);
            bayObj.transform.localPosition = bayDef.worldCenter;
            bayObj.transform.localRotation = Quaternion.LookRotation(bayDef.inwardNormal, Vector3.up);

            // Architectural stone framing framing the 3m bay
            PassageDesLanternesAssets.CreatePrimitive(bayObj.transform, PrimitiveType.Cube, "BayBackPanel",
                new Vector3(0f, 1.42f, 0.02f), Vector3.zero, new Vector3(2.44f, 2.38f, 0.04f), mats.StoneDark);
            PassageDesLanternesAssets.CreatePrimitive(bayObj.transform, PrimitiveType.Cube, "BayPilasterL",
                new Vector3(1.24f, 1.48f, 0.06f), Vector3.zero, new Vector3(0.14f, 2.64f, 0.14f), mats.StoneTrim);
            PassageDesLanternesAssets.CreatePrimitive(bayObj.transform, PrimitiveType.Cube, "BayPilasterR",
                new Vector3(-1.24f, 1.48f, 0.06f), Vector3.zero, new Vector3(0.14f, 2.64f, 0.14f), mats.StoneTrim);
            PassageDesLanternesAssets.CreatePrimitive(bayObj.transform, PrimitiveType.Cube, "BayCornice",
                new Vector3(0f, 2.74f, 0.08f), Vector3.zero, new Vector3(2.68f, 0.16f, 0.22f), mats.StoneTrim);

            // Distinct 3D geometric bay emblem at eye-above height (Y = 2.28m)
            var emblemAnchor = new GameObject("BayEmblemAnchor").transform;
            emblemAnchor.SetParent(bayObj.transform, false);
            emblemAnchor.localPosition = new Vector3(0f, 2.28f, 0.08f);
            emblemAnchor.localRotation = Quaternion.Euler(0f, 180f, 0f);
            PassageDesLanternesAssets.BuildBayEmblemGeometry(emblemAnchor, bayDef.emblemKind, mats.BronzeMetal, 1.45f);

            // Concise French bay label plaque (rot 180 so canvas faces +Z into the corridor)
            PassageDesLanternesAssets.CreateWorldPlaque(
                bayObj.transform, $"Plaque_{bayDef.id}",
                new Vector3(0f, 0.44f, 0.06f), new Vector3(0f, 180f, 0f),
                0.86f, 0.26f,
                $"Baie {bayDef.numeral} • {bayDef.displayNameFr}",
                string.Empty,
                mats, 50, 22);

            // Instantiate 6 modular LanternNiche sockets
            var nicheSockets = new LanternNicheSocket[6];
            for (var row = 0; row < 2; row++)
            {
                for (var col = 0; col < 3; col++)
                {
                    var index = row * 3 + col;
                    var nichePos = new Vector3(colX[col], rowY[row], 0.06f);
                    var nicheInstance = InstantiatePrefab(prefabs.LanternNiche, bayObj.transform,
                        nichePos, Quaternion.identity, $"Niche_{bayDef.id}_{index}");
                    var socketScript = nicheInstance.GetComponent<LanternNicheSocket>();
                    socketScript.mazeState = mazeState;
                    socketScript.mazeAudio = mazeAudio;
                    socketScript.haptics = haptics;
                    socketScript.bayId = bayDef.id;
                    socketScript.nicheIndex = index;
                    nicheSockets[index] = socketScript;
                }
            }

            var revealOrigin = new GameObject("SeamRevealOrigin").transform;
            revealOrigin.SetParent(bayObj.transform, false);
            revealOrigin.localPosition = new Vector3(0f, 1.25f, 0.8f);

            var bayView = bayObj.AddComponent<LanternBayView>();
            bayView.mazeState = mazeState;
            bayView.mazeAudio = mazeAudio;
            bayView.lanternItem = lanternItem;
            bayView.bayId = bayDef.id;
            bayView.backsOntoAtriumRail = bayDef.backsOntoAtriumRail;
            bayView.correctNicheIndex = bayDef.correctNicheIndex;
            bayView.seamRevealOrigin = revealOrigin;
            bayView.nicheSockets = nicheSockets;

            // For Bay 5 (the bay physically backing onto the Atrium Rail), sculpt the hidden carved stone seam
            // leading unmistakably from the lower floor rail mark right into Niche #5 (top-right, localX = -0.78, localY = 1.54)!
            if (bayDef.backsOntoAtriumRail)
            {
                var seamGroup = new GameObject("CarvedSeam_HiddenConduit").transform;
                seamGroup.SetParent(bayObj.transform, false);
                var seamRenderers = new List<Renderer>
                {
                    // Base rail anchor plate -> vertical riser -> step -> horizontal channel -> entry into Niche #5
                    PassageDesLanternesAssets.CreatePrimitive(seamGroup, PrimitiveType.Cube, "Seam_0_FloorNode",
                        new Vector3(0.0f, 0.12f, 0.045f), new Vector3(0f, 0f, 45f), new Vector3(0.16f, 0.16f, 0.03f), mats.EmissiveDormant).GetComponent<Renderer>(),
                    PassageDesLanternesAssets.CreatePrimitive(seamGroup, PrimitiveType.Cube, "Seam_1_RiserA",
                        new Vector3(0.0f, 0.45f, 0.045f), Vector3.zero, new Vector3(0.055f, 0.62f, 0.025f), mats.EmissiveDormant).GetComponent<Renderer>(),
                    PassageDesLanternesAssets.CreatePrimitive(seamGroup, PrimitiveType.Cube, "Seam_2_BranchLeft",
                        new Vector3(0.18f, 0.74f, 0.045f), Vector3.zero, new Vector3(0.42f, 0.055f, 0.025f), mats.EmissiveDormant).GetComponent<Renderer>(),
                    PassageDesLanternesAssets.CreatePrimitive(seamGroup, PrimitiveType.Cube, "Seam_3_RiserMid",
                        new Vector3(0.38f, 1.02f, 0.045f), Vector3.zero, new Vector3(0.055f, 0.62f, 0.025f), mats.EmissiveDormant).GetComponent<Renderer>(),
                    PassageDesLanternesAssets.CreatePrimitive(seamGroup, PrimitiveType.Cube, "Seam_4_SweepRight",
                        new Vector3(-0.20f, 1.30f, 0.045f), Vector3.zero, new Vector3(1.22f, 0.058f, 0.025f), mats.EmissiveDormant).GetComponent<Renderer>(),
                    PassageDesLanternesAssets.CreatePrimitive(seamGroup, PrimitiveType.Cube, "Seam_5_IntoNiche5",
                        new Vector3(-0.78f, 1.36f, 0.052f), Vector3.zero, new Vector3(0.06f, 0.16f, 0.03f), mats.EmissiveDormant).GetComponent<Renderer>(),
                };
                bayView.carvedSeamRenderers = seamRenderers.ToArray();

                // Solve surge conduit rising from Niche #5 up to the cornice & over toward the Atrium Rail behind the wall
                var conduitGroup = new GameObject("RailSurgeConduit").transform;
                conduitGroup.SetParent(bayObj.transform, false);
                var conduitRenderers = new List<Renderer>
                {
                    PassageDesLanternesAssets.CreatePrimitive(conduitGroup, PrimitiveType.Cube, "Conduit_0_AboveNiche5",
                        new Vector3(-0.78f, 1.98f, 0.05f), Vector3.zero, new Vector3(0.06f, 0.44f, 0.03f), mats.EmissiveDormant).GetComponent<Renderer>(),
                    PassageDesLanternesAssets.CreatePrimitive(conduitGroup, PrimitiveType.Cube, "Conduit_1_ToCenter",
                        new Vector3(-0.39f, 2.20f, 0.05f), Vector3.zero, new Vector3(0.84f, 0.06f, 0.03f), mats.EmissiveDormant).GetComponent<Renderer>(),
                    PassageDesLanternesAssets.CreatePrimitive(conduitGroup, PrimitiveType.Cube, "Conduit_2_CrownSurge",
                        new Vector3(0.0f, 2.52f, 0.06f), Vector3.zero, new Vector3(0.08f, 0.56f, 0.04f), mats.EmissiveDormant).GetComponent<Renderer>(),
                    PassageDesLanternesAssets.CreatePrimitive(conduitGroup, PrimitiveType.Cube, "Conduit_3_OverWall",
                        new Vector3(0.0f, 2.86f, -0.22f), Vector3.zero, new Vector3(0.14f, 0.14f, 0.62f), mats.EmissiveDormant).GetComponent<Renderer>(),
                };
                bayView.railConduitRenderers = conduitRenderers.ToArray();
            }
        }
    }

    private static void BuildAtriumStructures(Transform mazeRoot, PassageMaterialSet mats, MazeDefinition definition,
        MazeState mazeState, MazeSessionState sessionState, ProceduralMazeAudio mazeAudio, XRHapticsService haptics,
        AtriumCrossingController crossingController)
    {
        var atriumGroup = new GameObject("Atrium Puzzle Structures").transform;
        atriumGroup.SetParent(mazeRoot, false);

        // 1. FIXED CENTRAL BARRIER separating Entry (Z > 0) and Far/Exit Side (Z < 0)
        // Spans X in [-1.5, +2.85], Z = 0, full 4.0m height with bold bronze vertical ribs so it clearly reads as fixed
        var barrier = new GameObject("Atrium Fixed Barrier").transform;
        barrier.SetParent(atriumGroup, false);
        PassageDesLanternesAssets.CreatePrimitive(barrier, PrimitiveType.Cube, "FixedBarrierWall",
            new Vector3(0.68f, 2.0f, 0f), Vector3.zero, new Vector3(4.36f, 4.0f, 0.36f), mats.StoneDark, false);
        PassageDesLanternesAssets.CreatePrimitive(barrier, PrimitiveType.Cube, "FixedBarrierCrown",
            new Vector3(0.68f, 3.92f, 0f), Vector3.zero, new Vector3(4.42f, 0.22f, 0.44f), mats.StoneTrim);
        for (var i = 0; i < 4; i++)
        {
            PassageDesLanternesAssets.CreatePrimitive(barrier, PrimitiveType.Cube, $"BarrierRib_{i}",
                new Vector3(-0.75f + i * 1.05f, 2.0f, 0f), Vector3.zero, new Vector3(0.16f, 3.85f, 0.44f), mats.BronzeMetal);
        }

        // 2. FIXED DOORWAY FRAME along X = +3.18 (North doorway Z in [+0.2, +2.0], Center Pier [-0.2, +0.2], South doorway [-2.0, -0.2])
        var frameGroup = new GameObject("Atrium Fixed Rail Frame").transform;
        frameGroup.SetParent(atriumGroup, false);
        PassageDesLanternesAssets.CreatePrimitive(frameGroup, PrimitiveType.Cube, "FrameWing_North",
            new Vector3(3.18f, 2.0f, 3.25f), Vector3.zero, new Vector3(0.24f, 4.0f, 2.50f), mats.StoneDark, false);
        PassageDesLanternesAssets.CreatePrimitive(frameGroup, PrimitiveType.Cube, "FramePier_Center",
            new Vector3(3.18f, 2.0f, 0.0f), Vector3.zero, new Vector3(0.28f, 4.0f, 0.44f), mats.StoneTrim, false);
        PassageDesLanternesAssets.CreatePrimitive(frameGroup, PrimitiveType.Cube, "FrameWing_South",
            new Vector3(3.18f, 2.0f, -3.25f), Vector3.zero, new Vector3(0.24f, 4.0f, 2.50f), mats.StoneDark, false);
        // Overhead lintel beam above the two 2.85m doorways
        PassageDesLanternesAssets.CreatePrimitive(frameGroup, PrimitiveType.Cube, "FrameOverheadLintel",
            new Vector3(3.18f, 3.50f, 0f), Vector3.zero, new Vector3(0.32f, 1.0f, 9.0f), mats.StoneTrim, false);

        // Prominent Atrium Sliding Rail running North-South along X = 2.84 from Z = -4.45 to Z = +4.45 (bolted straight into Bay 5's back wall!)
        var railTop = PassageDesLanternesAssets.CreatePrimitive(frameGroup, PrimitiveType.Cube, "AtriumRail_Overhead",
            new Vector3(2.84f, 3.35f, 0f), Vector3.zero, new Vector3(0.20f, 0.16f, 8.92f), mats.BronzeMetal);
        var railFloor = PassageDesLanternesAssets.CreatePrimitive(frameGroup, PrimitiveType.Cube, "AtriumRail_FloorSlot",
            new Vector3(2.84f, 0.015f, 0f), Vector3.zero, new Vector3(0.22f, 0.03f, 8.92f), mats.EmissiveDormant);
        var railNorthAnchor = PassageDesLanternesAssets.CreatePrimitive(frameGroup, PrimitiveType.Cube, "AtriumRail_NorthAnchorBay5",
            new Vector3(2.96f, 2.86f, 4.30f), Vector3.zero, new Vector3(0.66f, 0.56f, 0.34f), mats.EmissiveDormant);

        // 3. SINGLE SLIDING PARTITION with ONE 1.8m Arched Opening (localZ in [-0.9, +0.9]) and TWO 2.2m Solid Wings!
        var partDef = definition.atriumPartition;
        var slidingRoot = new GameObject("Atrium Sliding Partition");
        slidingRoot.transform.SetParent(atriumGroup, false);
        var startZ = partDef.initialPosition == AtriumPartitionPosition.EntryToIsland
            ? partDef.entryPositionWorldZ
            : partDef.exitPositionWorldZ;
        slidingRoot.transform.localPosition = new Vector3(partDef.railWorldX, 0f, startZ);

        // North solid wing: localZ in [+0.9, +3.26] (length 2.36m, 16cm overlap behind fixed wing -> zero parallax gap!)
        PassageDesLanternesAssets.CreatePrimitive(slidingRoot.transform, PrimitiveType.Cube, "PartitionWing_North",
            new Vector3(0f, 1.62f, 2.08f), Vector3.zero, new Vector3(0.24f, 3.24f, 2.36f), mats.StoneTrim, false);
        // South solid wing: localZ in [-3.26, -0.9] (length 2.36m, 16cm overlap behind fixed wing -> zero parallax gap!)
        PassageDesLanternesAssets.CreatePrimitive(slidingRoot.transform, PrimitiveType.Cube, "PartitionWing_South",
            new Vector3(0f, 1.62f, -2.08f), Vector3.zero, new Vector3(0.24f, 3.24f, 2.36f), mats.StoneTrim, false);
        // Carved bronze bands on the solid wings so the sliding wall reads unmistakably in motion
        PassageDesLanternesAssets.CreatePrimitive(slidingRoot.transform, PrimitiveType.Cube, "PartitionBand_North",
            new Vector3(0f, 1.62f, 2.08f), Vector3.zero, new Vector3(0.29f, 0.16f, 2.12f), mats.BronzeMetal);
        PassageDesLanternesAssets.CreatePrimitive(slidingRoot.transform, PrimitiveType.Cube, "PartitionBand_South",
            new Vector3(0f, 1.62f, -2.08f), Vector3.zero, new Vector3(0.29f, 0.16f, 2.12f), mats.BronzeMetal);
        // Single Opening overhead arch beam: localZ in [-0.9, +0.9] (height Y in [2.76, 3.24])
        PassageDesLanternesAssets.CreatePrimitive(slidingRoot.transform, PrimitiveType.Cube, "PartitionOpening_Lintel",
            new Vector3(0f, 3.00f, 0f), Vector3.zero, new Vector3(0.28f, 0.48f, 1.84f), mats.BronzeMetal, false);

        // Glowing trim framing the single opening so it reads instantly from Entry, Island, and Far Side
        var trimL = PassageDesLanternesAssets.CreatePrimitive(slidingRoot.transform, PrimitiveType.Cube, "OpeningTrim_N",
            new Vector3(0f, 1.40f, 0.90f), Vector3.zero, new Vector3(0.30f, 2.80f, 0.08f), mats.EmissiveDormant);
        var trimR = PassageDesLanternesAssets.CreatePrimitive(slidingRoot.transform, PrimitiveType.Cube, "OpeningTrim_S",
            new Vector3(0f, 1.40f, -0.90f), Vector3.zero, new Vector3(0.30f, 2.80f, 0.08f), mats.EmissiveDormant);
        var trimTop = PassageDesLanternesAssets.CreatePrimitive(slidingRoot.transform, PrimitiveType.Cube, "OpeningTrim_Top",
            new Vector3(0f, 2.78f, 0f), Vector3.zero, new Vector3(0.32f, 0.10f, 1.88f), mats.EmissiveDormant);

        var partitionWorldView = atriumGroup.gameObject.AddComponent<AtriumPartitionWorldView>();
        partitionWorldView.mazeState = mazeState;
        partitionWorldView.mazeAudio = mazeAudio;
        partitionWorldView.haptics = haptics;
        partitionWorldView.slidingPartitionRoot = slidingRoot.transform;
        partitionWorldView.railLocalX = partDef.railWorldX;
        partitionWorldView.entryPositionLocalZ = partDef.entryPositionWorldZ;
        partitionWorldView.exitPositionLocalZ = partDef.exitPositionWorldZ;
        partitionWorldView.slideSeconds = partDef.slideSeconds;
        partitionWorldView.archTrimRenderers = new[]
        {
            trimL.GetComponent<Renderer>(),
            trimR.GetComponent<Renderer>(),
            trimTop.GetComponent<Renderer>(),
        };
        partitionWorldView.railTrackRenderers = new[]
        {
            railFloor.GetComponent<Renderer>(),
            railNorthAnchor.GetComponent<Renderer>(),
        };

        // 4. Floor Crossing Guide Chevrons & Threshold Safety Warning Strips
        var northGuideA = PassageDesLanternesAssets.CreatePrimitive(atriumGroup, PrimitiveType.Cube, "Guide_North_EntrySide",
            new Vector3(2.22f, 0.014f, 1.1f), Vector3.zero, new Vector3(0.28f, 0.012f, 1.45f), mats.EmissiveDormant);
        var northGuideB = PassageDesLanternesAssets.CreatePrimitive(atriumGroup, PrimitiveType.Cube, "Guide_North_IslandSide",
            new Vector3(3.75f, 0.014f, 1.1f), Vector3.zero, new Vector3(0.28f, 0.012f, 1.45f), mats.EmissiveDormant);
        var southGuideA = PassageDesLanternesAssets.CreatePrimitive(atriumGroup, PrimitiveType.Cube, "Guide_South_FarSide",
            new Vector3(2.22f, 0.014f, -1.1f), Vector3.zero, new Vector3(0.28f, 0.012f, 1.45f), mats.EmissiveDormant);
        var southGuideB = PassageDesLanternesAssets.CreatePrimitive(atriumGroup, PrimitiveType.Cube, "Guide_South_IslandSide",
            new Vector3(3.75f, 0.014f, -1.1f), Vector3.zero, new Vector3(0.28f, 0.012f, 1.45f), mats.EmissiveDormant);
        var warnN = PassageDesLanternesAssets.CreatePrimitive(atriumGroup, PrimitiveType.Cube, "ThresholdWarning_N",
            new Vector3(3.00f, 0.018f, 1.1f), Vector3.zero, new Vector3(0.75f, 0.012f, 1.55f), mats.EmissiveDormant);
        var warnS = PassageDesLanternesAssets.CreatePrimitive(atriumGroup, PrimitiveType.Cube, "ThresholdWarning_S",
            new Vector3(3.00f, 0.018f, -1.1f), Vector3.zero, new Vector3(0.75f, 0.012f, 1.55f), mats.EmissiveDormant);

        crossingController.northCrossingGuideRenderers = new[] { northGuideA.GetComponent<Renderer>(), northGuideB.GetComponent<Renderer>() };
        crossingController.southCrossingGuideRenderers = new[] { southGuideA.GetComponent<Renderer>(), southGuideB.GetComponent<Renderer>() };
        crossingController.thresholdWarningRenderers = new[] { warnN.GetComponent<Renderer>(), warnS.GetComponent<Renderer>() };

        // Concise French cue plaque on the Atrium Entry Deck explaining the sliding handle
        var atriumPlaque = PassageDesLanternesAssets.CreateWorldPlaque(
            atriumGroup, "Plaque_AtriumRail",
            new Vector3(0.65f, 1.55f, 0.24f), new Vector3(0f, 180f, 0f),
            1.36f, 0.44f,
            "La Cloison Coulissante",
            "Glissez la poignée sur la maquette (ou visez-la + B)\nL'îlot central reste sûr dans les deux positions",
            mats, 44, 30);
        atriumPlaque.sessionState = sessionState;
        atriumPlaque.filterByProgressionStage = true;
        atriumPlaque.minVisibleStage = ProgressionStage.ReachAtriumIsland;
        atriumPlaque.maxVisibleStage = ProgressionStage.CrossToAtriumFarSide;
    }

    private static void BuildExitLever(Transform mazeRoot, PassagePrefabSet prefabs, PassageMaterialSet mats,
        MazeState mazeState, MazeSessionState sessionState, ProceduralMazeAudio mazeAudio, XRHapticsService haptics,
        XRInteractionManager interactionManager)
    {
        var leverObj = InstantiatePrefab(prefabs.ExitLever, mazeRoot,
            new Vector3(1.15f, 0f, -3.25f), Quaternion.Euler(0f, -145f, 0f), "Full-Size Exit Lever");
        var leverView = leverObj.GetComponent<ExitLeverView>();
        leverView.mazeState = mazeState;
        leverView.mazeAudio = mazeAudio;
        leverView.haptics = haptics;

        // Rotated 180 deg so readable front (-Z local) faces +Z (North) toward the player on the Atrium Far Side!
        var leverPlaque = PassageDesLanternesAssets.CreateWorldPlaque(
            mazeRoot, "Plaque_ExitLever",
            new Vector3(1.25f, 1.98f, -4.22f), new Vector3(0f, 180f, 0f),
            1.22f, 0.40f,
            "Mécanisme de l'Aube",
            "Abaissez le levier pour déverrouiller la sortie",
            mats, 44, 30);
        leverPlaque.sessionState = sessionState;
        leverPlaque.filterByProgressionStage = true;
        leverPlaque.minVisibleStage = ProgressionStage.PullExitLever;
        leverPlaque.maxVisibleStage = ProgressionStage.PullExitLever;
    }

    private static CompletionSequenceView BuildExitSanctuaryAndRouteBeacons(
        Transform demoRoot, Transform mazeRoot, PassagePrefabSet prefabs, PassageMaterialSet mats,
        MazeState mazeState, MazeSessionState sessionState, ProceduralMazeAudio mazeAudio, XRHapticsService haptics,
        TeleportationProvider teleportationProvider, XROrigin xrOrigin, XRInteractionManager interactionManager)
    {
        // 1. Ordered Route Beacons tracing the complete solved journey across the world maze
        var beaconsGroup = new GameObject("Solved Route Beacons").transform;
        beaconsGroup.SetParent(mazeRoot, false);
        Vector3[] beaconPositions =
        {
            new Vector3(-3.0f, 0f, -5.6f), // Start
            new Vector3(-3.0f, 0f, -2.5f), // Corridor-B
            new Vector3(-3.0f, 0f,  0.0f), // Vestibule
            new Vector3(-5.8f, 0f,  3.0f), // Gallery West
            new Vector3( 3.0f, 0f,  5.8f), // Bay 5 (Arche)
            new Vector3(-1.8f, 0f,  3.0f), // Atrium Lock
            new Vector3( 1.2f, 0f,  2.2f), // Atrium Entry
            new Vector3( 5.2f, 0f,  0.0f), // Central Island
            new Vector3( 1.0f, 0f, -2.2f), // Atrium Far Side
            new Vector3( 0.0f, 0f, -5.8f), // Exit Sanctuary
        };

        var beaconRenderers = new Renderer[beaconPositions.Length];
        for (var i = 0; i < beaconPositions.Length; i++)
        {
            var beacon = InstantiatePrefab(prefabs.RouteBeacon, beaconsGroup, beaconPositions[i], Quaternion.identity, $"RouteBeacon_{i}");
            var disc = beacon.transform.Find("GlowDisc");
            beaconRenderers[i] = disc != null ? disc.GetComponent<Renderer>() : beacon.GetComponentInChildren<Renderer>();
        }

        // 2. Exit Sanctuary Landmark Pedestal, Halo Beacon, Completion Board, and Replay Button
        var sanctuary = new GameObject("Exit Sanctuary").transform;
        sanctuary.SetParent(mazeRoot, false);
        sanctuary.localPosition = new Vector3(2.45f, 0f, -6.0f);

        PassageDesLanternesAssets.CreatePrimitive(sanctuary, PrimitiveType.Cylinder, "SanctuaryDais",
            new Vector3(0f, 0.06f, 0f), Vector3.zero, new Vector3(1.85f, 0.06f, 1.85f), mats.StoneTrim, false);
        PassageDesLanternesAssets.CreatePrimitive(sanctuary, PrimitiveType.Cylinder, "BeaconColumn",
            new Vector3(0.45f, 0.55f, 0f), Vector3.zero, new Vector3(0.48f, 0.50f, 0.48f), mats.StoneDark, false);

        var haloRoot = new GameObject("SanctuaryGoldenHalo").transform;
        haloRoot.SetParent(sanctuary, false);
        haloRoot.localPosition = new Vector3(0.45f, 2.35f, 0f);
        var haloA = PassageDesLanternesAssets.CreatePrimitive(haloRoot, PrimitiveType.Cube, "HaloDiamondA",
            Vector3.zero, new Vector3(45f, 0f, 45f), new Vector3(0.42f, 0.42f, 0.42f), mats.EmissiveGold);
        var haloB = PassageDesLanternesAssets.CreatePrimitive(haloRoot, PrimitiveType.Cube, "HaloRing",
            Vector3.zero, new Vector3(0f, 45f, 0f), new Vector3(0.68f, 0.06f, 0.68f), mats.EmissiveGold);

        var lightObj = new GameObject("SanctuaryBeaconLight");
        lightObj.transform.SetParent(sanctuary, false);
        lightObj.transform.localPosition = new Vector3(0.15f, 2.15f, 0f);
        var beaconLight = lightObj.AddComponent<Light>();
        beaconLight.type = LightType.Point;
        beaconLight.color = new Color(1f, 0.80f, 0.36f, 1f);
        beaconLight.intensity = 0.8f;
        beaconLight.range = 6.5f;
        beaconLight.shadows = LightShadows.None;
        beaconLight.bounceIntensity = 0f;

        // Dormant stone inscription visible through the viewing bars from Start
        var dormantPlaque = PassageDesLanternesAssets.CreateWorldPlaque(
            sanctuary, "DormantInscription",
            new Vector3(0.35f, 1.56f, 0f), new Vector3(0f, 90f, 0f),
            1.28f, 0.44f,
            "Sanctuaire de l'Aube",
            "Trouvez le levier de l'autre côté de l'atrium",
            mats, 46, 32);

        // Completion panel (revealed on DemoCompleted; starts hidden cleanly in Edit Mode!)
        var completedPanel = PassageDesLanternesAssets.CreateWorldPlaque(
            sanctuary, "CompletedSummaryPlaque",
            new Vector3(0.35f, 1.62f, 0f), new Vector3(0f, 90f, 0f),
            1.42f, 0.64f,
            "Le Passage des Lanternes",
            "Traversée accomplie\nTemps de traversée : 00:00",
            mats, 50, 36);
        completedPanel.gameObject.SetActive(false);

        // Clean stone lectern holding the intentional Replay button at ergonomic waist/chest height
        PassageDesLanternesAssets.CreatePrimitive(sanctuary, PrimitiveType.Cube, "ReplayLectern",
            new Vector3(-0.36f, 0.50f, 0f), Vector3.zero, new Vector3(0.36f, 0.92f, 0.44f), mats.StoneDark, false);
        PassageDesLanternesAssets.CreatePrimitive(sanctuary, PrimitiveType.Cube, "ReplayLecternCap",
            new Vector3(-0.36f, 0.98f, 0f), Vector3.zero, new Vector3(0.42f, 0.06f, 0.50f), mats.StoneTrim, false);

        var replayBtnObj = InstantiatePrefab(prefabs.WorldButton, sanctuary,
            new Vector3(-0.36f, 1.01f, 0f), Quaternion.Euler(0f, 90f, 0f), "ReplayButton");
        var replayBtn = replayBtnObj.GetComponent<XRWorldButtonInteractable>();
        replayBtn.mazeAudio = mazeAudio;
        replayBtn.haptics = haptics;

        PassageDesLanternesAssets.CreateWorldPlaque(
            sanctuary, "Plaque_ReplayLabel",
            new Vector3(-0.59f, 0.72f, 0f), new Vector3(0f, 90f, 0f),
            0.54f, 0.20f,
            "Recommencer",
            string.Empty,
            mats, 40, 20);

        var boardView = sanctuary.gameObject.AddComponent<CompletionBoardView>();
        boardView.sessionState = sessionState;
        boardView.completedPanelRoot = completedPanel.gameObject;
        boardView.dormantInscriptionRoot = dormantPlaque.gameObject;
        boardView.elapsedTimeText = completedPanel.bodyText;
        boardView.replayButton = replayBtn;

        var seqObj = new GameObject("Completion Sequence Controller");
        seqObj.transform.SetParent(demoRoot, false);
        var seq = seqObj.AddComponent<CompletionSequenceView>();
        seq.mazeState = mazeState;
        seq.sessionState = sessionState;
        seq.mazeAudio = mazeAudio;
        seq.haptics = haptics;
        seq.completionBoardView = boardView;
        seq.teleportationProvider = teleportationProvider;
        seq.xrOrigin = xrOrigin;
        seq.worldMazeRoot = mazeRoot;
        seq.routeBeaconRenderers = beaconRenderers;
        seq.sanctuaryBeaconLight = beaconLight;
        seq.sanctuaryHaloTransform = haloRoot;
        seq.sanctuaryAccentRenderers = new[] { haloA.GetComponent<Renderer>(), haloB.GetComponent<Renderer>() };
        return seq;
    }

    private static Transform BuildMiniature(
        Transform demoRoot, PassageMaterialSet mats, MazeDefinition definition, MazeState mazeState,
        ProceduralMazeAudio mazeAudio, XRHapticsService haptics, MazeScaleMapper mapper, Transform headCamera,
        Transform trackedLantern, XRInteractionManager interactionManager,
        out MiniatureLanternView miniatureLanternView, out HintConsoleView hintConsoleView, out int targetLayer)
    {
        var s = definition.metresToMiniatureScale; // 0.01f (1:100)
        targetLayer = EnsureLayer("MiniatureTarget");

        var miniRoot = new GameObject("Left Hand Miniature").transform;
        miniRoot.SetParent(demoRoot, false);
        miniRoot.localPosition = new Vector3(-3.0f, 1.15f, -5.65f);
        miniRoot.localRotation = Quaternion.identity;
        miniRoot.localScale = Vector3.one;

        // Compact diorama plinth & maze baseplate (15cm x 15cm at 1:100)
        PassageDesLanternesAssets.CreatePrimitive(miniRoot, PrimitiveType.Cube, "MiniaturePlinth",
            new Vector3(0f, -0.005f, 0f), Vector3.zero, new Vector3(15.6f * s, 0.008f, 15.6f * s), mats.StoneDark);
        PassageDesLanternesAssets.CreatePrimitive(miniRoot, PrimitiveType.Cube, "MiniatureBaseRim",
            new Vector3(0f, -0.0045f, 0f), Vector3.zero, new Vector3(15.9f * s, 0.005f, 15.9f * s), mats.BronzeMetal);

        // Floor slabs matching playable areas on miniature
        CreateMiniBox(miniRoot, "MiniFloor_StartSpine", new Vector3(-3.0f, 0f, -1.5f), new Vector3(3.0f, 0.15f, 12.0f), s, mats.MiniatureFloor);
        CreateMiniBox(miniRoot, "MiniFloor_GalleryWest", new Vector3(-6.0f, 0f, 3.0f), new Vector3(3.0f, 0.15f, 9.0f), s, mats.MiniatureFloor);
        CreateMiniBox(miniRoot, "MiniFloor_GalleryNorth", new Vector3(0.0f, 0f, 6.0f), new Vector3(15.0f, 0.15f, 3.0f), s, mats.MiniatureFloor);
        CreateMiniBox(miniRoot, "MiniFloor_AtriumWest", new Vector3(0.75f, 0f, 0.0f), new Vector3(4.5f, 0.15f, 9.0f), s, mats.MiniatureFloor);
        CreateMiniBox(miniRoot, "MiniFloor_AtriumAbyss", new Vector3(5.38f, -0.18f, 0.0f), new Vector3(4.1f, 0.10f, 9.0f), s, mats.AbyssChasm);
        CreateMiniBox(miniRoot, "MiniFloor_CentralIsland", new Vector3(5.38f, 0.04f, 0.0f), new Vector3(3.6f, 0.22f, 4.9f), s, mats.MiniatureFloor);
        CreateMiniBox(miniRoot, "MiniFloor_ExitSanctuary", new Vector3(1.5f, 0f, -6.0f), new Vector3(6.0f, 0.15f, 3.0f), s, mats.MiniatureFloor);

        // Simplified white miniature maze walls (matching exact world coordinates at 1:100!)
        var miniWalls = new GameObject("Miniature Walls").transform;
        miniWalls.SetParent(miniRoot, false);
        float wallH = 3.2f;
        float wallT = 0.36f;

        // Horizontal walls (X span, Z pos)
        (float xMin, float xMax, float z)[] miniHWalls =
        {
            (-4.5f, 4.5f, -7.5f), // South outer
            ( 1.5f, 7.5f, -4.5f), // Atrium South E
            (-7.5f, -4.5f, -1.5f),// Gallery SW South (Bay 1)
            (-4.5f, 7.5f, 4.5f),  // North Gallery / Atrium divider (Bays 4, 5, 6!)
            (-7.5f, 7.5f, 7.5f),  // North outer
            (-1.5f, 2.85f, 0.0f), // ATRIUM FIXED BARRIER (clearly darker bronze-slate!)
        };
        for (var i = 0; i < miniHWalls.Length; i++)
        {
            var hw = miniHWalls[i];
            var isFixedBarrier = i == miniHWalls.Length - 1;
            var mat = isFixedBarrier ? mats.StoneDark : mats.MiniatureWall;
            CreateMiniBox(miniWalls, $"MiniHWall_{i}",
                new Vector3((hw.xMin + hw.xMax) * 0.5f, wallH * 0.5f, hw.z),
                new Vector3(hw.xMax - hw.xMin, wallH, wallT), s, mat);
        }

        // Vertical walls (X pos, Z span)
        (float x, float zMin, float zMax)[] miniVWalls =
        {
            (-4.5f, -7.5f, -1.5f),
            (-4.5f,  1.5f,  4.5f),
            (-7.5f, -1.5f,  7.5f),
            (-1.5f, -4.5f,  1.5f),
            ( 4.5f, -7.5f, -4.5f),
            ( 7.5f, -4.5f,  7.5f),
            // Fixed Rail Wing Walls along X = +3.18
            ( 3.18f,  2.0f,  4.5f),
            ( 3.18f, -0.2f,  0.2f),
            ( 3.18f, -4.5f, -2.0f),
        };
        for (var i = 0; i < miniVWalls.Length; i++)
        {
            var vw = miniVWalls[i];
            CreateMiniBox(miniWalls, $"MiniVWall_{i}",
                new Vector3(vw.x, wallH * 0.5f, (vw.zMin + vw.zMax) * 0.5f),
                new Vector3(wallT, wallH, vw.zMax - vw.zMin), s, mats.MiniatureWall);
        }

        // 1. Miniature intro gate ("center_gate") with matching diamond emblem and B-button target collider.
        var introTargetObj = new GameObject("Intro Gate Miniature Target");
        introTargetObj.layer = targetLayer;
        introTargetObj.transform.SetParent(miniRoot, false);
        introTargetObj.transform.localPosition = new Vector3(-3.0f * s, 1.6f * s, -4.5f * s);
        var introCol = introTargetObj.AddComponent<BoxCollider>();
        introCol.isTrigger = true;
        introCol.size = new Vector3(0.055f, 0.065f, 0.055f);

        var introVisual = PassageDesLanternesAssets.CreatePrimitive(introTargetObj.transform, PrimitiveType.Cube, "IntroGateMiniVisual",
            Vector3.zero, Vector3.zero, new Vector3(2.9f * s, 3.2f * s, 0.42f * s), mats.MiniatureWall);
        var introDiamond = PassageDesLanternesAssets.CreatePrimitive(introVisual.transform, PrimitiveType.Cube, "IntroGateMiniDiamond",
            new Vector3(0f, 0.1f, -0.58f), new Vector3(0f, 0f, 45f), new Vector3(0.32f, 0.32f, 0.45f), mats.EmissiveGold);
        var introPad = PassageDesLanternesAssets.CreatePrimitive(introTargetObj.transform, PrimitiveType.Cube, "IntroGateLocationPad",
            new Vector3(0f, -1.52f * s, 0f), Vector3.zero, new Vector3(3.2f * s, 0.2f * s, 1.0f * s), mats.MiniatureAccent);

        var introTarget = introTargetObj.AddComponent<MiniatureWallTarget>();
        introTarget.targetKind = MiniatureTargetKind.ToggleGate;
        introTarget.mechanismId = MazeMechanismIds.IntroGate;
        introTarget.highlightRenderers = new[] { introVisual.GetComponent<Renderer>(), introPad.GetComponent<Renderer>() };

        var introMiniView = introTargetObj.AddComponent<MiniatureMechanismView>();
        introMiniView.mazeState = mazeState;
        introMiniView.mechanismId = MazeMechanismIds.IntroGate;
        introMiniView.visual = introVisual.transform;
        introMiniView.raisedLocalPosition = introVisual.transform.localPosition;
        introMiniView.loweredLocalOffset = Vector3.down * (3.6f * s);
        introMiniView.animationSeconds = 0.32f;

        // 2. MINIATURE ATRIUM LOCK GATE (non-interactive view synced to MazeState)
        var atriumLockMiniRoot = new GameObject("Atrium Lock Miniature View");
        atriumLockMiniRoot.transform.SetParent(miniRoot, false);
        atriumLockMiniRoot.transform.localPosition = new Vector3(-1.5f * s, 1.5f * s, 3.0f * s);
        var atriumLockVisual = PassageDesLanternesAssets.CreatePrimitive(atriumLockMiniRoot.transform, PrimitiveType.Cube, "AtriumLockMiniVisual",
            Vector3.zero, Vector3.zero, new Vector3(0.45f * s, 3.0f * s, 2.6f * s), mats.BronzeMetal);
        var atriumLockView = atriumLockMiniRoot.AddComponent<MiniatureMechanismView>();
        atriumLockView.mazeState = mazeState;
        atriumLockView.mechanismId = MazeMechanismIds.AtriumLock;
        atriumLockView.visual = atriumLockVisual.transform;
        atriumLockView.raisedLocalPosition = atriumLockVisual.transform.localPosition;
        atriumLockView.loweredLocalOffset = Vector3.down * (3.4f * s);
        atriumLockView.animationSeconds = 0.4f;

        // 3. MINIATURE EXIT GATE & GOLDEN CROWN ARCH LANDMARK (visible right next to Spawn!)
        var exitLandmarkArch = CreateMiniBox(miniRoot, "MiniLandmark_ExitCrownArch",
            new Vector3(0.0f, 3.35f, -4.5f), new Vector3(3.1f, 0.65f, 0.65f), s, mats.EmissiveGold);
        var exitGateMiniRoot = new GameObject("Exit Gate Miniature View");
        exitGateMiniRoot.transform.SetParent(miniRoot, false);
        exitGateMiniRoot.transform.localPosition = new Vector3(0.0f, 1.5f * s, -4.5f * s);
        var exitGateVisual = PassageDesLanternesAssets.CreatePrimitive(exitGateMiniRoot.transform, PrimitiveType.Cube, "ExitGateMiniVisual",
            Vector3.zero, Vector3.zero, new Vector3(2.6f * s, 2.8f * s, 0.42f * s), mats.BronzeMetal);
        var exitGateView = exitGateMiniRoot.AddComponent<MiniatureMechanismView>();
        exitGateView.mazeState = mazeState;
        exitGateView.mechanismId = MazeMechanismIds.ExitGate;
        exitGateView.visual = exitGateVisual.transform;
        exitGateView.raisedLocalPosition = exitGateVisual.transform.localPosition;
        exitGateView.loweredLocalOffset = Vector3.down * (3.2f * s);
        exitGateView.animationSeconds = 0.45f;

        // 4. MINIATURE ATRIUM RAIL & REVEALABLE SLIDING PARTITION WITH FORGIVING GRAB HANDLE!
        var partDef = definition.atriumPartition;
        var miniRailTrack = CreateMiniBox(miniRoot, "MiniatureAtriumRailTrack",
            new Vector3(partDef.railWorldX, 0.38f, 0.0f), new Vector3(0.75f, 0.35f, 8.95f), s, mats.MiniatureAccent);
        var miniRailTopBar = CreateMiniBox(miniRoot, "MiniatureAtriumRailTopBar",
            new Vector3(partDef.railWorldX, 3.45f, 0.0f), new Vector3(0.45f, 0.28f, 8.95f), s, mats.MiniatureAccent);
        // Prominent rail anchor bracket visibly riveted straight into the back of Bay 5 (X=+3.0, Z=+4.5)!
        var miniRailBay5Joint = CreateMiniBox(miniRoot, "MiniatureAtriumRail_Bay5Joint",
            new Vector3(3.0f, 2.35f, 4.46f), new Vector3(1.15f, 2.4f, 1.05f), s, mats.EmissiveGold);

        // Snap detent indicators at Entry (+1.1) and Exit (-1.1)
        var entryDetent = CreateMiniBox(miniRoot, "MiniDetent_Entry",
            new Vector3(2.05f, 0.38f, partDef.entryPositionWorldZ), new Vector3(0.85f, 0.30f, 1.5f), s, mats.MiniatureAccent);
        var exitDetent = CreateMiniBox(miniRoot, "MiniDetent_Exit",
            new Vector3(2.05f, 0.38f, partDef.exitPositionWorldZ), new Vector3(0.85f, 0.30f, 1.5f), s, mats.MiniatureAccent);

        var miniSlidingRoot = new GameObject("Miniature Sliding Partition").transform;
        miniSlidingRoot.SetParent(miniRoot, false);
        var initMiniZ = (partDef.initialPosition == AtriumPartitionPosition.EntryToIsland
            ? partDef.entryPositionWorldZ
            : partDef.exitPositionWorldZ) * s;
        miniSlidingRoot.localPosition = new Vector3(partDef.railWorldX * s, 0f, initMiniZ);

        // Miniature partition solid wings + glowing opening lintel (1:100 replica of world partition!)
        CreateMiniBox(miniSlidingRoot, "MiniPartWing_North", new Vector3(0f, 1.55f, 2.08f), new Vector3(0.36f, 3.0f, 2.34f), s, mats.BronzeMetal);
        CreateMiniBox(miniSlidingRoot, "MiniPartWing_South", new Vector3(0f, 1.55f, -2.08f), new Vector3(0.36f, 3.0f, 2.34f), s, mats.BronzeMetal);
        CreateMiniBox(miniSlidingRoot, "MiniPartOpening_Lintel", new Vector3(0f, 2.85f, 0f), new Vector3(0.48f, 0.45f, 1.85f), s, mats.EmissiveTeal);

        var lockedClasp = CreateMiniBox(miniSlidingRoot, "MiniPartLockedClasp",
            new Vector3(0f, 3.55f, 0f), new Vector3(0.95f, 0.65f, 0.95f), s, mats.StoneDark);

        // The unlocked handle supports direct grabbing. The miniature pointer and B also move the partition.
        // Starts hidden in Edit Mode matching startsLocked=true; smoothly rises when Bay 5 is solved!
        var grabHandleObj = new GameObject("Miniature Partition Grab Handle");
        grabHandleObj.transform.SetParent(miniSlidingRoot, false);
        grabHandleObj.transform.localPosition = new Vector3(0f, 3.9f * s, 0f);
        PassageDesLanternesAssets.CreatePrimitive(grabHandleObj.transform, PrimitiveType.Cylinder, "HandleStem",
            new Vector3(0f, -0.004f, 0f), Vector3.zero, new Vector3(0.006f, 0.006f, 0.006f), mats.BronzeMetal);
        var handleKnob = PassageDesLanternesAssets.CreatePrimitive(grabHandleObj.transform, PrimitiveType.Cube, "HandleKnob",
            new Vector3(0f, 0.005f, 0f), Vector3.zero, new Vector3(0.016f, 0.012f, 0.024f), mats.MiniatureAccent);
        var handleRidge = PassageDesLanternesAssets.CreatePrimitive(grabHandleObj.transform, PrimitiveType.Cube, "HandleRidge",
            new Vector3(0f, 0.011f, 0f), Vector3.zero, new Vector3(0.018f, 0.004f, 0.006f), mats.EmissiveGold);

        // Non-trigger collider on Default layer for Right-hand NearFarInteractor direct grab-to-slide!
        var grabCol = grabHandleObj.AddComponent<BoxCollider>();
        grabCol.isTrigger = false;
        grabCol.center = new Vector3(0f, 0.004f, 0f);
        grabCol.size = new Vector3(0.036f, 0.032f, 0.042f);

        var handleSimple = grabHandleObj.AddComponent<XRSimpleInteractable>();
        handleSimple.interactionLayers = InteractionLayerMask.GetMask("Default");
        grabHandleObj.AddComponent<RightHandOnlyFilter>();
        if (partDef.startsLocked)
            grabHandleObj.SetActive(false);

        // Trigger collider on MiniatureTarget for the right-hand pointer and B-button action.
        var partPointerObj = new GameObject("PartitionPointerTarget");
        partPointerObj.layer = targetLayer;
        partPointerObj.transform.SetParent(miniSlidingRoot, false);
        partPointerObj.transform.localPosition = new Vector3(0f, 3.2f * s, 0f);
        var partPointerCol = partPointerObj.AddComponent<BoxCollider>();
        partPointerCol.isTrigger = true;
        partPointerCol.size = new Vector3(0.045f, 0.055f, 0.065f);
        var partPointerTarget = partPointerObj.AddComponent<MiniatureWallTarget>();
        partPointerTarget.targetKind = MiniatureTargetKind.ToggleAtriumPartition;
        partPointerTarget.mechanismId = MazeMechanismIds.AtriumPartition;
        partPointerTarget.highlightRenderers = new[] { handleKnob.GetComponent<Renderer>(), handleRidge.GetComponent<Renderer>() };

        var miniPartitionView = miniRoot.gameObject.AddComponent<MiniaturePartitionView>();
        miniPartitionView.mazeState = mazeState;
        miniPartitionView.mazeAudio = mazeAudio;
        miniPartitionView.haptics = haptics;
        miniPartitionView.partitionTrackRoot = miniRoot;
        miniPartitionView.slidingVisualRoot = miniSlidingRoot;
        miniPartitionView.grabHandleRoot = grabHandleObj.transform;
        miniPartitionView.grabHandleInteractable = handleSimple;
        miniPartitionView.grabHandleCollider = grabCol;
        miniPartitionView.pointerTarget = partPointerTarget;
        miniPartitionView.lockedClaspVisual = lockedClasp;
        miniPartitionView.entryDetentRenderer = entryDetent.GetComponent<Renderer>();
        miniPartitionView.exitDetentRenderer = exitDetent.GetComponent<Renderer>();
        miniPartitionView.handleGlowRenderers = new[] { handleKnob.GetComponent<Renderer>(), handleRidge.GetComponent<Renderer>() };

        // 5. MINIATURE BAY LANDMARKS (6 recognizable geometric markers matching the 6 full-size bays!)
        var landmarkEntries = new List<MiniatureLandmarkEntry>
        {
            new MiniatureLandmarkEntry
            {
                landmarkId = MazeMechanismIds.IntroGate,
                renderers = new[] { introDiamond.GetComponent<Renderer>(), introPad.GetComponent<Renderer>() },
                baseColor = new Color(1f, 0.84f, 0.42f, 1f),
            },
            new MiniatureLandmarkEntry
            {
                landmarkId = MazeMechanismIds.AtriumPartition,
                renderers = new[] { handleKnob.GetComponent<Renderer>(), miniRailTrack.GetComponent<Renderer>() },
                baseColor = new Color(0.32f, 0.88f, 0.92f, 1f),
            },
            new MiniatureLandmarkEntry
            {
                landmarkId = MazeMechanismIds.ExitGate,
                renderers = new[] { exitLandmarkArch.GetComponent<Renderer>() },
                baseColor = new Color(1f, 0.84f, 0.42f, 1f),
            },
        };

        var miniPedestalMark = CreateMiniBox(miniRoot, "MiniLandmark_LanternPedestal",
            new Vector3(-3.0f, 0.65f, 0.0f), new Vector3(0.8f, 1.0f, 0.8f), s, mats.MiniatureAccent);
        landmarkEntries.Add(new MiniatureLandmarkEntry
        {
            landmarkId = "lantern_pedestal",
            renderers = new[] { miniPedestalMark.GetComponent<Renderer>() },
            baseColor = new Color(0.95f, 0.72f, 0.3f, 1f),
        });

        var miniLeverMark = CreateMiniBox(miniRoot, "MiniLandmark_ExitLever",
            new Vector3(1.15f, 0.85f, -3.25f), new Vector3(0.75f, 1.4f, 0.75f), s, mats.MiniatureAccent);
        landmarkEntries.Add(new MiniatureLandmarkEntry
        {
            landmarkId = "exit_lever",
            renderers = new[] { miniLeverMark.GetComponent<Renderer>() },
            baseColor = new Color(0.95f, 0.72f, 0.3f, 1f),
        });

        var bayLandmarksGroup = new GameObject("Miniature Bay Landmarks").transform;
        bayLandmarksGroup.SetParent(miniRoot, false);
        foreach (var bayDef in definition.lanternBays)
        {
            var markerRoot = new GameObject($"MiniBay_{bayDef.id}").transform;
            markerRoot.SetParent(bayLandmarksGroup, false);
            var markerPos = bayDef.worldCenter + Vector3.up * 3.75f + bayDef.inwardNormal * 0.65f;
            markerRoot.localPosition = markerPos * s;
            markerRoot.localRotation = Quaternion.Euler(90f, 0f, 0f); // Flat and crisp when looking down at the miniature!

            var emblemRenderers = PassageDesLanternesAssets.BuildBayEmblemGeometry(
                markerRoot, bayDef.emblemKind, mats.BronzeMetal, 0.052f);
            landmarkEntries.Add(new MiniatureLandmarkEntry
            {
                landmarkId = bayDef.id,
                renderers = emblemRenderers,
                baseColor = bayDef.emblemColor,
            });
        }

        // 6. MINIATURE SOLVED-ROUTE LUMINOUS THREAD (lights up in sync with world route cascade!)
        var routeGroup = new GameObject("Miniature Solved Route Thread").transform;
        routeGroup.SetParent(miniRoot, false);
        Vector3[] miniRoutePoints =
        {
            new Vector3(-3.0f, 0.28f, -5.6f),
            new Vector3(-3.0f, 0.28f, -2.5f),
            new Vector3(-3.0f, 0.28f,  0.0f),
            new Vector3(-5.8f, 0.28f,  3.0f),
            new Vector3( 3.0f, 0.28f,  5.8f),
            new Vector3(-1.8f, 0.28f,  3.0f),
            new Vector3( 1.2f, 0.28f,  2.2f),
            new Vector3( 5.2f, 0.28f,  0.0f),
            new Vector3( 1.0f, 0.28f, -2.2f),
            new Vector3( 0.0f, 0.28f, -5.8f),
        };
        var miniRouteRenderers = new Renderer[miniRoutePoints.Length];
        for (var i = 0; i < miniRoutePoints.Length; i++)
        {
            var dot = PassageDesLanternesAssets.CreatePrimitive(routeGroup, PrimitiveType.Sphere, $"MiniRouteNode_{i}",
                miniRoutePoints[i] * s, Vector3.zero, Vector3.one * 0.0085f, mats.MiniatureAccent);
            var r = dot.GetComponent<Renderer>();
            r.enabled = false;
            miniRouteRenderers[i] = r;
        }

        // 7. LIVE PLAYER MARKER + FACING CHEVRON
        var markerRootObj = new GameObject("Player Position Marker");
        markerRootObj.transform.SetParent(miniRoot, false);
        PassageDesLanternesAssets.CreatePrimitive(markerRootObj.transform, PrimitiveType.Sphere, "MarkerCore",
            Vector3.zero, Vector3.zero, Vector3.one * 0.012f, mats.MiniatureMarker);
        PassageDesLanternesAssets.CreatePrimitive(markerRootObj.transform, PrimitiveType.Cube, "MarkerFacingCone",
            new Vector3(0f, 0.001f, 0.0075f), new Vector3(0f, 45f, 0f), new Vector3(0.007f, 0.004f, 0.007f), mats.EmissiveGold);
        var markerView = markerRootObj.AddComponent<PlayerMarkerView>();
        markerView.mapper = mapper;
        markerView.trackedPlayer = headCamera;
        markerView.miniatureLocalHeight = 0.011f;
        markerView.syncFacingYaw = true;

        // 8. LIVE MINIATURE LANTERN DOT
        var miniLanternDot = PassageDesLanternesAssets.CreatePrimitive(miniRoot, PrimitiveType.Sphere, "Miniature Lantern Dot",
            new Vector3(-3.0f * s, 0.014f, 0f), Vector3.zero, Vector3.one * 0.0085f, mats.EmissiveGold);

        miniatureLanternView = miniRoot.gameObject.AddComponent<MiniatureLanternView>();
        miniatureLanternView.mazeState = mazeState;
        miniatureLanternView.mapper = mapper;
        miniatureLanternView.trackedLantern = trackedLantern;
        miniatureLanternView.miniatureLanternDot = miniLanternDot.transform;
        miniatureLanternView.lanternLocalHeight = 0.014f;
        miniatureLanternView.miniatureRailRenderers = new[]
        {
            miniRailTrack.GetComponent<Renderer>(),
            miniRailTopBar.GetComponent<Renderer>(),
            miniRailBay5Joint.GetComponent<Renderer>(),
        };
        miniatureLanternView.miniatureSolvedRouteRenderers = miniRouteRenderers;
        miniatureLanternView.landmarks = landmarkEntries.ToArray();

        // 9. OPTIONAL ESCALATING HINT ORB ("Indice") mounted on the SW corner of the miniature rim + floating whisper console
        var hintOrbObj = new GameObject("Miniature Hint Orb");
        hintOrbObj.layer = targetLayer;
        hintOrbObj.transform.SetParent(miniRoot, false);
        hintOrbObj.transform.localPosition = new Vector3(-0.072f, 0.012f, -0.072f);
        var hintCol = hintOrbObj.AddComponent<SphereCollider>();
        hintCol.isTrigger = true;
        hintCol.radius = 0.022f;
        var hintOrbVisual = PassageDesLanternesAssets.CreatePrimitive(hintOrbObj.transform, PrimitiveType.Sphere, "HintOrbVisual",
            Vector3.zero, Vector3.zero, Vector3.one * 0.014f, mats.MiniatureAccent);
        var hintTarget = hintOrbObj.AddComponent<MiniatureWallTarget>();
        hintTarget.targetKind = MiniatureTargetKind.RequestHint;
        hintTarget.mechanismId = MazeMechanismIds.HintOrb;
        hintTarget.highlightRenderers = new[] { hintOrbVisual.GetComponent<Renderer>() };

        // Floating diegetic whisper console above the north edge of the miniature (stays hidden until triggered!)
        var consoleHolder = new GameObject("Miniature Hint Console");
        consoleHolder.transform.SetParent(miniRoot, false);
        consoleHolder.transform.localPosition = new Vector3(0f, 0.068f, 0.055f);
        var whisperPlaque = PassageDesLanternesAssets.CreateWorldPlaque(
            consoleHolder.transform, "WhisperPanel",
            Vector3.zero, Vector3.zero,
            0.22f, 0.085f,
            "Indice",
            string.Empty,
            mats, 16, 13);
        var whisperCanvas = whisperPlaque.GetComponentInChildren<Canvas>();
        if (whisperCanvas != null)
        {
            whisperCanvas.transform.localScale = Vector3.one * 0.00042f;
            var rect = whisperCanvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0.22f / 0.00042f, 0.085f / 0.00042f);
            if (whisperPlaque.titleText != null) whisperPlaque.titleText.fontSize = 30;
            if (whisperPlaque.bodyText != null) whisperPlaque.bodyText.fontSize = 24;
        }
        whisperPlaque.gameObject.SetActive(false);

        hintConsoleView = consoleHolder.AddComponent<HintConsoleView>();
        hintConsoleView.panelRoot = whisperPlaque.gameObject;
        hintConsoleView.headerText = whisperPlaque.titleText;
        hintConsoleView.messageText = whisperPlaque.bodyText;
        hintConsoleView.lookAtCamera = headCamera;

        return miniRoot;
    }

    private static GameObject CreateMiniBox(Transform parent, string name, Vector3 worldCenter, Vector3 worldSize, float scale, Material mat)
    {
        return PassageDesLanternesAssets.CreatePrimitive(parent, PrimitiveType.Cube, name,
            worldCenter * scale, Vector3.zero, worldSize * scale, mat);
    }

    private static GameObject PlaceFloorTile(Transform parent, GameObject floorPrefab, int cx, int cz, string name)
    {
        var pos = new Vector3(cx * 3f + 1.5f, 0f, cz * 3f - 1.5f);
        return InstantiatePrefab(floorPrefab, parent, pos, Quaternion.identity, name);
    }

    private static void AlignFloorSurfaceCollider(GameObject floor)
    {
        const float surfaceColliderThickness = 0.02f;
        var meshFilter = floor.GetComponent<MeshFilter>();
        var collider = floor.GetComponent<BoxCollider>();
        if (meshFilter == null || meshFilter.sharedMesh == null || collider == null)
            return;

        var localSurfaceHeight = meshFilter.sharedMesh.bounds.max.y;
        var localPos = floor.transform.localPosition;
        localPos.y = -localSurfaceHeight;
        floor.transform.localPosition = localPos;

        collider.center = new Vector3(collider.center.x, localSurfaceHeight - surfaceColliderThickness * 0.5f, collider.center.z);
        collider.size = new Vector3(collider.size.x, surfaceColliderThickness, collider.size.z);
    }

    private static void ConfigureFloorTeleport(GameObject floor, XRInteractionManager interactionManager,
        TeleportationProvider teleportationProvider, AtriumCrossingController crossingController, MazeZoneId zone)
    {
        AlignFloorSurfaceCollider(floor);
        var area = floor.AddComponent<TeleportationArea>();
        area.interactionManager = interactionManager;
        area.teleportationProvider = teleportationProvider;
        var teleportMask = InteractionLayerMask.GetMask("Teleport");
        if (teleportMask == 0)
            throw new InvalidOperationException("XRI 'Teleport' interaction layer not found.");
        area.interactionLayers = teleportMask;

        var filter = floor.AddComponent<ZoneTeleportFilter>();
        filter.crossingController = crossingController;
        filter.targetZone = zone;
    }

    private static void CreateDedicatedTeleportPad(Transform parent, string name, Vector3 localCenter, Vector3 size,
        MazeZoneId zone, XRInteractionManager interactionManager, TeleportationProvider teleportationProvider,
        AtriumCrossingController crossingController)
    {
        var pad = new GameObject(name);
        pad.transform.SetParent(parent, false);
        pad.transform.localPosition = localCenter;

        var col = pad.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, -size.y * 0.5f + 0.005f, 0f);
        col.size = size;

        var area = pad.AddComponent<TeleportationArea>();
        area.interactionManager = interactionManager;
        area.teleportationProvider = teleportationProvider;
        var teleportMask = InteractionLayerMask.GetMask("Teleport");
        if (teleportMask == 0)
            throw new InvalidOperationException("XRI 'Teleport' interaction layer not found.");
        area.interactionLayers = teleportMask;

        var filter = pad.AddComponent<ZoneTeleportFilter>();
        filter.crossingController = crossingController;
        filter.targetZone = zone;
    }

    private static GameObject PlaceHorizontalWall(Transform parent, GameObject wallPrefab, int cx, float zEdge, string name)
    {
        return InstantiatePrefab(wallPrefab, parent, new Vector3(cx * 3f + 1.5f, 0f, zEdge), Quaternion.identity, name);
    }

    private static GameObject PlaceVerticalWall(Transform parent, GameObject wallPrefab, float xEdge, int cz, string name)
    {
        return InstantiatePrefab(wallPrefab, parent, new Vector3(xEdge, 0f, cz * 3f - 1.5f), Quaternion.Euler(0f, 90f, 0f), name);
    }

    private static GameObject InstantiatePrefab(GameObject prefab, Transform parent, Vector3 localPos, Quaternion localRot, string name)
    {
        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            throw new InvalidOperationException($"Could not instantiate prefab '{prefab.name}'.");

        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = localPos;
        instance.transform.localRotation = localRot;
        instance.transform.localScale = Vector3.one;
        return instance;
    }

    private static int EnsureLayer(string targetLayerName)
    {
        var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        var serialized = new SerializedObject(tagManager);
        var layers = serialized.FindProperty("layers");

        for (var i = 8; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == targetLayerName)
                return i;
        }

        for (var i = 8; i < layers.arraySize; i++)
        {
            var layer = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(layer.stringValue))
                continue;

            layer.stringValue = targetLayerName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return i;
        }

        throw new InvalidOperationException($"No unused user layer available for '{targetLayerName}'.");
    }
}

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

public static class BuildCorridorPrototype
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string DefinitionPath = "Assets/Data/Mazes/PrototypeMazeDefinition.asset";
    private const string MiniatureMaterialPath = "Assets/Art/MiniatureWhite.mat";
    private const string MarkerMaterialPath = "Assets/Art/MiniatureMarker.mat";
    private const string PointerMaterialPath = "Assets/Art/MagicPointer.mat";
    private const string NightSkyboxMaterialPath = "Assets/Art/NightSkybox.mat";
    private const string NightSkyboxTexturePath = "Assets/Art/NightSkyboxCube.png";
    private const string RigPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.7.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    private const string FloorPrefabPath = "Assets/Maze/Prefabs/Floor_3M.prefab";
    private const string WallPrefabPath = "Assets/Maze/Prefabs/Wall_3M.prefab";
    private const string WallLampPrefabPath = "Assets/Maze/Prefabs/Wall_Lamp.prefab";
    private const string MechanismId = "center_gate";
    private const string PrototypeRootName = "Pocket Labyrinthe Prototype";

    [MenuItem("Pocket Labyrinthe/Generate Corridor Prototype Scene")]
    public static void BuildScene()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.path != ScenePath)
            activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EnsureFolder("Assets/Data");
        EnsureFolder("Assets/Data/Mazes");
        EnsureFolder("Assets/Art");

        var existingRoot = GameObject.Find(PrototypeRootName);
        if (existingRoot != null)
            Object.DestroyImmediate(existingRoot);

        var oldMainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        if (oldMainCamera != null)
            Object.DestroyImmediate(oldMainCamera);

        ConfigureNightEnvironment();

        var root = new GameObject(PrototypeRootName);
        var definition = LoadOrCreateDefinition();
        var interactionManager = new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
        interactionManager.transform.SetParent(root.transform, false);

        var stateObject = new GameObject("Maze State");
        stateObject.transform.SetParent(root.transform, false);
        var mazeState = stateObject.AddComponent<MazeState>();
        mazeState.Definition = definition;

        var mazeRoot = new GameObject("World Maze Mapping Root").transform;
        mazeRoot.SetParent(root.transform, false);
        BuildWorldCorridor(mazeRoot, mazeState, interactionManager, definition);
        var teleportationProvider = BuildPlayerAndMiniature(root.transform, mazeRoot, mazeState, definition, interactionManager);
        foreach (var teleportationArea in mazeRoot.GetComponentsInChildren<TeleportationArea>(true))
            teleportationArea.teleportationProvider = teleportationProvider;

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Selection.activeGameObject = root;
        Debug.Log("Generated the Pocket Labyrinthe corridor prototype in SampleScene. No player build was started.", root);
    }

    private static void BuildWorldCorridor(Transform mazeRoot, MazeState mazeState, XRInteractionManager interactionManager,
        MazeDefinition definition)
    {
        var floorPrefab = LoadPrefab(FloorPrefabPath);
        var wallPrefab = LoadPrefab(WallPrefabPath);
        var lampPrefab = LoadPrefab(WallLampPrefabPath);

        var entryFloor = InstantiateAsset(floorPrefab, mazeRoot, new Vector3(3f, 0f, -3f), Quaternion.identity, "Entry Floor");
        ConfigureFloorTeleportation(entryFloor, interactionManager);
        var exitFloor = InstantiateAsset(floorPrefab, mazeRoot, new Vector3(3f, 0f, 0f), Quaternion.identity, "Exit Floor");
        ConfigureFloorTeleportation(exitFloor, interactionManager);

        for (var segment = 0; segment < 2; segment++)
        {
            var z = segment * 3f - 3f;
            InstantiateAsset(wallPrefab, mazeRoot, new Vector3(0f, 0f, z), Quaternion.Euler(0f, 90f, 0f), $"Left Corridor Wall {segment + 1}");
            InstantiateAsset(wallPrefab, mazeRoot, new Vector3(3f, 0f, z), Quaternion.Euler(0f, 90f, 0f), $"Right Corridor Wall {segment + 1}");
        }

        var gate = InstantiateAsset(wallPrefab, mazeRoot, new Vector3(3f, 0f, 0f), Quaternion.identity, "Center Gate");
        definition.TryGetMechanism(MechanismId, out var mechanism);
        var gateView = gate.AddComponent<WallMechanismView>();
        gateView.mazeState = mazeState;
        gateView.mechanismId = MechanismId;
        gateView.raisedLocalPosition = gate.transform.localPosition;
        gateView.loweredLocalOffset = Vector3.down * (mechanism != null ? mechanism.lowerDistance : 5f);
        gateView.animationSeconds = mechanism != null ? mechanism.animationSeconds : 0.45f;

        AddWarmLampLight(InstantiateAsset(lampPrefab, mazeRoot, new Vector3(0.25f, 2.2f, -1.5f), Quaternion.Euler(0f, 90f, 0f), "Entry Lamp Left"), "Entry Lamp Left Warm Light");
        AddWarmLampLight(InstantiateAsset(lampPrefab, mazeRoot, new Vector3(2.75f, 2.2f, -1.5f), Quaternion.Euler(0f, -90f, 0f), "Entry Lamp Right"), "Entry Lamp Right Warm Light");
        AddWarmLampLight(InstantiateAsset(lampPrefab, mazeRoot, new Vector3(0.25f, 2.2f, 1.5f), Quaternion.Euler(0f, 90f, 0f), "Exit Lamp Left"), "Exit Lamp Left Warm Light");
        AddWarmLampLight(InstantiateAsset(lampPrefab, mazeRoot, new Vector3(2.75f, 2.2f, 1.5f), Quaternion.Euler(0f, -90f, 0f), "Exit Lamp Right"), "Exit Lamp Right Warm Light");

    }

    private static void ConfigureFloorTeleportation(GameObject floor, XRInteractionManager interactionManager)
    {
        const float surfaceColliderThickness = 0.02f;
        var meshFilter = floor.GetComponent<MeshFilter>();
        var collider = floor.GetComponent<BoxCollider>();
        if (meshFilter == null || meshFilter.sharedMesh == null || collider == null)
            throw new System.InvalidOperationException($"Floor prefab '{floor.name}' needs a mesh and BoxCollider.");

        var localSurfaceHeight = meshFilter.sharedMesh.bounds.max.y;
        var localPosition = floor.transform.localPosition;
        localPosition.y -= localSurfaceHeight;
        floor.transform.localPosition = localPosition;

        collider.center = new Vector3(collider.center.x, localSurfaceHeight - surfaceColliderThickness * 0.5f, collider.center.z);
        collider.size = new Vector3(collider.size.x, surfaceColliderThickness, collider.size.z);

        var teleportationArea = floor.AddComponent<TeleportationArea>();
        teleportationArea.interactionManager = interactionManager;
        var teleportMask = InteractionLayerMask.GetMask("Teleport");
        if (teleportMask == 0)
            throw new System.InvalidOperationException("The XRI 'Teleport' interaction layer could not be found.");
        teleportationArea.interactionLayers = teleportMask;
    }

    private static void ConfigureNightEnvironment()
    {
        var shader = Shader.Find("Skybox/Cubemap");
        var cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(NightSkyboxTexturePath);
        if (shader == null || cubemap == null)
            throw new System.InvalidOperationException("The night skybox shader or cubemap asset is missing.");

        var material = AssetDatabase.LoadAssetAtPath<Material>(NightSkyboxMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "NightSkybox" };
            AssetDatabase.CreateAsset(material, NightSkyboxMaterialPath);
        }

        material.shader = shader;
        material.SetTexture("_Tex", cubemap);
        material.SetColor("_Tint", new Color(0.78f, 0.82f, 1f, 1f));
        material.SetFloat("_Exposure", 0.8f);
        material.SetFloat("_Rotation", 0f);
        EditorUtility.SetDirty(material);

        RenderSettings.skybox = material;
        RenderSettings.ambientIntensity = 0.55f;
        RenderSettings.sun = null;
        foreach (var sceneLight in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (sceneLight.type == LightType.Directional)
                sceneLight.enabled = false;
        }

        DynamicGI.UpdateEnvironment();
    }

    private static void AddWarmLampLight(GameObject lamp, string lightName)
    {
        var lightObject = new GameObject(lightName);
        lightObject.transform.SetParent(lamp.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.15f, 0f);

        var lampLight = lightObject.AddComponent<Light>();
        lampLight.type = LightType.Point;
        lampLight.color = new Color(1f, 0.34f, 0.12f, 1f);
        lampLight.intensity = 1.8f;
        lampLight.range = 4.5f;
        lampLight.shadows = LightShadows.None;
        lampLight.bounceIntensity = 0f;
    }

    private static TeleportationProvider BuildPlayerAndMiniature(Transform root, Transform mazeRoot, MazeState mazeState,
        MazeDefinition definition, XRInteractionManager interactionManager)
    {
        definition.TryGetMechanism(MechanismId, out var mechanism);
        var rigPrefab = LoadPrefab(RigPrefabPath);
        var rig = PrefabUtility.InstantiatePrefab(rigPrefab) as GameObject;
        if (rig == null)
            throw new System.InvalidOperationException($"Could not instantiate XR Origin prefab at {RigPrefabPath}.");

        rig.name = "Quest Player Rig";
        rig.transform.SetParent(root, false);
        rig.transform.localPosition = new Vector3(1.5f, 0f, -1.5f);
        rig.transform.localRotation = Quaternion.identity;
        rig.SetActive(true);
        var teleportationProvider = rig.GetComponentInChildren<TeleportationProvider>(true);
        if (teleportationProvider == null)
            throw new System.InvalidOperationException("The XR Origin prefab has no TeleportationProvider.");

        var xrOrigin = rig.GetComponent<XROrigin>();
        if (xrOrigin != null)
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
        ConfigureComfortableLocomotion(rig);

        var headCamera = rig.GetComponentInChildren<Camera>(true);
        if (headCamera == null)
            throw new System.InvalidOperationException("The XR Origin prefab has no camera.");

        var miniRootObject = new GameObject("Left Hand Miniature");
        var miniRoot = miniRootObject.transform;
        miniRoot.SetParent(root, false);
        miniRoot.localPosition = new Vector3(1.5f, 1.2f, -1.45f);
        miniRoot.localScale = Vector3.one;

        var whiteMaterial = LoadOrCreateMaterial(MiniatureMaterialPath, Color.white, "Universal Render Pipeline/Lit");
        var markerMaterial = LoadOrCreateMaterial(MarkerMaterialPath, new Color(1f, 0.28f, 0.12f, 1f));
        var pointerMaterial = LoadOrCreateMaterial(PointerMaterialPath, new Color(0.25f, 0.8f, 1f, 1f));

        CreateCube(miniRoot, "Miniature Base", new Vector3(0.015f, -0.003f, 0f), new Vector3(0.03f, 0.006f, 0.06f), whiteMaterial);
        CreateCube(miniRoot, "Miniature Left Wall Entry", new Vector3(-0.00015f, 0.02f, -0.015f), new Vector3(0.0037f, 0.04f, 0.03f), whiteMaterial);
        CreateCube(miniRoot, "Miniature Left Wall Exit", new Vector3(-0.00015f, 0.02f, 0.015f), new Vector3(0.0037f, 0.04f, 0.03f), whiteMaterial);
        CreateCube(miniRoot, "Miniature Right Wall Entry", new Vector3(0.02985f, 0.02f, -0.015f), new Vector3(0.0037f, 0.04f, 0.03f), whiteMaterial);
        CreateCube(miniRoot, "Miniature Right Wall Exit", new Vector3(0.02985f, 0.02f, 0.015f), new Vector3(0.0037f, 0.04f, 0.03f), whiteMaterial);

        var targetLayer = EnsureMiniatureLayer();
        var targetObject = new GameObject("Center Gate Miniature Target");
        targetObject.layer = targetLayer;
        targetObject.transform.SetParent(miniRoot, false);
        targetObject.transform.localPosition = new Vector3(0.015f, 0.02f, 0f);
        var targetCollider = targetObject.AddComponent<BoxCollider>();
        targetCollider.isTrigger = true;
        targetCollider.size = new Vector3(0.08f, 0.1f, 0.08f);

        var miniatureGate = CreateCube(targetObject.transform, "Center Gate Miniature Visual", Vector3.zero,
            new Vector3(0.03f, 0.04f, 0.0037f), whiteMaterial);
        var miniatureTarget = targetObject.AddComponent<MiniatureWallTarget>();
        miniatureTarget.mechanismId = MechanismId;
        var gatePad = CreateCube(targetObject.transform, "Center Gate Miniature Location Marker", new Vector3(0f, -0.017f, 0f),
            new Vector3(0.036f, 0.002f, 0.012f), whiteMaterial);
        miniatureTarget.highlightRenderers = new[]
        {
            miniatureGate.GetComponent<MeshRenderer>(),
            gatePad.GetComponent<MeshRenderer>(),
        };

        var miniatureView = targetObject.AddComponent<MiniatureMechanismView>();
        miniatureView.mazeState = mazeState;
        miniatureView.mechanismId = MechanismId;
        miniatureView.visual = miniatureGate.transform;
        miniatureView.raisedLocalPosition = miniatureGate.transform.localPosition;
        miniatureView.loweredLocalOffset = Vector3.down *
            (mechanism != null ? mechanism.lowerDistance * definition.metresToMiniatureScale : 0.05f);
        miniatureView.animationSeconds = mechanism != null ? mechanism.animationSeconds * 0.5f : 0.2f;

        var mapperObject = new GameObject("Maze Scale Mapper");
        mapperObject.transform.SetParent(root, false);
        var mapper = mapperObject.AddComponent<MazeScaleMapper>();
        mapper.worldMazeRoot = mazeRoot;
        mapper.miniatureRoot = miniRoot;
        mapper.metresToMiniatureScale = definition.metresToMiniatureScale;

        var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "Player Position Marker";
        marker.transform.SetParent(miniRoot, false);
        marker.transform.localScale = Vector3.one * 0.012f;
        Object.DestroyImmediate(marker.GetComponent<Collider>());
        marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
        var markerView = marker.AddComponent<PlayerMarkerView>();
        markerView.mapper = mapper;
        markerView.trackedPlayer = headCamera.transform;
        markerView.miniatureLocalHeight = 0.008f;

        var interactionObject = new GameObject("Controller Miniature Interaction");
        interactionObject.transform.SetParent(root, false);
        var interaction = interactionObject.AddComponent<XRControllerMiniatureInteraction>();
        interaction.trackingOrigin = rig.transform;
        interaction.miniatureRoot = miniRoot;
        interaction.mazeState = mazeState;
        interaction.targetLayers = 1 << targetLayer;
        interaction.rayLength = 1.5f;
        interaction.leftHandMiniatureLocalPosition = new Vector3(0.025f, 0.012f, 0.01f);
        interaction.leftHandMiniatureLocalEulerOffset = new Vector3(0f, 0f, -90f);
        interaction.pointerCurveSag = 0.2f;

        var pointer = interactionObject.AddComponent<LineRenderer>();
        pointer.enabled = false;
        pointer.useWorldSpace = true;
        pointer.positionCount = 17;
        pointer.sharedMaterial = pointerMaterial;
        pointer.startColor = new Color(0.25f, 0.8f, 1f, 0.9f);
        pointer.endColor = new Color(0.25f, 0.8f, 1f, 0.25f);
        pointer.startWidth = 0.003f;
        pointer.endWidth = 0.001f;
        pointer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pointer.receiveShadows = false;
        interaction.pointerLine = pointer;

        foreach (var interactable in rig.GetComponentsInChildren<XRBaseInteractable>(true))
            interactable.interactionManager = interactionManager;
        foreach (var interactor in rig.GetComponentsInChildren<XRBaseInteractor>(true))
            interactor.interactionManager = interactionManager;

        return teleportationProvider;
    }

    private static void ConfigureComfortableLocomotion(GameObject rig)
    {
        var controllerSettingsFound = false;
        foreach (var behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
                continue;

            var typeName = behaviour.GetType().Name;
            if (typeName.Contains("ContinuousMoveProvider") || typeName.Contains("ContinuousTurnProvider"))
            {
                behaviour.enabled = false;
                continue;
            }

            if (typeName != "ControllerInputActionManager" && typeName != "ActionBasedControllerManager")
                continue;

            controllerSettingsFound = true;
            var serialized = new SerializedObject(behaviour);
            var smoothMotion = serialized.FindProperty("m_SmoothMotionEnabled");
            if (smoothMotion != null)
            {
                smoothMotion.boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        if (!controllerSettingsFound)
            Debug.LogWarning("The XR Origin prefab has no controller input manager; confirm smooth movement stays disabled.", rig);
    }

    private static GameObject CreateCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = localScale;
        Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        return cube;
    }

    private static GameObject InstantiateAsset(GameObject prefab, Transform parent, Vector3 localPosition, Quaternion localRotation, string name)
    {
        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            throw new System.InvalidOperationException($"Could not instantiate prefab '{prefab.name}'.");

        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = localRotation;
        instance.transform.localScale = Vector3.one;
        return instance;
    }

    private static GameObject LoadPrefab(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            throw new FileNotFoundException($"Required imported maze prefab was not found: {path}");
        return prefab;
    }

    private static MazeDefinition LoadOrCreateDefinition()
    {
        var definition = AssetDatabase.LoadAssetAtPath<MazeDefinition>(DefinitionPath);
        if (definition != null)
            return definition;

        definition = ScriptableObject.CreateInstance<MazeDefinition>();
        definition.metresToMiniatureScale = 0.01f;
        definition.mechanisms = new[]
        {
            new MazeMechanismDefinition
            {
                id = MechanismId,
                startsLowered = false,
                lowerDistance = 5f,
                animationSeconds = 0.45f,
            },
        };
        AssetDatabase.CreateAsset(definition, DefinitionPath);
        return definition;
    }

    private static Material LoadOrCreateMaterial(string path, Color color, string shaderName = "Universal Render Pipeline/Unlit")
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find(shaderName);
        if (shader == null)
            throw new System.InvalidOperationException($"Required shader '{shaderName}' could not be found for the prototype.");

        if (material == null)
        {
            material = new Material(shader);
            material.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.SetColor("_BaseColor", color);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static int EnsureMiniatureLayer()
    {
        var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        var serialized = new SerializedObject(tagManager);
        var layers = serialized.FindProperty("layers");

        for (var i = 8; i < layers.arraySize; i++)
        {
            var layerName = layers.GetArrayElementAtIndex(i).stringValue;
            if (layerName == "MiniatureTarget")
                return i;
        }

        for (var i = 8; i < layers.arraySize; i++)
        {
            var layer = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(layer.stringValue))
                continue;

            layer.stringValue = "MiniatureTarget";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return i;
        }

        throw new System.InvalidOperationException("No unused user layer is available for the miniature target.");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        var folderName = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}

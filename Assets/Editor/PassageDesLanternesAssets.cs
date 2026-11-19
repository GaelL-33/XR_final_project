using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public sealed class PassageMaterialSet
{
    public Material StoneDark;
    public Material StoneTrim;
    public Material BronzeMetal;
    public Material EmissiveGold;
    public Material EmissiveTeal;
    public Material EmissiveDormant;
    public Material AbyssChasm;
    public Material MiniatureFloor;
    public Material MiniatureWall;
    public Material MiniatureMarker;
    public Material MiniatureAccent;
    public Material MagicPointer;
    public Material NightSkybox;
}

public sealed class PassagePrefabSet
{
    public GameObject Floor3M;
    public GameObject Wall3M;
    public GameObject WallLamp;
    public GameObject QuestRig;
    public GameObject LanternNiche;
    public GameObject PortableLantern;
    public GameObject WorldButton;
    public GameObject ExitLever;
    public GameObject RouteBeacon;
}

public static class PassageDesLanternesAssets
{
    public const string ScenePath = "Assets/Scenes/SampleScene.unity";
    public const string DefinitionPath = "Assets/Data/Mazes/PassageDesLanternesDefinition.asset";

    public const string RigPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.7.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    public const string FloorPrefabPath = "Assets/Maze/Prefabs/Floor_3M.prefab";
    public const string WallPrefabPath = "Assets/Maze/Prefabs/Wall_3M.prefab";
    public const string WallLampPrefabPath = "Assets/Maze/Prefabs/Wall_Lamp.prefab";
    public const string NightSkyboxTexturePath = "Assets/Art/NightSkyboxCube.png";

    public const string LanternNichePrefabPath = "Assets/Prefabs/LanternNiche.prefab";
    public const string PortableLanternPrefabPath = "Assets/Prefabs/PortableLantern.prefab";
    public const string WorldButtonPrefabPath = "Assets/Prefabs/WorldButton.prefab";
    public const string ExitLeverPrefabPath = "Assets/Prefabs/ExitLever.prefab";
    public const string RouteBeaconPrefabPath = "Assets/Prefabs/RouteBeacon.prefab";

    public static void EnsureFolders()
    {
        EnsureFolder("Assets/Data");
        EnsureFolder("Assets/Data/Mazes");
        EnsureFolder("Assets/Art");
        EnsureFolder("Assets/Prefabs");
    }

    public static PassageMaterialSet LoadOrCreateMaterials()
    {
        EnsureFolders();
        return new PassageMaterialSet
        {
            StoneDark = LoadOrCreateLitMaterial("Assets/Art/StoneDark.mat", new Color(0.16f, 0.18f, 0.23f, 1f), 0f, 0.16f, false, Color.black),
            StoneTrim = LoadOrCreateLitMaterial("Assets/Art/StoneTrim.mat", new Color(0.45f, 0.41f, 0.37f, 1f), 0.05f, 0.22f, false, Color.black),
            BronzeMetal = LoadOrCreateLitMaterial("Assets/Art/BronzeMetal.mat", new Color(0.62f, 0.46f, 0.24f, 1f), 0.72f, 0.52f, false, Color.black),
            EmissiveGold = LoadOrCreateLitMaterial("Assets/Art/EmissiveGold.mat", new Color(1f, 0.84f, 0.42f, 1f), 0.1f, 0.35f, true, new Color(1f, 0.68f, 0.22f, 1f) * 1.4f),
            EmissiveTeal = LoadOrCreateLitMaterial("Assets/Art/EmissiveTeal.mat", new Color(0.32f, 0.88f, 0.92f, 1f), 0.1f, 0.35f, true, new Color(0.18f, 0.72f, 0.82f, 1f) * 1.15f),
            EmissiveDormant = LoadOrCreateLitMaterial("Assets/Art/EmissiveDormant.mat", new Color(0.24f, 0.22f, 0.23f, 1f), 0.25f, 0.35f, true, Color.black),
            AbyssChasm = LoadOrCreateLitMaterial("Assets/Art/AbyssChasm.mat", new Color(0.05f, 0.07f, 0.14f, 1f), 0f, 0.08f, false, Color.black),
            MiniatureFloor = LoadOrCreateLitMaterial("Assets/Art/MiniatureFloor.mat", new Color(0.88f, 0.86f, 0.81f, 1f), 0f, 0.15f, false, Color.black),
            MiniatureWall = LoadOrCreateLitMaterial("Assets/Art/MiniatureWhite.mat", Color.white, 0f, 0.15f, false, Color.black),
            MiniatureMarker = LoadOrCreateUnlitMaterial("Assets/Art/MiniatureMarker.mat", new Color(1f, 0.36f, 0.14f, 1f)),
            MiniatureAccent = LoadOrCreateLitMaterial("Assets/Art/MiniatureAccent.mat", new Color(0.92f, 0.8f, 0.46f, 1f), 0.2f, 0.35f, true, Color.black),
            MagicPointer = LoadOrCreateUnlitMaterial("Assets/Art/MagicPointer.mat", new Color(0.25f, 0.82f, 1f, 1f)),
            NightSkybox = LoadOrCreateNightSkybox(),
        };
    }

    public static PassagePrefabSet LoadOrCreatePrefabs(PassageMaterialSet mats)
    {
        EnsureFolders();
        return new PassagePrefabSet
        {
            Floor3M = LoadRequiredPrefab(FloorPrefabPath),
            Wall3M = LoadRequiredPrefab(WallPrefabPath),
            WallLamp = LoadRequiredPrefab(WallLampPrefabPath),
            QuestRig = LoadRequiredPrefab(RigPrefabPath),
            LanternNiche = CreateOrUpdateLanternNichePrefab(mats),
            PortableLantern = CreateOrUpdatePortableLanternPrefab(mats),
            WorldButton = CreateOrUpdateWorldButtonPrefab(mats),
            ExitLever = CreateOrUpdateExitLeverPrefab(mats),
            RouteBeacon = CreateOrUpdateRouteBeaconPrefab(mats),
        };
    }

    public static MazeDefinition LoadOrCreateDefinition()
    {
        EnsureFolders();
        var definition = AssetDatabase.LoadAssetAtPath<MazeDefinition>(DefinitionPath);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<MazeDefinition>();
            AssetDatabase.CreateAsset(definition, DefinitionPath);
        }

        definition.metresToMiniatureScale = 0.01f;
        definition.spawnWorldPosition = new Vector3(-3f, 0f, -6f);
        definition.spawnYawDegrees = 0f;
        definition.lanternPedestalWorldPosition = new Vector3(-3f, 0.98f, 0f);
        definition.mazeBoundsMin = new Vector3(-7.75f, -0.45f, -7.75f);
        definition.mazeBoundsMax = new Vector3(7.75f, 5.0f, 7.75f);

        definition.mechanisms = new[]
        {
            new MazeMechanismDefinition
            {
                id = MazeMechanismIds.IntroGate,
                startsLowered = false,
                allowDirectMiniatureToggle = true,
                lowerDistance = 4.6f,
                animationSeconds = 0.7f,
            },
            new MazeMechanismDefinition
            {
                id = MazeMechanismIds.AtriumLock,
                startsLowered = false,
                allowDirectMiniatureToggle = false,
                lowerDistance = 4.6f,
                animationSeconds = 0.9f,
            },
            new MazeMechanismDefinition
            {
                id = MazeMechanismIds.ExitGate,
                startsLowered = false,
                allowDirectMiniatureToggle = false,
                lowerDistance = 4.6f,
                animationSeconds = 1.0f,
            },
        };

        definition.lanternBays = new[]
        {
            new LanternBayDefinition
            {
                id = "bay_1",
                numeral = "I",
                displayNameFr = "Croissant",
                emblemKind = BayEmblemKind.Crescent,
                emblemColor = new Color(0.84f, 0.75f, 0.56f, 1f),
                backsOntoAtriumRail = false,
                nicheCount = 6,
                correctNicheIndex = -1,
                worldCenter = new Vector3(-6.0f, 0f, -1.29f),
                inwardNormal = Vector3.forward,
            },
            new LanternBayDefinition
            {
                id = "bay_2",
                numeral = "II",
                displayNameFr = "Pilier",
                emblemKind = BayEmblemKind.TwinPillar,
                emblemColor = new Color(0.68f, 0.76f, 0.84f, 1f),
                backsOntoAtriumRail = false,
                nicheCount = 6,
                correctNicheIndex = -1,
                worldCenter = new Vector3(-4.71f, 0f, 3.0f),
                inwardNormal = Vector3.left,
            },
            new LanternBayDefinition
            {
                id = "bay_3",
                numeral = "III",
                displayNameFr = "Étoile",
                emblemKind = BayEmblemKind.Star,
                emblemColor = new Color(0.92f, 0.78f, 0.48f, 1f),
                backsOntoAtriumRail = false,
                nicheCount = 6,
                correctNicheIndex = -1,
                worldCenter = new Vector3(-6.0f, 0f, 7.29f),
                inwardNormal = Vector3.back,
            },
            new LanternBayDefinition
            {
                id = "bay_4",
                numeral = "IV",
                displayNameFr = "Sablier",
                emblemKind = BayEmblemKind.Hourglass,
                emblemColor = new Color(0.74f, 0.66f, 0.82f, 1f),
                backsOntoAtriumRail = false,
                nicheCount = 6,
                correctNicheIndex = -1,
                worldCenter = new Vector3(0.0f, 0f, 4.71f),
                inwardNormal = Vector3.forward,
            },
            new LanternBayDefinition
            {
                id = "bay_5",
                numeral = "V",
                displayNameFr = "Arche",
                emblemKind = BayEmblemKind.Arch,
                emblemColor = new Color(0.96f, 0.74f, 0.36f, 1f),
                backsOntoAtriumRail = true,
                nicheCount = 6,
                correctNicheIndex = 5,
                worldCenter = new Vector3(3.0f, 0f, 4.71f),
                inwardNormal = Vector3.forward,
            },
            new LanternBayDefinition
            {
                id = "bay_6",
                numeral = "VI",
                displayNameFr = "Cadran",
                emblemKind = BayEmblemKind.Compass,
                emblemColor = new Color(0.62f, 0.82f, 0.74f, 1f),
                backsOntoAtriumRail = false,
                nicheCount = 6,
                correctNicheIndex = -1,
                worldCenter = new Vector3(6.0f, 0f, 4.71f),
                inwardNormal = Vector3.forward,
            },
        };

        definition.atriumPartition = new AtriumPartitionDefinition
        {
            id = MazeMechanismIds.AtriumPartition,
            startsLocked = true,
            initialPosition = AtriumPartitionPosition.IslandToExit,
            railWorldX = 2.84f,
            entryPositionWorldZ = 1.1f,
            exitPositionWorldZ = -1.1f,
            slideSeconds = 0.78f,
            travelSafetyMin = new Vector3(2.42f, -0.5f, -2.25f),
            travelSafetyMax = new Vector3(3.58f, 4.0f, 2.25f),
        };

        definition.hints = new[]
        {
            new HintStageDefinition
            {
                stage = ProgressionStage.IntroGateClosed,
                highlightLandmarkId = MazeMechanismIds.IntroGate,
                tier1Fr = "Observez le losange gravé devant vous, puis retrouvez-le sur la maquette.",
                tier2Fr = "Pointez la porte miniature avec votre main droite.",
                tier3Fr = "Visez la porte sur la maquette et appuyez sur B pour l'abaisser.",
            },
            new HintStageDefinition
            {
                stage = ProgressionStage.FindLantern,
                highlightLandmarkId = "lantern_pedestal",
                tier1Fr = "Une lanterne vous attend sur son socle dans le vestibule.",
                tier2Fr = "Prenez la lanterne avant d'explorer la galerie des six baies.",
                tier3Fr = "Approchez la main droite de la lanterne et pressez la poignée (Grip).",
            },
            new HintStageDefinition
            {
                stage = ProgressionStage.DeduceLanternNiche,
                highlightLandmarkId = "bay_5",
                tier1Fr = "Sur la maquette, suivez le rail de l'atrium : contre quelle baie s'appuie-t-il ?",
                tier2Fr = "Le rail aboutit derrière la baie V (Arche), au nord-est de la galerie.",
                tier3Fr = "Approchez la lanterne de la baie V : la veine dorée désigne la niche en haut à droite.",
            },
            new HintStageDefinition
            {
                stage = ProgressionStage.ReachAtriumIsland,
                highlightLandmarkId = MazeMechanismIds.AtriumPartition,
                tier1Fr = "Le verrou est levé et une poignée est apparue sur la cloison de la maquette.",
                tier2Fr = "Ouvrez le passage nord pour rejoindre l'îlot central.",
                tier3Fr = "Glissez la poignée vers le nord (ou visez-la + B), puis téléportez-vous sur l'îlot.",
            },
            new HintStageDefinition
            {
                stage = ProgressionStage.CrossToAtriumFarSide,
                highlightLandmarkId = MazeMechanismIds.AtriumPartition,
                tier1Fr = "L'îlot reste sûr dans les deux positions : vérifiez votre marqueur.",
                tier2Fr = "Faites maintenant glisser la cloison côté sud pour ouvrir l'autre rive.",
                tier3Fr = "Glissez la poignée vers le sud (ou visez-la + B), puis traversez vers le levier.",
            },
            new HintStageDefinition
            {
                stage = ProgressionStage.PullExitLever,
                highlightLandmarkId = "exit_lever",
                tier1Fr = "Un levier grandeur nature se dresse devant l'arche sud.",
                tier2Fr = "Abaissez-le pour déverrouiller la Porte de l'Aube aperçue au départ.",
                tier3Fr = "Saisissez ou activez le levier, puis franchissez l'arche dorée.",
            },
            new HintStageDefinition
            {
                stage = ProgressionStage.EnterExitSanctuary,
                highlightLandmarkId = MazeMechanismIds.ExitGate,
                tier1Fr = "La Porte de l'Aube s'est abaissée devant vous.",
                tier2Fr = "Suivez la traînée lumineuse jusqu'au sanctuaire de sortie.",
                tier3Fr = "Téléportez-vous dans le sanctuaire doré pour terminer la traversée.",
            },
        };

        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssets();
        return definition;
    }

    public static GameObject CreatePrimitive(Transform parent, PrimitiveType primitiveType, string name,
        Vector3 localPosition, Vector3 localEuler, Vector3 localScale, Material material, bool removeCollider = true)
    {
        var obj = GameObject.CreatePrimitive(primitiveType);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        obj.transform.localRotation = Quaternion.Euler(localEuler);
        obj.transform.localScale = localScale;
        if (removeCollider)
        {
            var col = obj.GetComponent<Collider>();
            if (col != null)
                Object.DestroyImmediate(col);
        }

        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        return obj;
    }

    public static Renderer[] BuildBayEmblemGeometry(Transform parent, BayEmblemKind kind, Material material, float scale)
    {
        var root = new GameObject($"Emblem_{kind}").transform;
        root.SetParent(parent, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one * scale;

        switch (kind)
        {
            case BayEmblemKind.Crescent:
            {
                var a = CreatePrimitive(root, PrimitiveType.Cube, "CrescentLeft", new Vector3(-0.08f, 0f, 0f), new Vector3(0f, 0f, 22f), new Vector3(0.06f, 0.24f, 0.04f), material);
                var b = CreatePrimitive(root, PrimitiveType.Cube, "CrescentTop", new Vector3(0.03f, 0.11f, 0f), new Vector3(0f, 0f, -35f), new Vector3(0.06f, 0.16f, 0.04f), material);
                var c = CreatePrimitive(root, PrimitiveType.Cube, "CrescentBottom", new Vector3(0.03f, -0.11f, 0f), new Vector3(0f, 0f, 35f), new Vector3(0.06f, 0.16f, 0.04f), material);
                return new[] { a.GetComponent<Renderer>(), b.GetComponent<Renderer>(), c.GetComponent<Renderer>() };
            }

            case BayEmblemKind.TwinPillar:
            {
                var a = CreatePrimitive(root, PrimitiveType.Cube, "PillarL", new Vector3(-0.09f, 0f, 0f), Vector3.zero, new Vector3(0.07f, 0.28f, 0.045f), material);
                var b = CreatePrimitive(root, PrimitiveType.Cube, "PillarR", new Vector3(0.09f, 0f, 0f), Vector3.zero, new Vector3(0.07f, 0.28f, 0.045f), material);
                var c = CreatePrimitive(root, PrimitiveType.Cube, "Lintel", new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(0.30f, 0.055f, 0.05f), material);
                return new[] { a.GetComponent<Renderer>(), b.GetComponent<Renderer>(), c.GetComponent<Renderer>() };
            }

            case BayEmblemKind.Star:
            {
                var a = CreatePrimitive(root, PrimitiveType.Cube, "StarA", Vector3.zero, new Vector3(0f, 0f, 45f), new Vector3(0.22f, 0.22f, 0.045f), material);
                var b = CreatePrimitive(root, PrimitiveType.Cube, "StarB", Vector3.zero, Vector3.zero, new Vector3(0.22f, 0.22f, 0.045f), material);
                return new[] { a.GetComponent<Renderer>(), b.GetComponent<Renderer>() };
            }

            case BayEmblemKind.Hourglass:
            {
                var top = CreatePrimitive(root, PrimitiveType.Cube, "TopBar", new Vector3(0f, 0.13f, 0f), Vector3.zero, new Vector3(0.26f, 0.05f, 0.045f), material);
                var bot = CreatePrimitive(root, PrimitiveType.Cube, "BotBar", new Vector3(0f, -0.13f, 0f), Vector3.zero, new Vector3(0.26f, 0.05f, 0.045f), material);
                var d1 = CreatePrimitive(root, PrimitiveType.Cube, "DiagA", Vector3.zero, new Vector3(0f, 0f, 36f), new Vector3(0.055f, 0.30f, 0.045f), material);
                var d2 = CreatePrimitive(root, PrimitiveType.Cube, "DiagB", Vector3.zero, new Vector3(0f, 0f, -36f), new Vector3(0.055f, 0.30f, 0.045f), material);
                return new[] { top.GetComponent<Renderer>(), bot.GetComponent<Renderer>(), d1.GetComponent<Renderer>(), d2.GetComponent<Renderer>() };
            }

            case BayEmblemKind.Arch:
            {
                var l = CreatePrimitive(root, PrimitiveType.Cube, "ArchL", new Vector3(-0.12f, -0.04f, 0f), Vector3.zero, new Vector3(0.065f, 0.24f, 0.05f), material);
                var r = CreatePrimitive(root, PrimitiveType.Cube, "ArchR", new Vector3(0.12f, -0.04f, 0f), Vector3.zero, new Vector3(0.065f, 0.24f, 0.05f), material);
                var tl = CreatePrimitive(root, PrimitiveType.Cube, "ArchTopL", new Vector3(-0.065f, 0.10f, 0f), new Vector3(0f, 0f, 40f), new Vector3(0.065f, 0.16f, 0.05f), material);
                var tr = CreatePrimitive(root, PrimitiveType.Cube, "ArchTopR", new Vector3(0.065f, 0.10f, 0f), new Vector3(0f, 0f, -40f), new Vector3(0.065f, 0.16f, 0.05f), material);
                var key = CreatePrimitive(root, PrimitiveType.Cube, "Keystone", new Vector3(0f, 0.16f, 0f), new Vector3(0f, 0f, 45f), new Vector3(0.08f, 0.08f, 0.06f), material);
                return new[]
                {
                    l.GetComponent<Renderer>(), r.GetComponent<Renderer>(),
                    tl.GetComponent<Renderer>(), tr.GetComponent<Renderer>(),
                    key.GetComponent<Renderer>(),
                };
            }

            default: // Compass
            {
                var h = CreatePrimitive(root, PrimitiveType.Cube, "CrossH", Vector3.zero, Vector3.zero, new Vector3(0.30f, 0.055f, 0.045f), material);
                var v = CreatePrimitive(root, PrimitiveType.Cube, "CrossV", Vector3.zero, Vector3.zero, new Vector3(0.055f, 0.30f, 0.045f), material);
                var c = CreatePrimitive(root, PrimitiveType.Sphere, "CenterDot", Vector3.zero, Vector3.zero, new Vector3(0.12f, 0.12f, 0.06f), material);
                return new[] { h.GetComponent<Renderer>(), v.GetComponent<Renderer>(), c.GetComponent<Renderer>() };
            }
        }
    }

    public static DiegeticTextPlaque CreateWorldPlaque(Transform parent, string name, Vector3 localPos, Vector3 localEuler,
        float widthMeters, float heightMeters, string title, string body, PassageMaterialSet mats,
        int titleFontSize = 52, int bodyFontSize = 36)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;
        root.transform.localRotation = Quaternion.Euler(localEuler);

        // Proportional stone & bronze frame so both large world plaques and small miniature whispers look crisp
        var stonePad = Mathf.Clamp(heightMeters * 0.14f, 0.006f, 0.055f);
        var bronzePad = stonePad * 1.45f;
        var depth = Mathf.Clamp(heightMeters * 0.08f, 0.004f, 0.032f);

        CreatePrimitive(root.transform, PrimitiveType.Cube, "StoneBacking",
            new Vector3(0f, 0f, depth * 0.65f), Vector3.zero, new Vector3(widthMeters + stonePad, heightMeters + stonePad, depth), mats.StoneDark);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "BronzeTrim",
            new Vector3(0f, 0f, depth * 0.85f), Vector3.zero, new Vector3(widthMeters + bronzePad, heightMeters + bronzePad, depth * 0.62f), mats.BronzeMetal);

        var canvasObj = new GameObject("Canvas");
        canvasObj.transform.SetParent(root.transform, false);
        canvasObj.transform.localPosition = new Vector3(0f, 0f, -0.002f);
        canvasObj.transform.localRotation = Quaternion.identity;
        const float canvasScale = 0.001f;
        canvasObj.transform.localScale = Vector3.one * canvasScale;

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var canvasGroup = canvasObj.AddComponent<CanvasGroup>();

        var canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(widthMeters / canvasScale, heightMeters / canvasScale);

        var bgObj = new GameObject("Backdrop");
        bgObj.transform.SetParent(canvasObj.transform, false);
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.08f, 0.10f, 0.15f, 0.96f);
        var bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var hasBody = !string.IsNullOrEmpty(body);

        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(canvasObj.transform, false);
        var titleText = titleObj.AddComponent<Text>();
        titleText.font = font;
        titleText.fontSize = titleFontSize;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.87f, 0.52f, 1f);
        titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        titleText.verticalOverflow = VerticalWrapMode.Overflow;
        var titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.04f, hasBody ? 0.56f : 0.06f);
        titleRect.anchorMax = new Vector2(0.96f, 0.94f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        var bodyObj = new GameObject("Body");
        bodyObj.transform.SetParent(canvasObj.transform, false);
        var bodyText = bodyObj.AddComponent<Text>();
        bodyText.font = font;
        bodyText.fontSize = bodyFontSize;
        bodyText.alignment = TextAnchor.MiddleCenter;
        bodyText.color = new Color(0.94f, 0.95f, 0.98f, 1f);
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        var bodyRect = bodyObj.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.04f, 0.06f);
        bodyRect.anchorMax = new Vector2(0.96f, string.IsNullOrEmpty(title) ? 0.94f : 0.58f);
        bodyRect.offsetMin = Vector2.zero;
        bodyRect.offsetMax = Vector2.zero;

        var plaque = root.AddComponent<DiegeticTextPlaque>();
        plaque.titleText = titleText;
        plaque.bodyText = bodyText;
        plaque.canvasGroup = canvasGroup;
        plaque.SetText(title, body);
        return plaque;
    }

    private static GameObject CreateOrUpdateLanternNichePrefab(PassageMaterialSet mats)
    {
        var root = new GameObject("LanternNiche");
        // Stone housing framing a shallow alcove opening toward +Z
        CreatePrimitive(root.transform, PrimitiveType.Cube, "BackPlate", new Vector3(0f, 0f, -0.02f), Vector3.zero, new Vector3(0.42f, 0.42f, 0.04f), mats.StoneDark);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Shelf", new Vector3(0f, -0.185f, 0.09f), Vector3.zero, new Vector3(0.46f, 0.055f, 0.24f), mats.StoneTrim);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "SideLeft", new Vector3(-0.20f, 0.01f, 0.07f), Vector3.zero, new Vector3(0.05f, 0.40f, 0.18f), mats.StoneTrim);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "SideRight", new Vector3(0.20f, 0.01f, 0.07f), Vector3.zero, new Vector3(0.05f, 0.40f, 0.18f), mats.StoneTrim);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "TopLintel", new Vector3(0f, 0.205f, 0.08f), Vector3.zero, new Vector3(0.46f, 0.06f, 0.20f), mats.StoneTrim);

        // Emissive status ring seated on the shelf
        var ringObj = CreatePrimitive(root.transform, PrimitiveType.Cylinder, "StatusRing",
            new Vector3(0f, -0.15f, 0.10f), Vector3.zero, new Vector3(0.22f, 0.014f, 0.22f), mats.EmissiveDormant);

        var attach = new GameObject("SocketAttachPoint").transform;
        attach.SetParent(root.transform, false);
        attach.localPosition = new Vector3(0f, -0.135f, 0.10f);
        attach.localRotation = Quaternion.identity;

        var triggerCol = root.AddComponent<SphereCollider>();
        triggerCol.isTrigger = true;
        triggerCol.center = new Vector3(0f, -0.02f, 0.12f);
        triggerCol.radius = 0.22f;

        var socket = root.AddComponent<XRSocketInteractor>();
        socket.attachTransform = attach;
        socket.recycleDelayTime = 0.65f;
        socket.showInteractableHoverMeshes = true;
        socket.hoverSocketSnapping = true;
        socket.socketSnappingRadius = 0.18f;
        socket.interactionLayers = InteractionLayerMask.GetMask("Default");

        var nicheScript = root.AddComponent<LanternNicheSocket>();
        nicheScript.ringRenderer = ringObj.GetComponent<Renderer>();

        return SaveAsPrefabAndDestroyTemp(root, LanternNichePrefabPath);
    }

    private static GameObject CreateOrUpdatePortableLanternPrefab(PassageMaterialSet mats)
    {
        var root = new GameObject("PortableLantern");

        // Sculpted bronze & amber lantern with base at local Y=0 (sits upright cleanly in sockets & on pedestal)
        CreatePrimitive(root.transform, PrimitiveType.Cylinder, "BasePlinth", new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.16f, 0.02f, 0.16f), mats.BronzeMetal);
        CreatePrimitive(root.transform, PrimitiveType.Cylinder, "CrownCap", new Vector3(0f, 0.24f, 0f), Vector3.zero, new Vector3(0.15f, 0.022f, 0.15f), mats.BronzeMetal);
        CreatePrimitive(root.transform, PrimitiveType.Sphere, "CrownFinial", new Vector3(0f, 0.28f, 0f), Vector3.zero, new Vector3(0.06f, 0.06f, 0.06f), mats.BronzeMetal);

        // 4 bronze cage pillars
        Vector3[] corners =
        {
            new Vector3(-0.058f, 0.13f, -0.058f),
            new Vector3( 0.058f, 0.13f, -0.058f),
            new Vector3(-0.058f, 0.13f,  0.058f),
            new Vector3( 0.058f, 0.13f,  0.058f),
        };
        for (var i = 0; i < corners.Length; i++)
            CreatePrimitive(root.transform, PrimitiveType.Cube, $"Strut_{i}", corners[i], Vector3.zero, new Vector3(0.016f, 0.20f, 0.016f), mats.BronzeMetal);

        // Handle arch on top
        CreatePrimitive(root.transform, PrimitiveType.Cube, "HandleLeft", new Vector3(-0.055f, 0.295f, 0f), new Vector3(0f, 0f, -18f), new Vector3(0.014f, 0.09f, 0.014f), mats.BronzeMetal);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "HandleRight", new Vector3(0.055f, 0.295f, 0f), new Vector3(0f, 0f, 18f), new Vector3(0.014f, 0.09f, 0.014f), mats.BronzeMetal);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "HandleGrip", new Vector3(0f, 0.335f, 0f), Vector3.zero, new Vector3(0.095f, 0.018f, 0.018f), mats.BronzeMetal);

        // Glowing amber core
        var glassCore = CreatePrimitive(root.transform, PrimitiveType.Cylinder, "AmberCore", new Vector3(0f, 0.13f, 0f), Vector3.zero, new Vector3(0.095f, 0.088f, 0.095f), mats.EmissiveGold);
        var flameHeart = CreatePrimitive(root.transform, PrimitiveType.Sphere, "FlameHeart", new Vector3(0f, 0.14f, 0f), Vector3.zero, new Vector3(0.055f, 0.075f, 0.055f), mats.EmissiveGold);

        var lightObj = new GameObject("LanternWarmLight");
        lightObj.transform.SetParent(root.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        var pointLight = lightObj.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = new Color(1f, 0.64f, 0.24f, 1f);
        pointLight.intensity = 1.75f;
        pointLight.range = 4.6f;
        pointLight.shadows = LightShadows.None;
        pointLight.bounceIntensity = 0f;

        var attach = new GameObject("GrabAttach").transform;
        attach.SetParent(root.transform, false);
        attach.localPosition = new Vector3(0f, 0.17f, 0f);
        attach.localRotation = Quaternion.identity;

        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.17f, 0f);
        col.size = new Vector3(0.20f, 0.34f, 0.20f);

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 0.65f;
        rb.linearDamping = 2.0f;
        rb.angularDamping = 3.0f;
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var grab = root.AddComponent<XRGrabInteractable>();
        grab.attachTransform = attach;
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.useDynamicAttach = false;
        grab.forceGravityOnDetach = true;
        grab.throwOnDetach = false;
        grab.interactionLayers = InteractionLayerMask.GetMask("Default");

        var lanternItem = root.AddComponent<LanternItem>();
        lanternItem.lanternLight = pointLight;
        lanternItem.flameRenderers = new[] { glassCore.GetComponent<Renderer>(), flameHeart.GetComponent<Renderer>() };

        return SaveAsPrefabAndDestroyTemp(root, PortableLanternPrefabPath);
    }

    private static GameObject CreateOrUpdateWorldButtonPrefab(PassageMaterialSet mats)
    {
        var root = new GameObject("WorldButton");
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Housing", new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.22f, 0.04f, 0.14f), mats.StoneDark);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Rim", new Vector3(0f, 0.036f, 0f), Vector3.zero, new Vector3(0.20f, 0.02f, 0.12f), mats.BronzeMetal);
        var cap = CreatePrimitive(root.transform, PrimitiveType.Cube, "Cap", new Vector3(0f, 0.054f, 0f), Vector3.zero, new Vector3(0.16f, 0.032f, 0.09f), mats.EmissiveDormant);

        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.05f, 0f);
        col.size = new Vector3(0.26f, 0.11f, 0.18f);

        var simple = root.AddComponent<XRSimpleInteractable>();
        simple.interactionLayers = InteractionLayerMask.GetMask("Default");

        var btn = root.AddComponent<XRWorldButtonInteractable>();
        btn.buttonCap = cap.transform;
        btn.highlightRenderers = new[] { cap.GetComponent<Renderer>() };

        return SaveAsPrefabAndDestroyTemp(root, WorldButtonPrefabPath);
    }

    private static GameObject CreateOrUpdateExitLeverPrefab(PassageMaterialSet mats)
    {
        var root = new GameObject("ExitLever");

        // Stone console base (top at Y = 0.96m)
        CreatePrimitive(root.transform, PrimitiveType.Cube, "ConsoleBase", new Vector3(0f, 0.46f, 0f), Vector3.zero, new Vector3(0.56f, 0.92f, 0.44f), mats.StoneDark);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "ConsoleCap", new Vector3(0f, 0.94f, 0f), Vector3.zero, new Vector3(0.62f, 0.06f, 0.50f), mats.StoneTrim);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "SlotGuardLeft", new Vector3(-0.10f, 1.06f, 0f), Vector3.zero, new Vector3(0.05f, 0.22f, 0.34f), mats.BronzeMetal);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "SlotGuardRight", new Vector3(0.10f, 1.06f, 0f), Vector3.zero, new Vector3(0.05f, 0.22f, 0.34f), mats.BronzeMetal);

        var crystal = CreatePrimitive(root.transform, PrimitiveType.Cube, "StatusCrystal",
            new Vector3(0f, 0.72f, 0.23f), new Vector3(0f, 0f, 45f), new Vector3(0.11f, 0.11f, 0.04f), mats.EmissiveDormant);

        var pivot = new GameObject("LeverPivot").transform;
        pivot.SetParent(root.transform, false);
        pivot.localPosition = new Vector3(0f, 1.06f, 0f);
        pivot.localRotation = Quaternion.Euler(-38f, 0f, 0f);

        CreatePrimitive(pivot, PrimitiveType.Cylinder, "LeverShaft", new Vector3(0f, 0.24f, 0f), Vector3.zero, new Vector3(0.04f, 0.24f, 0.04f), mats.BronzeMetal);

        var handleObj = new GameObject("LeverHandle");
        handleObj.transform.SetParent(pivot, false);
        handleObj.transform.localPosition = new Vector3(0f, 0.48f, 0f);
        var gripBar = CreatePrimitive(handleObj.transform, PrimitiveType.Cylinder, "GripBar", Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.055f, 0.18f, 0.055f), mats.EmissiveDormant);

        var handleCol = handleObj.AddComponent<BoxCollider>();
        handleCol.center = Vector3.zero;
        handleCol.size = new Vector3(0.46f, 0.22f, 0.24f);

        var handleInteractable = handleObj.AddComponent<XRSimpleInteractable>();
        handleInteractable.interactionLayers = InteractionLayerMask.GetMask("Default");

        var leverView = root.AddComponent<ExitLeverView>();
        leverView.leverPivot = pivot;
        leverView.handleInteractable = handleInteractable;
        leverView.indicatorRenderers = new[] { crystal.GetComponent<Renderer>(), gripBar.GetComponent<Renderer>() };

        return SaveAsPrefabAndDestroyTemp(root, ExitLeverPrefabPath);
    }

    private static GameObject CreateOrUpdateRouteBeaconPrefab(PassageMaterialSet mats)
    {
        var root = new GameObject("RouteBeacon");
        CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Rim", new Vector3(0f, 0.012f, 0f), Vector3.zero, new Vector3(0.46f, 0.012f, 0.46f), mats.BronzeMetal);
        CreatePrimitive(root.transform, PrimitiveType.Cylinder, "GlowDisc", new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.32f, 0.01f, 0.32f), mats.EmissiveDormant);
        return SaveAsPrefabAndDestroyTemp(root, RouteBeaconPrefabPath);
    }

    private static GameObject SaveAsPrefabAndDestroyTemp(GameObject tempObject, string path)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(tempObject, path);
        Object.DestroyImmediate(tempObject);
        return prefab;
    }

    private static GameObject LoadRequiredPrefab(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            throw new FileNotFoundException($"Required prefab not found: {path}");
        return prefab;
    }

    private static Material LoadOrCreateLitMaterial(string path, Color baseColor, float metallic, float smoothness,
        bool enableEmission, Color emissionColor)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new System.InvalidOperationException("Shader 'Universal Render Pipeline/Lit' not found.");

        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }

        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (enableEmission)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emissionColor);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }

        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material LoadOrCreateUnlitMaterial(string path, Color baseColor)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            throw new System.InvalidOperationException("Shader 'Universal Render Pipeline/Unlit' not found.");

        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }

        mat.SetColor("_BaseColor", baseColor);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material LoadOrCreateNightSkybox()
    {
        const string skyboxMatPath = "Assets/Art/NightSkybox.mat";
        var shader = Shader.Find("Skybox/Cubemap");
        var cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(NightSkyboxTexturePath);
        if (shader == null || cubemap == null)
            throw new System.InvalidOperationException("Night skybox shader or cubemap asset is missing.");

        var mat = AssetDatabase.LoadAssetAtPath<Material>(skyboxMatPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = "NightSkybox" };
            AssetDatabase.CreateAsset(mat, skyboxMatPath);
        }

        mat.shader = shader;
        mat.SetTexture("_Tex", cubemap);
        mat.SetColor("_Tint", new Color(0.82f, 0.86f, 1f, 1f));
        mat.SetFloat("_Exposure", 0.9f);
        mat.SetFloat("_Rotation", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
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

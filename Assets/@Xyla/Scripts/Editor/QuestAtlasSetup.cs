using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using UnityEngine.UI;
using Xyla;
using Oculus.Interaction.Editor;

public static class QuestAtlasSetup
{
    const string ScenePath = "Assets/@Xyla/Scenes/HumanAtlasQuest.unity";
    const string AtlasPath = "Assets/@Xyla/Data/Atlas.asset";
    const string RigPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
    const string InteractionPath = "Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/OVRInteractionComprehensive.prefab";
    const string ApkPath = "Builds/HumanAtlasQuest.apk";

    [MenuItem("Atlas Humano Quest/Preparar aplicación Meta Quest")]
    public static void Prepare()
    {
        Xyla.Editor.AtlasBrainTissueSetup.Ensure();
        Xyla.Editor.AtlasTissueSetup.Ensure();
        Xyla.Editor.QuestAtlasSceneMenuSetup.EnsureLandingScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject rig = InstantiatePrefab(RigPath, "Building Block · Cámara Meta XR");
        rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        UnityObjectAddedBroadcaster.HandleObjectWasAdded(rig);
        ConfigureRigCameras(rig);

        GameObject interactions = InstantiatePrefab(InteractionPath, "Building Block · Interacciones de manos y controles");
        interactions.transform.SetParent(rig.transform, false);
        
        ConfigureMetaBuildingBlocks(rig, interactions);
UnityObjectAddedBroadcaster.HandleObjectWasAdded(interactions);

        CreateLighting();
        CreateEnvironment();

        GameObject atlasRoot = new GameObject("Atlas Humano · 2.234 piezas agarrables");
        atlasRoot.transform.position = new Vector3(0f, 0f, 0.85f);
        QuestAtlasController controller = atlasRoot.AddComponent<QuestAtlasController>();
        controller.data = AssetDatabase.LoadAssetAtPath<AtlasData>(AtlasPath);
        if (controller.data == null)
            throw new FileNotFoundException("No se encontró Atlas.asset.", AtlasPath);

        controller.statusText = CreateStatusPanel().GetComponentInChildren<Text>(true);
        controller.sceneMenu = Xyla.Editor.QuestAtlasSceneMenuSetup.CreateMenu(false);
        Xyla.Editor.QuestAtlasPsychologySetup.EnsureAtlasPanel();
        Xyla.Editor.QuestPatientSetup.Ensure();
        Xyla.Editor.QuestPatientOfficeSetup.Ensure();

        if (!Directory.Exists("Assets/@Xyla/Scenes"))
            Directory.CreateDirectory("Assets/@Xyla/Scenes");

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(Xyla.Editor.QuestAtlasSceneMenuSetup.LandingScenePath, true),
            new EditorBuildSettingsScene(ScenePath, true)
        };

        ConfigurePlayer();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeGameObject = atlasRoot;
        EditorGUIUtility.PingObject(atlasRoot);
        Debug.Log("[Human Atlas Quest] Escena creada con Meta XR All-in-One, Building Blocks, 2.234 piezas y escala a dos manos.");
    }

    [MenuItem("Atlas Humano Quest/Construir APK")]
    public static void BuildApk()
    {
        Xyla.Editor.AtlasBrainTissueSetup.Ensure();
        Xyla.Editor.AtlasTissueSetup.Ensure();
        Xyla.Editor.AtlasTissueValidation.Run();
        if (!File.Exists(ScenePath))
            Prepare();
        Xyla.Editor.QuestAtlasSceneMenuSetup.EnsureLandingScene();

        ConfigurePlayer();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Xyla.Editor.QuestAtlasSceneMenuSetup.EnsureAtlasMenu();
            Xyla.Editor.QuestAtlasPsychologySetup.EnsureAtlasPanel();
            Xyla.Editor.QuestPatientSetup.Ensure();
            Xyla.Editor.QuestPatientOfficeSetup.Ensure();
            Xyla.Editor.QuestAtlasGuideSetup.Ensure();
            Xyla.Editor.QuestAtlasInputSetup.ConfigureScene();
            Xyla.Editor.QuestLiveKitSetup.Ensure();
            EditorSceneManager.SaveOpenScenes();
            EditorSceneManager.OpenScene(Xyla.Editor.QuestAtlasSceneMenuSetup.LandingScenePath, OpenSceneMode.Single);
            Xyla.Editor.QuestAtlasPsychologySetup.EnsureLandingShortcut();
            Xyla.Editor.QuestAtlasInputSetup.ConfigureScene();
            Xyla.Editor.QuestLiveKitSetup.Ensure();
            EditorSceneManager.SaveOpenScenes();
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(Xyla.Editor.QuestAtlasSceneMenuSetup.LandingScenePath, true),
            new EditorBuildSettingsScene(ScenePath, true)
        };
        Directory.CreateDirectory("Builds");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { Xyla.Editor.QuestAtlasSceneMenuSetup.LandingScenePath, ScenePath },
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("La compilación APK no terminó correctamente: " + report.summary.result);

        Debug.Log("[Human Atlas Quest] APK creado: " + Path.GetFullPath(ApkPath));
    }

    static GameObject InstantiatePrefab(string path, string name)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            throw new FileNotFoundException("No se encontró el prefab oficial de Meta XR.", path);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        return instance;
    }

    static void ConfigureRigCameras(GameObject rig)
    {
        foreach (Camera camera in rig.GetComponentsInChildren<Camera>(true))
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.025f, 0.055f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
        }
        RenderSettings.skybox = null;
    }

static void ConfigureMetaBuildingBlocks(GameObject rig, GameObject interactions)
    {
        OVRManager manager = rig.GetComponent<OVRManager>();
        if (manager != null)
        {
            SerializedObject serializedManager = new SerializedObject(manager);
            SerializedProperty launchTogether =
                serializedManager.FindProperty("launchSimultaneousHandsControllersOnStartup");
            SerializedProperty controllerHands =
                serializedManager.FindProperty("controllerDrivenHandPosesType");

            if (launchTogether != null) launchTogether.boolValue = true;
            if (controllerHands != null) controllerHands.intValue = 2;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        Transform locomotor = interactions.transform.Find("Locomotor");
        Transform leftHand = interactions.transform.Find("OVRLeftHandVisual");
        Transform rightHand = interactions.transform.Find("OVRRightHandVisual");
        Transform smoothMove = interactions.transform.Find(
            "LeftInteractions/Interactors/Controller/LocomotionControllerInteractorGroup/ControllerSlideInteractor");
        Transform smoothTurn = interactions.transform.Find(
            "RightInteractions/Interactors/Controller/LocomotionControllerInteractorGroup/ControllerTurnerInteractor");

        if (locomotor != null)
        {
            locomotor.gameObject.SetActive(true);
            locomotor.name = "Building Block · Locomoción Meta XR";
        }

        if (leftHand != null)
        {
            leftHand.gameObject.SetActive(true);
            leftHand.name = "Building Block · Mano izquierda Meta XR";
        }

        if (rightHand != null)
        {
            rightHand.gameObject.SetActive(true);
            rightHand.name = "Building Block · Mano derecha Meta XR";
        }

        if (smoothMove != null) smoothMove.gameObject.SetActive(true);
        if (smoothTurn != null) smoothTurn.gameObject.SetActive(true);
    }


static void CreateLighting()
    {
        GameObject lightObject = new GameObject("Iluminación suave");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.92f, 0.96f, 1f);
        light.intensity = 1.1f;
        light.shadows = LightShadows.None;
        lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.16f, 0.21f, 0.30f);
        RenderSettings.ambientEquatorColor = new Color(0.07f, 0.10f, 0.16f);
        RenderSettings.ambientGroundColor = new Color(0.025f, 0.035f, 0.06f);
        RenderSettings.fog = false;
    }

    static void CreateEnvironment()
    {
        Material floorMaterial = CreateMaterial("Suelo VR", new Color(0.035f, 0.065f, 0.10f), 0.35f, 0.55f);

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Suelo de referencia";
        floor.transform.SetPositionAndRotation(new Vector3(0f, -0.04f, 1.1f), Quaternion.identity);
        floor.transform.localScale = new Vector3(5f, 0.08f, 5f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;

        GameObject title = CreateWorldText(
            "Título del atlas",
            "ATLAS HUMANO VR",
            new Vector3(0f, 2.05f, 1.05f),
            new Vector2(900f, 100f),
            54,
            TextAnchor.MiddleCenter,
            new Color(0.72f, 0.94f, 1f));
        title.transform.rotation = Quaternion.identity;
    }

    static GameObject CreateStatusPanel()
    {
        GameObject canvasObject = new GameObject("Panel de ayuda en español", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(620f, 520f);
        canvasObject.transform.SetPositionAndRotation(
            new Vector3(-0.78f, 1.32f, 0.95f),
            Quaternion.Euler(0f, -15f, 0f));
        canvasObject.transform.localScale = Vector3.one * 0.00115f;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 20f;

        GameObject background = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvasObject.transform, false);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image image = background.GetComponent<Image>();
        image.color = new Color(0.018f, 0.045f, 0.075f, 0.94f);

        GameObject accent = new GameObject("Acento", typeof(RectTransform), typeof(Image));
        accent.transform.SetParent(background.transform, false);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0.018f, 1f);
        accentRect.offsetMin = Vector2.zero;
        accentRect.offsetMax = Vector2.zero;
        accent.GetComponent<Image>().color = new Color(0.12f, 0.78f, 0.92f, 1f);

        GameObject textObject = new GameObject("Estado e instrucciones", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(background.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(38f, 28f);
        textRect.offsetMax = new Vector2(-28f, -28f);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = new Color(0.90f, 0.97f, 1f);
        text.text = "ATLAS HUMANO VR\n\nPreparando anatomía…";

        return canvasObject;
    }

    static GameObject CreateWorldText(string name, string value, Vector3 position, Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = size;
        canvasObject.transform.position = position;
        canvasObject.transform.localScale = Vector3.one * 0.001f;

        GameObject textObject = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.text = value;
        return canvasObject;
    }

    static Material CreateMaterial(string name, Color color, float metallic, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        return material;
    }

    static void EnsureOpenXRAndroid()
    {
        const string settingsPath = "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset";
        XRGeneralSettingsPerBuildTarget perBuildTarget =
            AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(settingsPath);

        if (perBuildTarget == null)
        {
            Directory.CreateDirectory("Assets/XR/Settings");
            perBuildTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perBuildTarget, settingsPath);
        }

        EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perBuildTarget, true);

        if (!perBuildTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android))
            perBuildTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
        if (!perBuildTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            perBuildTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);

        XRGeneralSettings general = perBuildTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
        general.InitManagerOnStart = true;
        XRManagerSettings manager = perBuildTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        manager.automaticLoading = true;
        manager.automaticRunning = true;

        if (!XRPackageMetadataStore.AssignLoader(
                manager,
                "UnityEngine.XR.OpenXR.OpenXRLoader",
                BuildTargetGroup.Android))
            throw new InvalidOperationException("No se pudo activar OpenXR para Android.");

        EditorUtility.SetDirty(perBuildTarget);
        EditorUtility.SetDirty(general);
        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();
    }

static void ConfigurePlayer()
    {
        PlayerSettings.companyName = "Human Atlas";
        PlayerSettings.productName = "Human Atlas Quest";
        PlayerSettings.bundleVersion = "2.3.0";
        PlayerSettings.Android.bundleVersionCode = 58;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.humanatlas.quest");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

        OVRProjectConfig metaConfig = OVRProjectConfig.CachedProjectConfig;
        metaConfig.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
        metaConfig.handTrackingFrequency = OVRProjectConfig.HandTrackingFrequency.HIGH;
        metaConfig.renderModelSupport = OVRProjectConfig.RenderModelSupport.Enabled;
        metaConfig.insightPassthroughEnabled = true;
        OVRProjectConfig.CommitProjectConfig(metaConfig);

        QualitySettings.vSyncCount = 0;
        QualitySettings.antiAliasing = 2;
        QualitySettings.pixelLightCount = 1;
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.shadowDistance = 0f;
        QualitySettings.lodBias = 1f;

        
        EnsureOpenXRAndroid();
        Xyla.Editor.QuestAtlasInputSetup.ConfigureProfiles();
        RemoveOpenXRTestFeatures();

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
    }


static void RemoveOpenXRTestFeatures()
    {
        const string packageSettingsPath = "Assets/XR/Settings/OpenXR Package Settings.asset";
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(packageSettingsPath);
        var testFeatures = new System.Collections.Generic.List<UnityEngine.Object>();

        foreach (UnityEngine.Object asset in assets)
        {
            if (asset != null &&
                asset.GetType().FullName.StartsWith("UnityEngine.XR.OpenXR.Tests."))
                testFeatures.Add(asset);
        }

        foreach (UnityEngine.Object asset in assets)
        {
            if (asset == null ||
                asset.GetType().FullName != "UnityEngine.XR.OpenXR.OpenXRSettings")
                continue;

            SerializedObject settings = new SerializedObject(asset);
            SerializedProperty features = settings.FindProperty("features");
            if (features == null) continue;

            for (int i = features.arraySize - 1; i >= 0; i--)
            {
                UnityEngine.Object feature =
                    features.GetArrayElementAtIndex(i).objectReferenceValue;
                if (feature != null && testFeatures.Contains(feature))
                    features.DeleteArrayElementAtIndex(i);
            }

            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        foreach (UnityEngine.Object feature in testFeatures)
            UnityEngine.Object.DestroyImmediate(feature, true);

        AssetDatabase.SaveAssets();
    }
}

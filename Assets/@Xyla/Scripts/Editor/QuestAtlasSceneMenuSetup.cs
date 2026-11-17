using System;
using System.IO;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Xyla;

namespace Xyla.Editor
{
    public static class QuestAtlasSceneMenuSetup
    {
        public const string LandingScenePath = "Assets/@Xyla/Scenes/AtlasInicio.unity";
        const string RigPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
        const string InteractionPath = "Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/OVRInteractionComprehensive.prefab";
        static readonly Color Navy = new Color(.012f, .027f, .049f, .975f);
        static readonly Color Cyan = new Color(.16f, .86f, .92f, 1f);
        static readonly Color Muted = new Color(.59f, .75f, .83f, 1f);
        static Font font;

        public static void EnsureLandingScene()
        {
            if (!File.Exists(LandingScenePath)) CreateLandingScene();
        }

        public static void EnsureAtlasMenu()
        {
            QuestAtlasController controller = UnityEngine.Object.FindFirstObjectByType<QuestAtlasController>();
            if (!controller) throw new InvalidOperationException("Falta QuestAtlasController en la escena del atlas.");
            if (!controller.sceneMenu)
            {
                QuestAtlasSceneMenu existing = UnityEngine.Object.FindFirstObjectByType<QuestAtlasSceneMenu>(FindObjectsInactive.Include);
                controller.sceneMenu = existing ? existing : CreateMenu(false);
                EditorUtility.SetDirty(controller);
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            }
        }

        [MenuItem("Atlas Humano Quest/Crear escena de inicio")]
        public static void CreateLandingScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject rig = Instantiate(RigPath, "Building Block · Cámara Meta XR · Inicio");
            rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (Camera camera in rig.GetComponentsInChildren<Camera>(true))
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.004f, .012f, .026f, 1f);
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 30f;
            }
            GameObject interactions = Instantiate(InteractionPath, "Building Block · Manos y controles · Inicio");
            interactions.transform.SetParent(rig.transform, false);
            Transform locomotor = interactions.transform.Find("Locomotor");
            if (locomotor) locomotor.gameObject.SetActive(false);
            OVRManager manager = rig.GetComponent<OVRManager>();
            manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            manager.SimultaneousHandsAndControllersEnabled = true;
            manager.launchSimultaneousHandsControllersOnStartup = true;
            manager.controllerDrivenHandPosesType = OVRManager.ControllerDrivenHandPosesType.Natural;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.07f, .13f, .2f);
            CreateMenu(true);
            Directory.CreateDirectory("Assets/@Xyla/Scenes");
            EditorSceneManager.SaveScene(scene, LandingScenePath);
            Debug.Log("[Atlas] Escena de inicio independiente creada: " + LandingScenePath);
        }

        public static QuestAtlasSceneMenu CreateMenu(bool landing)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject(landing ? "MENÚ DE INICIO · editable" : "MENÚ START · editable",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PointableCanvas), typeof(AudioSource));
            var menu = go.AddComponent<QuestAtlasSceneMenu>();
            menu.isLandingScene = landing;
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 60;
            canvas.worldCamera = Camera.main;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1040f, 760f);
            go.transform.SetPositionAndRotation(new Vector3(0f, 1.54f, landing ? 1.4f : .7f), Quaternion.identity);
            go.transform.localScale = Vector3.one * .0011f;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            AudioSource sound = go.GetComponent<AudioSource>();
            sound.playOnAwake = false; sound.spatialBlend = 0f; sound.volume = .28f;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(1040f, 760f, .1f);
            collider.isTrigger = true;
            var surface = go.AddComponent<Oculus.Interaction.Surfaces.ColliderSurface>();
            surface.InjectCollider(collider);
            var ray = go.AddComponent<RayInteractable>();
            ray.InjectSurface(surface);
            ray.InjectOptionalPointableElement(go.GetComponent<PointableCanvas>());
            go.GetComponent<PointableCanvas>().InjectCanvas(canvas);
            EnsureEventSystem();

            Transform bg = Image("Panel principal", go.transform, Vector2.zero, new Vector2(1040, 760), Navy).transform;
            Image("Borde superior", bg, new Vector2(0, 376), new Vector2(1040, 6), Cyan);
            Image("Borde inferior", bg, new Vector2(0, -376), new Vector2(1040, 4), new Color(.05f, .37f, .48f, 1f));
            Image("Lateral izquierdo", bg, new Vector2(-516, 0), new Vector2(7, 760), new Color(.05f, .37f, .48f, 1f));
            Image("Barra de título", bg, new Vector2(0, 244), new Vector2(942, 2), new Color(.07f, .34f, .43f, 1f));
            Text("Título", bg, "A T L A S   H U M A N O", new Vector2(0, 322), new Vector2(900, 72), 42, TextAnchor.MiddleCenter, Color.white);
            Text("Subtítulo", bg, landing ? "LABORATORIO DE ANATOMÍA INMERSIVA" : "CENTRO DE CONTROL · SESIÓN ACTIVA",
                new Vector2(0, 272), new Vector2(900, 38), 20, TextAnchor.MiddleCenter, Cyan);
            Image("Identificador médico", bg, new Vector2(-460, 315), new Vector2(34, 7), Cyan);
            Image("Identificador médico vertical", bg, new Vector2(-460, 315), new Vector2(7, 34), Cyan);
            Text("Pie de navegación", bg,
                landing ? "APUNTA Y SELECCIONA CON LA MANO O EL CONTROL" : "PULSA START PARA CERRAR · A / X PARA HABLAR CON ATLAS",
                new Vector2(0, -341), new Vector2(950, 34), 19, TextAnchor.MiddleCenter, Muted);

            menu.mainPage = Page("01 · Inicio", bg);
            menu.settingsPage = Page("02 · Ajustes", bg);
            menu.creditsPage = Page("03 · Créditos", bg);
            BuildMain(menu, landing);
            BuildSettings(menu);
            BuildCredits(menu);
            menu.mainPage.SetActive(true);
            menu.settingsPage.SetActive(false);
            menu.creditsPage.SetActive(false);
            if (!landing) go.SetActive(false);
            return menu;
        }

        static void BuildMain(QuestAtlasSceneMenu menu, bool landing)
        {
            Transform p = menu.mainPage.transform;
            Text("Mensaje", p,
                landing ? "Explora 2.234 piezas anatómicas con manos, controles y Atlas." : "Sesión en curso. Elige cómo quieres continuar.",
                new Vector2(0, 155), new Vector2(900, 54), 25, TextAnchor.MiddleCenter, Color.white);
            if (landing)
            {
                Button("Entrar al atlas", p, "ENTRAR AL ATLAS", new Vector2(0, 70), new Vector2(620, 68), menu.EnterAtlas, true);
                menu.loadingLabel = Text("Estado de carga", p, "", new Vector2(0, 15), new Vector2(800, 34), 20, TextAnchor.MiddleCenter, Cyan);
            }
            else
            {
                Button("Continuar", p, "CONTINUAR EXPLORACIÓN", new Vector2(0, 82), new Vector2(620, 66), menu.Hide, true);
                Button("Abrir panel del atlas", p, "PANEL DEL ATLAS · SISTEMAS Y JUEGOS", new Vector2(0, 0), new Vector2(620, 60), menu.OpenAtlasPanel, false);
                menu.loadingLabel = Text("Estado de carga", p, "", new Vector2(0, -265), new Vector2(800, 30), 18, TextAnchor.MiddleCenter, Muted);
            }
            Button("Abrir ajustes", p, "AJUSTES", new Vector2(-158, landing ? -88 : -94), new Vector2(300, 58), menu.OpenSettings, false);
            Button("Abrir créditos", p, "CRÉDITOS", new Vector2(158, landing ? -88 : -94), new Vector2(300, 58), menu.OpenCredits, false);
            if (!landing) Button("Volver a inicio", p, "VOLVER A INICIO", new Vector2(0, -181), new Vector2(620, 54), menu.ReturnToStart, false);
            Text("Ayuda rápida", p, "START: MENÚ   ·   A / X: HABLAR   ·   GATILLO: SELECCIONAR",
                new Vector2(0, landing ? -194 : -264), new Vector2(900, 36), 18, TextAnchor.MiddleCenter, Muted);
        }

        static void BuildSettings(QuestAtlasSceneMenu menu)
        {
            Transform p = menu.settingsPage.transform;
            Text("Encabezado", p, "AJUSTES DE LA EXPERIENCIA", new Vector2(0, 183), new Vector2(900, 50), 31, TextAnchor.MiddleCenter, Color.white);
            menu.passthroughLabel = Button("Passthrough", p, "PASSTHROUGH AZULADO", new Vector2(0, 111), new Vector2(790, 56), menu.TogglePassthrough, false)
                .GetComponentInChildren<Text>();
            menu.effectsLabel = Button("Partículas y efectos", p, "PARTÍCULAS Y EFECTOS", new Vector2(0, 43), new Vector2(790, 56), menu.ToggleEffects, false)
                .GetComponentInChildren<Text>();
            menu.soundsLabel = Button("Sonidos", p, "SONIDOS DEL ENTORNO", new Vector2(0, -25), new Vector2(790, 56), menu.ToggleSounds, false)
                .GetComponentInChildren<Text>();
            menu.voiceVolumeLabel = Text("Volumen de Atlas", p, "VOZ DE ATLAS", new Vector2(0, -99), new Vector2(470, 48), 22, TextAnchor.MiddleCenter, Color.white);
            Button("Bajar volumen de Atlas", p, "−", new Vector2(-308, -99), new Vector2(74, 50), menu.DecreaseVoiceVolume, false);
            Button("Subir volumen de Atlas", p, "+", new Vector2(308, -99), new Vector2(74, 50), menu.IncreaseVoiceVolume, false);
            Button("Volver al menú", p, "VOLVER", new Vector2(0, -207), new Vector2(350, 54), menu.OpenMain, false);
        }

        static void BuildCredits(QuestAtlasSceneMenu menu)
        {
            Transform p = menu.creditsPage.transform;
            Text("Encabezado", p, "CRÉDITOS", new Vector2(0, 184), new Vector2(900, 55), 32, TextAnchor.MiddleCenter, Color.white);
            Text("Contenido de créditos · editable", p,
                "ATLAS HUMANO VR\nExperiencia educativa de anatomía humana\nUnity 6000.4.10f1  ·  Meta XR All-in-One\nInteracción con manos y controles  ·  Asistente Atlas",
                new Vector2(0, 17), new Vector2(860, 260), 24, TextAnchor.MiddleCenter, Color.white);
            Text("Nota", p, "Este texto puede modificarse directamente en la jerarquía de Unity.",
                new Vector2(0, -149), new Vector2(860, 38), 18, TextAnchor.MiddleCenter, Muted);
            Button("Volver al menú", p, "VOLVER", new Vector2(0, -219), new Vector2(350, 54), menu.OpenMain, false);
        }

        static GameObject Instantiate(string path, string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) throw new FileNotFoundException("Falta un prefab de Meta XR", path);
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            return go;
        }

        static void EnsureEventSystem()
        {
            EventSystem system = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (!system) new GameObject("Eventos · menús Meta XR", typeof(EventSystem), typeof(PointableCanvasModule));
            else if (!system.GetComponent<PointableCanvasModule>()) system.gameObject.AddComponent<PointableCanvasModule>();
        }

        static GameObject Page(string name, Transform parent)
        {
            GameObject page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(parent, false);
            page.GetComponent<RectTransform>().sizeDelta = new Vector2(1040, 760);
            return page;
        }

        static GameObject Image(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        static Text Text(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor anchor, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        static Button Button(string name, Transform parent, string label, Vector2 position, Vector2 size,
            UnityEngine.Events.UnityAction action, bool primary)
        {
            GameObject go = Image(name, parent, position, size,
                primary ? new Color(.075f, .38f, .48f, .98f) : new Color(.035f, .095f, .14f, .97f));
            Button button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            UnityEventTools.AddPersistentListener(button.onClick, action);
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(.28f, .7f, .8f, 1f);
            colors.pressedColor = new Color(.5f, .82f, .9f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.fadeDuration = .08f;
            button.colors = colors;
            Text("Etiqueta", go.transform, label, Vector2.zero, size - new Vector2(24, 10), 22, TextAnchor.MiddleCenter, Color.white);
            return button;
        }
    }
}

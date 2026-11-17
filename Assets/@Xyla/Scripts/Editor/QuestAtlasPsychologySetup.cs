using System;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Xyla;

namespace Xyla.Editor
{
    public static class QuestAtlasPsychologySetup
    {
        static readonly Color Cyan = new Color(.16f, .86f, .92f, 1f);
        static readonly Color Muted = new Color(.62f, .78f, .84f, 1f);
        static Font font;

        [MenuItem("Atlas Humano Quest/Instalar panel de psicología")]
        public static void EnsureAtlasPanel()
        {
            QuestAtlasController controller = UnityEngine.Object.FindFirstObjectByType<QuestAtlasController>();
            if (!controller) throw new InvalidOperationException("Abre la escena HumanAtlasQuest antes de instalar psicología.");
            if (!controller.psychologyPanel)
            {
                QuestAtlasPsychology existing = UnityEngine.Object.FindFirstObjectByType<QuestAtlasPsychology>(FindObjectsInactive.Include);
                controller.psychologyPanel = existing ? existing : CreatePanel();
                EditorUtility.SetDirty(controller);
            }
            EnsureHomePage(controller.psychologyPanel);
            ValidateMissionTargets(controller.data, controller.psychologyPanel.missions);
            QuestAtlasSceneMenu startMenu = controller.sceneMenu;
            AddShortcut(startMenu, false);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }

        public static void EnsureLandingShortcut()
        {
            QuestAtlasSceneMenu[] menus = UnityEngine.Object.FindObjectsByType<QuestAtlasSceneMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            QuestAtlasSceneMenu landing = Array.Find(menus, menu => menu.isLandingScene);
            if (!landing) throw new InvalidOperationException("Falta el menú de la escena AtlasInicio.");
            AddShortcut(landing, true);
            EditorSceneManager.MarkSceneDirty(landing.gameObject.scene);
        }

        static void AddShortcut(QuestAtlasSceneMenu menu, bool landing)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Transform page = menu.mainPage.transform;
            Transform existing = page.Find("Abrir psicología");
            if (existing)
            {
                Text label = existing.GetComponentInChildren<Text>();
                label.text = "PSICOLOGÍA · PACIENTE IA";
                EditorUtility.SetDirty(label);
                return;
            }
            Button("Abrir psicología", page, "PSICOLOGÍA · PACIENTE IA",
                new Vector2(0, landing ? -172 : -165), new Vector2(620, 55),
                landing ? (UnityEngine.Events.UnityAction)menu.EnterPsychology : menu.OpenPsychology, true);
            Transform help = page.Find("Ayuda rápida");
            if (help) help.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, landing ? -268 : -306);
            if (!landing)
            {
                Transform back = page.Find("Volver a inicio");
                if (back) back.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -238);
            }
        }

        static QuestAtlasPsychology CreatePanel()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("PSICOLOGÍA · panel de misiones y tutorial · editable",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PointableCanvas));
            var panel = go.AddComponent<QuestAtlasPsychology>();
            panel.missions = CreateMissions();
            panel.tutorialSteps = new[]
            {
                "Bienvenido a neuropsicología. Verás varias estructuras reales del sistema nervioso. Cada misión relaciona una función con una pieza del atlas; ninguna conducta depende de una sola región.",
                "Elige una tarjeta de misión. Lee la pregunta, apunta con el rayo o toca una pieza visible y selecciónala. Puedes usar manos o controles.",
                "Después de cada elección recibirás retroalimentación. Si no aciertas, lee la pista y vuelve a intentar. Al acertar, consulta la ficha anatómica y continúa con otra misión."
            };
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 55;
            canvas.worldCamera = Camera.main;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(1100, 850);
            go.transform.SetPositionAndRotation(new Vector3(1.18f, 1.43f, 1.18f), Quaternion.Euler(0, 20, 0));
            go.transform.localScale = Vector3.one * .001f;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(1100, 850, .1f);
            collider.isTrigger = true;
            var surface = go.AddComponent<Oculus.Interaction.Surfaces.ColliderSurface>();
            surface.InjectCollider(collider);
            var ray = go.AddComponent<RayInteractable>();
            ray.InjectSurface(surface);
            ray.InjectOptionalPointableElement(go.GetComponent<PointableCanvas>());
            go.GetComponent<PointableCanvas>().InjectCanvas(canvas);
            EnsureEventSystem();

            Transform bg = Image("Cristal holográfico", go.transform, Vector2.zero, new Vector2(1100, 850),
                new Color(.008f, .035f, .058f, .96f)).transform;
            Image("Borde superior", bg, new Vector2(0, 422), new Vector2(1100, 5), Cyan);
            Image("Borde inferior", bg, new Vector2(0, -422), new Vector2(1100, 3), new Color(.04f, .36f, .48f));
            Image("Lateral", bg, new Vector2(-547, 0), new Vector2(5, 850), new Color(.04f, .36f, .48f));
            Text("Título principal", bg, "NEUROPSICOLOGÍA", new Vector2(0, 369), new Vector2(1030, 60), 38, TextAnchor.MiddleCenter, Color.white);
            Text("Subtítulo", bg, "EXPLORA · RELACIONA · RECIBE RETROALIMENTACIÓN", new Vector2(0, 326), new Vector2(1030, 34), 18, TextAnchor.MiddleCenter, Cyan);
            Image("Separador", bg, new Vector2(0, 296), new Vector2(1020, 2), new Color(.08f, .35f, .45f));

            panel.missionPage = Page("01 · Misiones y retroalimentación", bg);
            panel.tutorialPage = Page("02 · Tutorial", bg);
            panel.homePage = Page("03 · Elegir experiencia", bg);
            BuildMissions(panel);
            BuildTutorial(panel);
            BuildHome(panel);
            panel.homePage.SetActive(true);
            panel.missionPage.SetActive(false);
            panel.tutorialPage.SetActive(false);
            go.SetActive(false);
            return panel;
        }

        static void EnsureHomePage(QuestAtlasPsychology panel)
        {
            if (panel.homePage) return;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Transform background = panel.missionPage.transform.parent;
            panel.homePage = Page("03 · Elegir experiencia", background);
            BuildHome(panel);
            panel.homePage.SetActive(true);
            panel.missionPage.SetActive(false);
            panel.tutorialPage.SetActive(false);
            EditorUtility.SetDirty(panel);
        }

        static void BuildHome(QuestAtlasPsychology panel)
        {
            Transform page = panel.homePage.transform;
            Text("Introducción", page,
                "ELIGE CÓMO QUIERES APRENDER",
                new Vector2(0, 211), new Vector2(980, 58), 31, TextAnchor.MiddleCenter, Color.white);
            Text("Descripción de misiones", page,
                "Relaciona memoria, emoción, atención y lenguaje con estructuras reales del cerebro. Recibe pistas y retroalimentación en seis misiones.",
                new Vector2(0, 117), new Vector2(920, 100), 23, TextAnchor.MiddleCenter, Muted);
            Button("Empezar misiones", page, "EXPLORAR LAS 6 MISIONES", new Vector2(0, 25),
                new Vector2(660, 67), panel.OpenMissions, true);
            Text("Descripción de paciente", page,
                "Practica una entrevista con María, una paciente simulada. Haz preguntas por voz y recibe retroalimentación al terminar.",
                new Vector2(0, -91), new Vector2(920, 86), 23, TextAnchor.MiddleCenter, Muted);
            Button("Entrevistar a María", page, "ENTREVISTAR A MARÍA · PACIENTE IA", new Vector2(0, -183),
                new Vector2(660, 67), panel.OpenPatient, true);
            Button("Salir al atlas", page, "VOLVER AL ATLAS", new Vector2(0, -306),
                new Vector2(390, 52), panel.Close, false);
        }

        static void BuildMissions(QuestAtlasPsychology panel)
        {
            Transform page = panel.missionPage.transform;
            Text("Lista de misiones", page, "MISIONES", new Vector2(-365, 255), new Vector2(340, 45), 26, TextAnchor.MiddleCenter, Cyan);
            panel.missionLabels = new Text[6];
            UnityEngine.Events.UnityAction[] actions =
            {
                panel.StartMission0, panel.StartMission1, panel.StartMission2,
                panel.StartMission3, panel.StartMission4, panel.StartMission5
            };
            for (int i = 0; i < 6; i++)
            {
                Button button = Button("Misión " + (i + 1), page, panel.missions[i].title,
                    new Vector2(-365, 195 - i * 77), new Vector2(330, 63), actions[i], false);
                panel.missionLabels[i] = button.GetComponentInChildren<Text>();
            }
            Image("Separador vertical", page, new Vector2(-174, -3), new Vector2(2, 565), new Color(.08f, .35f, .45f));
            panel.progressLabel = Text("Progreso", page, "PROGRESO 0 / 6", new Vector2(168, 254), new Vector2(650, 40), 22, TextAnchor.MiddleLeft, Cyan);
            panel.missionTitle = Text("Título de misión", page, "ELIGE UNA MISIÓN", new Vector2(168, 194), new Vector2(650, 50), 27, TextAnchor.MiddleLeft, Color.white);
            panel.missionQuestion = Text("Pregunta", page, "", new Vector2(168, 113), new Vector2(650, 100), 24, TextAnchor.MiddleLeft, Color.white);
            Image("Separador de retroalimentación", page, new Vector2(185, 32), new Vector2(600, 2), new Color(.08f, .35f, .45f));
            panel.feedbackHeading = Text("Estado de respuesta", page, "RETROALIMENTACIÓN", new Vector2(168, -9), new Vector2(650, 42), 23, TextAnchor.MiddleLeft, Cyan);
            panel.feedbackBody = Text("Explicación", page, "", new Vector2(168, -106), new Vector2(650, 142), 22, TextAnchor.UpperLeft, Color.white);
            panel.sourceLabel = Text("Fuente", page, "", new Vector2(168, -207), new Vector2(650, 44), 16, TextAnchor.MiddleLeft, Muted);
            Button("Pista", page, "PISTA", new Vector2(11, -264), new Vector2(220, 50), panel.ShowHint, false);
            Button("Siguiente misión", page, "SIGUIENTE", new Vector2(260, -264), new Vector2(250, 50), panel.NextMission, true);
            Button("Abrir tutorial", page, "TUTORIAL", new Vector2(-320, -351), new Vector2(230, 50), panel.OpenTutorial, false);
            Button("Cerrar psicología", page, "SALIR AL ATLAS", new Vector2(315, -351), new Vector2(300, 50), panel.Close, false);
        }

        static void BuildTutorial(QuestAtlasPsychology panel)
        {
            Transform page = panel.tutorialPage.transform;
            Text("Encabezado tutorial", page, "TUTORIAL DE PSICOLOGÍA", new Vector2(0, 211), new Vector2(950, 55), 33, TextAnchor.MiddleCenter, Color.white);
            panel.tutorialStepLabel = Text("Número de paso", page, "PASO 1 DE 3", new Vector2(0, 137), new Vector2(900, 42), 24, TextAnchor.MiddleCenter, Cyan);
            panel.tutorialBody = Text("Instrucción", page, "", new Vector2(0, 7), new Vector2(850, 190), 28, TextAnchor.MiddleCenter, Color.white);
            Button("Siguiente paso", page, "SIGUIENTE PASO", new Vector2(0, -177), new Vector2(390, 64), panel.NextTutorial, true);
            Button("Ir a misiones", page, "SALTAR TUTORIAL", new Vector2(0, -257), new Vector2(390, 50), panel.SkipTutorial, false);
            Button("Salir al atlas", page, "SALIR AL ATLAS", new Vector2(0, -349), new Vector2(300, 50), panel.Close, false);
        }

        static QuestAtlasPsychology.Mission[] CreateMissions() => new[]
        {
            Mission("Memoria", "Busca una estructura temporal medial que participa en la formación de nuevos recuerdos.",
                new[] {"Left hippocampus", "Right hippocampus"},
                "El hipocampo participa en la formación y organización de recuerdos nuevos, junto con otras regiones corticales. No es un depósito único de toda la memoria.",
                "Busca una estructura profunda del lóbulo temporal; su nombre recuerda a un caballito de mar.",
                "OpenStax · Anatomía y fisiología, 13.2"),
            Mission("Emoción", "Encuentra una estructura que ayuda a detectar la relevancia emocional de los estímulos.",
                new[] {"Left amygdala", "Right amygdala"},
                "La amígdala participa en el aprendizaje y la valoración emocional, especialmente de señales importantes o amenazantes. Actúa en redes, no como un único centro de las emociones.",
                "Busca una estructura pequeña, profunda y cercana al hipocampo.",
                "OpenStax · Anatomía y fisiología, 13.2"),
            Mission("Percepción visual", "Señala la región cortical posterior relacionada con el procesamiento de la información visual.",
                new[] {"Left occipital lobe", "Right occipital lobe"},
                "El lóbulo occipital incluye áreas esenciales para el procesamiento visual. La percepción completa también requiere conexiones con otras regiones cerebrales.",
                "Busca en la parte posterior del cerebro.",
                "OpenStax · Anatomía y fisiología, 14.2"),
            Mission("Atención espacial", "Busca una región parietal superior que participa en la orientación de la atención espacial.",
                new[] {"Left superior parietal lobule", "Right superior parietal lobule"},
                "El lobulillo parietal superior participa en redes de atención y orientación espacial. La atención depende de varias áreas conectadas.",
                "Busca en la zona superior y posterior de la corteza.",
                "OpenStax · Anatomía y fisiología, 13.2; redes parietales de atención"),
            Mission("Lenguaje", "Encuentra un giro frontal izquierdo relacionado con la producción del habla en muchas personas.",
                new[] {"Left inferior frontal gyrus"},
                "El giro frontal inferior izquierdo contiene regiones importantes para la producción del lenguaje en muchas personas. El lenguaje depende de una red más amplia y varía entre individuos.",
                "Busca un giro de la parte inferior del lóbulo frontal izquierdo.",
                "OpenStax · Anatomía y fisiología, 13.2"),
            Mission("Control y emoción", "Localiza un giro que participa en redes relacionadas con atención, acción y emoción.",
                new[] {"Left cingulate gyrus", "Right cingulate gyrus"},
                "Distintas partes del giro cingulado se conectan con redes de atención, acción, memoria y emoción. La función depende de la subregión y sus conexiones.",
                "Busca un giro en la cara medial del hemisferio, por encima del cuerpo calloso.",
                "OpenStax · Anatomía y fisiología, 13.2")
        };

        static QuestAtlasPsychology.Mission Mission(string title, string question, string[] targets,
            string feedback, string hint, string source) => new QuestAtlasPsychology.Mission
            { title = title, question = question, targetEnglishNames = targets,
              correctFeedback = feedback, hint = hint, source = source };

        static void ValidateMissionTargets(AtlasData data, QuestAtlasPsychology.Mission[] missions)
        {
            if (!data || data.parts == null || missions == null || missions.Length == 0)
                throw new InvalidOperationException("Faltan datos o misiones de psicología.");
            foreach (QuestAtlasPsychology.Mission mission in missions)
            {
                if (mission == null || mission.targetEnglishNames == null || mission.targetEnglishNames.Length == 0)
                    throw new InvalidOperationException("Una misión no tiene piezas objetivo.");
                foreach (string target in mission.targetEnglishNames)
                    if (Array.FindIndex(data.parts, part => part.system == "nervous" &&
                        string.Equals(part.name, target, StringComparison.OrdinalIgnoreCase)) < 0)
                        throw new InvalidOperationException("Falta la estructura cerebral de la misión: " + target);
            }
        }

        static void EnsureEventSystem()
        {
            EventSystem system = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (!system) new GameObject("Eventos · psicología Meta XR", typeof(EventSystem), typeof(PointableCanvasModule));
            else if (!system.GetComponent<PointableCanvasModule>()) system.gameObject.AddComponent<PointableCanvasModule>();
        }

        static GameObject Page(string name, Transform parent)
        {
            GameObject page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(parent, false);
            page.GetComponent<RectTransform>().sizeDelta = new Vector2(1100, 850);
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

        static Text Text(string name, Transform parent, string value, Vector2 position, Vector2 size,
            int sizeFont, TextAnchor alignment, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = sizeFont;
            text.alignment = alignment;
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
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.pressedColor = new Color(.74f, .92f, 1f, 1f);
            colors.fadeDuration = .08f;
            button.colors = colors;
            Text("Etiqueta", go.transform, label, Vector2.zero, size - new Vector2(18, 8), 21,
                TextAnchor.MiddleCenter, Color.white);
            return button;
        }
    }
}

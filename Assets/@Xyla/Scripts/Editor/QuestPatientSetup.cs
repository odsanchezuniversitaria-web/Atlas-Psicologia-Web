using System;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Xyla;

namespace Xyla.Editor
{
    public static class QuestPatientSetup
    {
        static readonly Color Cyan = new Color(.18f, .84f, .95f);
        static readonly Color Navy = new Color(.012f, .045f, .071f, .97f);
        static Font font;
        static Material skin, hair, shirt, pants, dark, white, blush;

        [MenuItem("Atlas Humano Quest/Instalar paciente de psicología")]
        public static void Ensure()
        {
            var atlas = UnityEngine.Object.FindFirstObjectByType<QuestAtlasController>();
            if (!atlas) throw new InvalidOperationException("Abre HumanAtlasQuest para instalar la paciente.");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!atlas.patientSimulation)
            {
                var existing = UnityEngine.Object.FindFirstObjectByType<QuestPatientSimulation>(FindObjectsInactive.Include);
                atlas.patientSimulation = existing ? existing : CreatePatient();
                EditorUtility.SetDirty(atlas);
            }
            var patient = atlas.patientSimulation;
            QuestInterviewResultsSetup.Ensure(patient);
            QuestPatientTeachingSetup.Ensure(patient);
            if (!patient.avatar)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(patient.visualRoot);
                var recovered = patient.visualRoot.AddComponent<QuestPatientAvatar>();
                Transform visual = patient.visualRoot.transform;
                recovered.head = visual.Find("Cabeza · mirada y movimiento");
                recovered.torso = visual.Find("Torso · respiración");
                recovered.leftArm = visual.Find("Brazo izquierdo · gestos");
                recovered.rightArm = visual.Find("Brazo derecho · gestos");
                recovered.leftEye = recovered.head.Find("Ojo izquierdo · parpadeo");
                recovered.rightEye = recovered.head.Find("Ojo derecho · parpadeo");
                recovered.mouth = recovered.head.Find("Boca · habla");
                patient.avatar = recovered;
                EditorUtility.SetDirty(patient);
                EditorUtility.SetDirty(recovered);
            }
            foreach (var duplicate in patient.visualRoot.GetComponents<QuestPatientAvatar>())
                if (duplicate != patient.avatar) UnityEngine.Object.DestroyImmediate(duplicate);
            if (!patient.avatar.leftBrow || !patient.avatar.rightBrow)
            {
                LoadMaterials();
                Transform head = patient.avatar.head;
                patient.avatar.leftBrow = Primitive("Ceja izquierda · expresión", PrimitiveType.Capsule, head,
                    new Vector3(-.105f, .145f, .21f), new Vector3(.10f, .014f, .014f), hair).transform;
                patient.avatar.rightBrow = Primitive("Ceja derecha · expresión", PrimitiveType.Capsule, head,
                    new Vector3(.105f, .145f, .21f), new Vector3(.10f, .014f, .014f), hair).transform;
                EditorUtility.SetDirty(patient.avatar);
            }
            if (patient.caseBrief.StartsWith("Paciente adulta ficticia", StringComparison.Ordinal))
            {
                patient.caseBrief = "Caso educativo: María vive con trastorno de ansiedad generalizada. Explora su experiencia, el sueño y el impacto en su vida; escucha antes de proponer conclusiones.";
                EditorUtility.SetDirty(patient);
            }
            Transform page = atlas.psychologyPanel.missionPage.transform;
            if (!page.Find("Paciente IA"))
                Button("Paciente IA", page, "PACIENTE IA", new Vector2(0, -351), new Vector2(280, 50),
                    atlas.psychologyPanel.OpenPatient, true);
            Transform conversationPage = patient.interviewPage.transform;
            Transform symbol = conversationPage.Find("Indicador · turno de voz");
            if (!symbol)
                symbol = Text("Indicador · turno de voz", conversationPage, "●", new Vector2(-389, 96),
                    new Vector2(40, 40), 30, Cyan).transform;
            patient.conversationSymbol = symbol.GetComponent<Text>();
            EditorUtility.SetDirty(patient);
            if (patient.realisticDriver)
            {
                var driver = patient.realisticDriver;
                Transform[] rigBones = driver.GetComponentsInChildren<Transform>(true);
                if (!driver.leftForearmBone)
                    driver.leftForearmBone = Array.Find(rigBones, bone => bone.name == "LeftForeArm");
                if (!driver.rightForearmBone)
                    driver.rightForearmBone = Array.Find(rigBones, bone => bone.name == "RightForeArm");
                if (!driver.leftForearmBone || !driver.rightForearmBone)
                    throw new InvalidOperationException("Faltan los antebrazos del rig de María.");
                if (Mathf.Approximately(driver.headYawDegrees, 4f)) driver.headYawDegrees = 6f;
                if (Mathf.Approximately(driver.gestureDegrees, 7f)) driver.gestureDegrees = 12f;
                if (Mathf.Approximately(driver.breathingDegrees, 1.2f)) driver.breathingDegrees = 1.6f;
                EditorUtility.SetDirty(driver);
            }
            EditorSceneManager.MarkSceneDirty(atlas.gameObject.scene);
        }

        static QuestPatientSimulation CreatePatient()
        {
            LoadMaterials();
            var root = new GameObject("PACIENTE IA · María · caso editable", typeof(QuestPatientSimulation), typeof(QuestPatientVoice));
            var patient = root.GetComponent<QuestPatientSimulation>();
            patient.voice = root.GetComponent<QuestPatientVoice>();
            var visual = new GameObject("01 · Personaje 3D y animaciones", typeof(QuestPatientAvatar));
            visual.transform.SetParent(root.transform, false);
            patient.visualRoot = visual;
            patient.avatar = visual.GetComponent<QuestPatientAvatar>();
            BuildAvatar(patient.avatar);
            patient.panelRoot = BuildPanel(patient);
            visual.SetActive(false);
            patient.panelRoot.SetActive(false);
            return patient;
        }

        static void BuildAvatar(QuestPatientAvatar avatar)
        {
            Transform root = avatar.transform;
            Primitive("Asiento", PrimitiveType.Cube, root, new Vector3(0, .34f, .08f), new Vector3(.67f, .10f, .56f), dark);
            Primitive("Respaldo", PrimitiveType.Cube, root, new Vector3(0, .77f, .36f), new Vector3(.66f, .82f, .10f), dark);
            foreach (float x in new[] { -.25f, .25f })
                foreach (float z in new[] { -.17f, .29f })
                    Primitive("Pata silla", PrimitiveType.Cube, root, new Vector3(x, .16f, z), new Vector3(.055f, .32f, .055f), dark);
            avatar.torso = Primitive("Torso · respiración", PrimitiveType.Capsule, root,
                new Vector3(0, .83f, -.07f), new Vector3(.27f, .41f, .18f), shirt).transform;
            var neck = Primitive("Cuello", PrimitiveType.Cylinder, root,
                new Vector3(0, 1.22f, -.07f), new Vector3(.095f, .09f, .095f), skin);
            var head = new GameObject("Cabeza · mirada y movimiento");
            head.transform.SetParent(root, false);
            head.transform.localPosition = new Vector3(0, 1.45f, -.07f);
            avatar.head = head.transform;
            Primitive("Rostro", PrimitiveType.Sphere, head.transform, Vector3.zero, new Vector3(.47f, .56f, .43f), skin);
            Primitive("Cabello", PrimitiveType.Sphere, head.transform, new Vector3(0, .23f, -.04f), new Vector3(.51f, .27f, .47f), hair);
            Primitive("Cabello posterior", PrimitiveType.Sphere, head.transform, new Vector3(0, -.08f, -.19f), new Vector3(.50f, .48f, .24f), hair);
            avatar.leftEye = Primitive("Ojo izquierdo · parpadeo", PrimitiveType.Sphere, head.transform,
                new Vector3(-.105f, .06f, .205f), new Vector3(.038f, .022f, .022f), dark).transform;
            avatar.rightEye = Primitive("Ojo derecho · parpadeo", PrimitiveType.Sphere, head.transform,
                new Vector3(.105f, .06f, .205f), new Vector3(.038f, .022f, .022f), dark).transform;
            avatar.leftBrow = Primitive("Ceja izquierda · expresión", PrimitiveType.Capsule, head.transform,
                new Vector3(-.105f, .145f, .21f), new Vector3(.10f, .014f, .014f), hair).transform;
            avatar.rightBrow = Primitive("Ceja derecha · expresión", PrimitiveType.Capsule, head.transform,
                new Vector3(.105f, .145f, .21f), new Vector3(.10f, .014f, .014f), hair).transform;
            Primitive("Nariz", PrimitiveType.Sphere, head.transform,
                new Vector3(0, -.03f, .23f), new Vector3(.048f, .075f, .07f), skin);
            avatar.mouth = Primitive("Boca · habla", PrimitiveType.Sphere, head.transform,
                new Vector3(0, -.135f, .205f), new Vector3(.115f, .018f, .019f), blush).transform;
            var audio = head.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            avatar.leftArm = Arm("Brazo izquierdo · gestos", root, -.34f, shirt, skin);
            avatar.rightArm = Arm("Brazo derecho · gestos", root, .34f, shirt, skin);
            foreach (float x in new[] { -.16f, .16f })
            {
                var thigh = Primitive("Muslo", PrimitiveType.Capsule, root, new Vector3(x, .43f, .24f),
                    new Vector3(.14f, .29f, .14f), pants);
                thigh.transform.localRotation = Quaternion.Euler(75, 0, 0);
                Primitive("Pierna", PrimitiveType.Capsule, root, new Vector3(x, .20f, .46f),
                    new Vector3(.12f, .20f, .12f), pants);
                Primitive("Zapato", PrimitiveType.Sphere, root, new Vector3(x, .055f, .53f),
                    new Vector3(.17f, .09f, .27f), dark);
            }
        }

        static Transform Arm(string name, Transform root, float x, Material sleeve, Material skinMaterial)
        {
            var pivot = new GameObject(name);
            pivot.transform.SetParent(root, false);
            pivot.transform.localPosition = new Vector3(x, 1.10f, -.07f);
            Primitive("Manga", PrimitiveType.Capsule, pivot.transform,
                new Vector3(0, -.24f, 0), new Vector3(.115f, .29f, .115f), sleeve);
            Primitive("Mano", PrimitiveType.Sphere, pivot.transform,
                new Vector3(0, -.55f, .08f), new Vector3(.10f, .13f, .07f), skinMaterial);
            pivot.transform.localRotation = Quaternion.Euler(-20, 0, x < 0 ? -11 : 11);
            return pivot.transform;
        }

        static GameObject Primitive(string name, PrimitiveType shape, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            return part;
        }

        static GameObject BuildPanel(QuestPatientSimulation patient)
        {
            var go = new GameObject("02 · Panel de entrevista y retroalimentación", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PointableCanvas));
            go.transform.SetParent(patient.transform, false);
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 56;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(920, 780);
            go.transform.localScale = Vector3.one * .001f;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(920, 780, .1f);
            collider.isTrigger = true;
            var surface = go.AddComponent<Oculus.Interaction.Surfaces.ColliderSurface>();
            surface.InjectCollider(collider);
            var ray = go.AddComponent<RayInteractable>();
            ray.InjectSurface(surface);
            ray.InjectOptionalPointableElement(go.GetComponent<PointableCanvas>());
            go.GetComponent<PointableCanvas>().InjectCanvas(canvas);
            var bg = Image("Cristal clínico", go.transform, Vector2.zero, new Vector2(920, 780), Navy).transform;
            Image("Borde superior", bg, new Vector2(0, 387), new Vector2(920, 5), Cyan);
            Text("Marca", bg, "ATLAS · ENTREVISTA SIMULADA", new Vector2(0, 337), new Vector2(860, 58), 27, Cyan);
            patient.interviewPage = Page("01 · Conversación", bg);
            patient.feedbackPage = Page("02 · Retroalimentación", bg);
            Transform talk = patient.interviewPage.transform;
            patient.heading = Text("Paciente", talk, "MARÍA · ENTREVISTA SIMULADA", new Vector2(0, 255), new Vector2(830, 52), 31, Color.white);
            patient.briefLabel = Text("Objetivo de entrevista", talk, patient.caseBrief,
                new Vector2(0, 180), new Vector2(790, 105), 20, new Color(.72f, .83f, .88f));
            patient.statusLabel = Text("Estado de voz", talk, "Conectando…", new Vector2(0, 96), new Vector2(810, 40), 22, Cyan);
            patient.conversationSymbol = Text("Indicador · turno de voz", talk, "●", new Vector2(-389, 96),
                new Vector2(40, 40), 30, Cyan);
            Image("Separador", talk, new Vector2(0, 62), new Vector2(820, 2), new Color(.08f, .35f, .45f));
            patient.transcriptLabel = Text("Lo que dijiste", talk, "TÚ: Mantén A o X para hablar.",
                new Vector2(0, -5), new Vector2(790, 100), 23, Color.white);
            patient.patientLabel = Text("Respuesta de María", talk, "MARÍA: Hola.",
                new Vector2(0, -132), new Vector2(790, 150), 24, new Color(.9f, .95f, 1f));
            Button("Terminar entrevista", talk, "TERMINAR Y EVALUAR", new Vector2(0, -285), new Vector2(400, 58),
                patient.FinishInterview, true);
            Button("Volver a psicología", talk, "VOLVER A PSICOLOGÍA", new Vector2(0, -347), new Vector2(400, 50),
                patient.Close, false);

            Transform review = patient.feedbackPage.transform;
            patient.feedbackLabel = Text("Resultado de práctica", review, "Retroalimentación", new Vector2(0, 26),
                new Vector2(820, 505), 20, Color.white);
            Button("Repetir entrevista", review, "REPETIR ENTREVISTA", new Vector2(-214, -326),
                new Vector2(380, 55), patient.RestartInterview, true);
            Button("Cerrar entrevista", review, "VOLVER A PSICOLOGÍA", new Vector2(214, -326),
                new Vector2(380, 55), patient.Close, false);
            patient.interviewPage.SetActive(true);
            patient.feedbackPage.SetActive(false);
            return go;
        }

        static GameObject Page(string name, Transform parent)
        {
            var page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(parent, false);
            page.GetComponent<RectTransform>().sizeDelta = new Vector2(920, 780);
            return page;
        }

        static Image Image(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = go.GetComponent<Image>(); image.color = color;
            return image;
        }

        static Text Text(string name, Transform parent, string value, Vector2 position, Vector2 size, int sizeFont, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.font = font; text.fontSize = sizeFont; text.color = color;
            text.alignment = TextAnchor.MiddleCenter; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate; text.text = value; text.raycastTarget = false;
            return text;
        }

        static Button Button(string name, Transform parent, string label, Vector2 position, Vector2 size,
            UnityEngine.Events.UnityAction action, bool primary)
        {
            var image = Image(name, parent, position, size,
                primary ? new Color(.075f, .38f, .48f, .98f) : new Color(.035f, .095f, .14f, .97f));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None };
            UnityEventTools.AddPersistentListener(button.onClick, action);
            Text("Etiqueta", image.transform, label, Vector2.zero, size - new Vector2(15, 8), 21, Color.white);
            return button;
        }

        static void LoadMaterials()
        {
            const string folder = "Assets/@Xyla/Materials/Patient";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/@Xyla/Materials"))
                    AssetDatabase.CreateFolder("Assets/@Xyla", "Materials");
                AssetDatabase.CreateFolder("Assets/@Xyla/Materials", "Patient");
            }
            skin = Mat(folder, "Piel cálida", new Color(.60f, .39f, .28f));
            hair = Mat(folder, "Cabello", new Color(.13f, .085f, .065f));
            shirt = Mat(folder, "Ropa azul", new Color(.12f, .34f, .46f));
            pants = Mat(folder, "Pantalón", new Color(.16f, .22f, .29f));
            dark = Mat(folder, "Detalles oscuros", new Color(.045f, .065f, .08f));
            white = Mat(folder, "Blanco", new Color(.86f, .89f, .9f));
            blush = Mat(folder, "Boca", new Color(.30f, .10f, .10f));
        }

        static Material Mat(string folder, string name, Color color)
        {
            string path = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (!shader) throw new InvalidOperationException("Falta un shader para el paciente.");
            material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}

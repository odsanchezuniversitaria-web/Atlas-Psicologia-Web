using System;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Xyla
{
    public sealed class QuestAtlasMenu : MonoBehaviour
    {
        readonly List<Button> systemButtons = new List<Button>();
        readonly List<Text> systemLabels = new List<Text>();
        readonly List<Button> tabs = new List<Button>();
        readonly List<GameObject> pages = new List<GameObject>();
        QuestAtlasController controller;
        Font font;
        Text textureLabel, welcomeButtonLabel;
        Text passthroughLabel, effectsLabel, soundsLabel;
        Text voiceStatus, voiceContext, voiceTranscript, voiceAnswer, voiceMicLabel, voiceAskLabel, voicePageLabel, voiceFooter;
        Text learningStatus, learningProgress, dissectionLabel;
        readonly List<string> answerPages = new List<string>();
        int answerPage;
        string previousAnswer;
        string previousTranscript;
        int pressingPointer = int.MinValue;
        float nextPressTime;
        public int CurrentPage { get; private set; }

        public bool TryPress(int pointer)
        {
            if (pressingPointer != int.MinValue || Time.unscaledTime < nextPressTime) return false;
            pressingPointer = pointer;
            nextPressTime = Time.unscaledTime + 0.22f;
            controller.PlaySelectionSound();
            return true;
        }
        public void ReleasePress(int pointer)
        {
            if (pressingPointer == pointer) pressingPointer = int.MinValue;
        }
        void OnDisable() { pressingPointer = int.MinValue; }

        public static QuestAtlasMenu Create(QuestAtlasController owner)
        {
            var go = new GameObject("Menú principal · Atlas Humano", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if(!AtlasWebPlatform.Active)go.AddComponent<PointableCanvas>();
            var menu = go.AddComponent<QuestAtlasMenu>();
            menu.controller = owner;
            menu.Build();
            return menu;
        }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            canvas.worldCamera = Camera.main;
            RectTransform rect = GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(980, 820);
            transform.SetPositionAndRotation(new Vector3(1.02f, 1.36f, 1.2f), Quaternion.Euler(0, 20, 0));
            transform.localScale = Vector3.one * 0.00108f;
            GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(980, 820, 0.1f);
            collider.isTrigger = true;
            if(!AtlasWebPlatform.Active){
            var surface = gameObject.AddComponent<Oculus.Interaction.Surfaces.ColliderSurface>();
            surface.InjectCollider(collider);
            var ray = gameObject.AddComponent<RayInteractable>();
            ray.InjectSurface(surface);
            ray.InjectOptionalPointableElement(GetComponent<PointableCanvas>());
            GetComponent<PointableCanvas>().InjectCanvas(canvas);
            }
            EnsureEventSystem();

            var bg = CreateImage("Fondo clínico", transform, Vector2.zero, new Vector2(980,820), new Color(.012f,.026f,.045f,.99f)).transform;
            CreateImage("Borde superior",bg,new Vector2(0,407),new Vector2(980,6),Cyan);
            CreateImage("Borde inferior",bg,new Vector2(0,-407),new Vector2(980,4),new Color(.035f,.3f,.4f));
            CreateImage("Línea lateral",bg,new Vector2(-487,0),new Vector2(6,820),new Color(.035f,.3f,.4f));
            CreateText("Marca",bg,"ATLAS HUMANO",new Vector2(-80,355),new Vector2(740,62),42,TextAnchor.MiddleLeft,Color.white);
            CreateText("Subtítulo",bg,"LABORATORIO DE ANATOMÍA INMERSIVA",new Vector2(-80,310),new Vector2(740,36),20,TextAnchor.MiddleLeft,Muted);
            Cross(bg,new Vector2(410,348),58);
            string[] titles = { "INICIO", "SISTEMAS", "ASISTENTE", "APRENDER", "AYUDA", "ENTORNO" };
            for(int i=0;i<6;i++)
            {
                int page=i;
                tabs.Add(CreateButton("Pestaña "+titles[i],bg,titles[i],new Vector2(-377+i*151,253),new Vector2(143,57),()=>ShowPage(page)));
            }
            CreateImage("Separador",bg,new Vector2(0,207),new Vector2(900,2),new Color(.08f,.24f,.32f));
            Transform home=Page(bg,"Inicio");
            Transform systems=Page(bg,"Sistemas");
            Transform voice=Page(bg,"Asistente de anatomía");
            Transform learning=Page(bg,"Tutor, examen y disección");
            Transform help=Page(bg,"Guía de interacción");
            Transform environment=Page(bg,"Entorno y sonido");
            BuildHome(home);
            BuildSystems(systems);
            BuildVoice(voice);
            BuildLearning(learning);
            BuildHelp(help);
            BuildEnvironment(environment);
            controller.Voice.Changed += RefreshVoice;
            voiceFooter=CreateText("Pie",bg,"EXPLORACIÓN 3D  /  MANOS Y MANDOS  /  ALCANCE 3 m",new Vector2(0,-375),new Vector2(900,32),19,TextAnchor.MiddleCenter,Muted);
            ShowPage(0);
        }

        static Color Cyan => new Color(.16f,.86f,.91f);
        static Color Muted => new Color(.61f,.75f,.83f);
        Transform Page(Transform parent,string label)
        {
            var go=new GameObject(label,typeof(RectTransform));
            go.transform.SetParent(parent,false);
            go.GetComponent<RectTransform>().sizeDelta=new Vector2(980,820);
            pages.Add(go);
            return go.transform;
        }

        void BuildHome(Transform p)
        {
            CreateText("Bienvenida",p,"Hola, soy Atlas.",new Vector2(-90,159),new Vector2(710,65),42,TextAnchor.MiddleLeft,Color.white);
            CreateText("Guía de inicio",p,"1  Mantén pulsado A o X mientras hablas.\n2  Señala y pregunta: «¿Qué es eso?».\n3  Pide: «Muestra el sistema óseo» y suelta.",new Vector2(0,47),new Vector2(880,138),28,TextAnchor.MiddleLeft,Muted);
            Cross(p,new Vector2(373,150),68);
            var start=CreateButton("Conversar con Atlas",p,"ASISTENTE / VOZ",new Vector2(-228,-74),new Vector2(434,66),()=>{if(!controller.Voice.Listening)controller.Voice.ToggleListening();ShowPage(2);});
            start.image.color=new Color(.025f,.38f,.45f);
            welcomeButtonLabel=CreateButton("Bienvenida hablada",p,"REPETIR BIENVENIDA",new Vector2(228,-74),new Vector2(434,66),controller.Voice.ToggleWelcome).GetComponentInChildren<Text>();
            CreateButton("Explorar anatomía",p,"EXPLORAR LOS SISTEMAS",new Vector2(0,-160),new Vector2(892,66),()=>ShowPage(1));
            CreateText("Acabado",p,"SUPERFICIE ÓSEA",new Vector2(-232,-252),new Vector2(420,48),24,TextAnchor.MiddleLeft,Cyan);
            var toggle=CreateButton("Comparar textura",p,"",new Vector2(230,-252),new Vector2(430,60),controller.ToggleBoneTexture);
            textureLabel=toggle.GetComponentInChildren<Text>();
            CreateText("Consejo",p,"Puedes decir activar, mostrar, encender u ocultar.\nNo necesitas conocer los nombres técnicos.",new Vector2(0,-326),new Vector2(882,60),21,TextAnchor.MiddleCenter,Muted);
        }

        void Stat(Transform p,float x,string value,string label)
        {
            CreateImage("Dato "+label,p,new Vector2(x,-15),new Vector2(280,102),new Color(.022f,.062f,.09f));
            CreateText(label,p,value,new Vector2(x,3),new Vector2(250,44),34,TextAnchor.MiddleCenter,Cyan);
            CreateText("Etiqueta "+label,p,label,new Vector2(x,-35),new Vector2(250,28),19,TextAnchor.MiddleCenter,Muted);
        }

        void Cross(Transform parent,Vector2 center,float size)
        {
            CreateImage("Símbolo médico horizontal",parent,center,new Vector2(size,size*.28f),Cyan);
            CreateImage("Símbolo médico vertical",parent,center,new Vector2(size*.28f,size),Cyan);
        }

        void BuildSystems(Transform p)
        {
            for(int i=0;i<AtlasSystems.Ids.Length;i++)
            {
                int index=i;
                var b=CreateButton("Sistema "+AtlasSystems.Names[i],p,AtlasSystems.Names[i],
                    new Vector2(-304+(i%3)*304,155-(i/3)*76),new Vector2(286,62),()=>controller.ToggleSystem(index));
                systemButtons.Add(b);
                systemLabels.Add(b.GetComponentInChildren<Text>());
            }
            string[] labels={"SOLO ACTIVO","VER TODOS","OCULTAR","REINICIAR"};
            UnityEngine.Events.UnityAction[] actions={controller.ShowOnlyActiveSystem,controller.ShowAllSystems,controller.HideAllSystems,controller.ResetAtlas};
            for(int i=0;i<4;i++)
                CreateButton(labels[i],p,labels[i],new Vector2(-342+i*228,-246),new Vector2(214,61),actions[i]);
            CreateText("Ayuda de sistemas",p,"Cada sistema se activa por separado.\nEl último que activas queda disponible para agarrar.",new Vector2(0,-314),new Vector2(900,58),21,TextAnchor.MiddleCenter,Muted);
        }


        void BuildVoice(Transform p)
        {
            voiceStatus=CreateText("Estado de voz",p,"",new Vector2(0,168),new Vector2(880,44),26,TextAnchor.MiddleLeft,Cyan);
            voiceContext=CreateText("Pieza de consulta",p,"",new Vector2(0,117),new Vector2(880,52),21,TextAnchor.MiddleLeft,Muted);
            var mic=CreateButton("Activar o silenciar voz",p,"ACTIVAR VOZ",new Vector2(-228,51),new Vector2(432,58),controller.Voice.ToggleListening);
            voiceMicLabel=mic.GetComponentInChildren<Text>();
            voiceAskLabel=CreateButton("Preguntar o detener",p,"PREGUNTAR AHORA",new Vector2(228,51),new Vector2(432,58),()=>{if(controller.Voice.Speaking)controller.Voice.StopSpeaking();else controller.Voice.AskNow();}).GetComponentInChildren<Text>();
            voiceTranscript=CreateText("Pregunta reconocida",p,"",new Vector2(0,-26),new Vector2(880,68),22,TextAnchor.UpperLeft,Muted);
            CreateImage("Tarjeta respuesta",p,new Vector2(0,-170),new Vector2(896,206),new Color(.025f,.063f,.09f));
            voiceAnswer=CreateText("Respuesta de Atlas",p,"",new Vector2(0,-171),new Vector2(848,178),24,TextAnchor.UpperLeft,Color.white);
            CreateButton("Respuesta anterior",p,"<",new Vector2(-373,-309),new Vector2(140,48),()=>ChangeAnswerPage(-1));
            voicePageLabel=CreateText("Página de respuesta",p,"",new Vector2(0,-309),new Vector2(560,44),20,TextAnchor.MiddleCenter,Muted);
            CreateButton("Respuesta siguiente",p,">",new Vector2(373,-309),new Vector2(140,48),()=>ChangeAnswerPage(1));
            RefreshVoice();
        }

        void RefreshVoice()
        {
            if(!voiceStatus) return;
            var agent=controller.Voice;
            if(welcomeButtonLabel)welcomeButtonLabel.text=agent.IsWelcoming?"OMITIR BIENVENIDA":"REPETIR BIENVENIDA";
            if(voiceFooter) voiceFooter.text=agent.Listening ? "VOZ LISTA  ·  MANTÉN A O X PARA HABLAR" : "EXPLORACIÓN 3D  /  MANOS Y MANDOS  /  ALCANCE 3 m";
            voiceStatus.text=agent.Status;
            voiceStatus.fontSize=agent.Status.Length>62?21:26;
            voiceContext.text="CONTEXTO: "+agent.ContextLabel;
            voiceMicLabel.text=agent.Listening||agent.Busy?"SILENCIAR / CANCELAR":"ACTIVAR VOZ";
            if(voiceAskLabel)voiceAskLabel.text=agent.Speaking?"DETENER RESPUESTA":"PREGUNTAR AHORA";
            string transcript=agent.Transcript;
            voiceTranscript.text=string.IsNullOrEmpty(transcript)?"Mantén A o X, haz tu pregunta y suelta el botón.": "TÚ: "+(transcript.Length>170?transcript.Substring(0,167)+"…":transcript);
            if(previousAnswer!=agent.Answer || previousTranscript!=transcript)
            {
                previousAnswer=agent.Answer; previousTranscript=transcript;
                answerPages.Clear();
                string page="";
                foreach(string word in agent.Answer.Split(' '))
                {
                    string candidate=page.Length==0?word:page+" "+word;
                    var settings=voiceAnswer.GetGenerationSettings(voiceAnswer.rectTransform.rect.size);
                    float height=voiceAnswer.cachedTextGeneratorForLayout.GetPreferredHeight(candidate,settings)/voiceAnswer.pixelsPerUnit;
                    if(height>voiceAnswer.rectTransform.rect.height && page.Length>0) {answerPages.Add(page);page=word;} else page=candidate;
                }
                if(page.Length>0)answerPages.Add(page);
                answerPage=0;
            }
            RenderAnswerPage();
        }

        public void ChangeAnswerPage(int delta)
        {
            answerPage=Mathf.Clamp(answerPage+delta,0,Mathf.Max(0,answerPages.Count-1));
            RenderAnswerPage();
        }

        void RenderAnswerPage()
        {
            voiceAnswer.text=answerPages.Count>0?answerPages[answerPage]:"";
            voicePageLabel.text="VOZ GENERADA POR IA  ·  "+(answerPage+1)+"/"+Mathf.Max(1,answerPages.Count);
        }

        void OnDestroy()
        {
            if(controller && controller.Voice) controller.Voice.Changed-=RefreshVoice;
        }

void BuildLearning(Transform p)
        {
            learningStatus=CreateText("Estado de aprendizaje",p,"",new Vector2(0,166),new Vector2(890,58),27,TextAnchor.MiddleLeft,Cyan);
            learningProgress=CreateText("Progreso",p,"",new Vector2(0,116),new Vector2(890,40),21,TextAnchor.MiddleLeft,Muted);

            CreateButton("Tutor guiado",p,GameTitle("tutor","TUTOR GUIADO"),new Vector2(-228,52),new Vector2(434,54),()=>controller.Learning.StartLesson(new[]{"skeletal"},Array.Empty<string>()));
            CreateButton("Siguiente explicación",p,"SIGUIENTE LECCIÓN",new Vector2(228,52),new Vector2(434,54),()=>controller.Learning.NextLesson());

            CreateButton("Búsqueda clásica",p,GameTitle("busqueda","BÚSQUEDA DE PIEZAS"),new Vector2(-228,-14),new Vector2(434,54),()=>controller.Learning.StartExam(new[]{"skeletal"},Array.Empty<string>()));
            CreateButton("Pista",p,"PEDIR PISTA",new Vector2(228,-14),new Vector2(434,54),()=>controller.Learning.Hint());

            CreateButton("Contrarreloj",p,GameTitle("contrarreloj","CONTRARRELOJ"),new Vector2(-228,-80),new Vector2(434,54),()=>controller.Learning.StartTimeAttack());
            CreateButton("Detective",p,GameTitle("detective","DETECTIVE ANATÓMICO"),new Vector2(228,-80),new Vector2(434,54),()=>controller.Learning.StartDetective());

            CreateButton("Supervivencia",p,GameTitle("supervivencia","SUPERVIVENCIA"),new Vector2(-228,-146),new Vector2(434,54),()=>controller.Learning.StartSurvival());
            var dissection=CreateButton("Modo disección",p,GameTitle("diseccion","DISECCIÓN LIBRE"),new Vector2(228,-146),new Vector2(434,54),()=>controller.Learning.ToggleDissection());
            dissectionLabel=dissection.GetComponentInChildren<Text>();

            CreateButton("Detener juego",p,"DETENER RETO",new Vector2(-228,-212),new Vector2(434,54),()=>controller.Learning.StopGame());
            CreateButton("Reconstruir",p,"RECONSTRUIR MODELO",new Vector2(228,-212),new Vector2(434,54),()=>controller.Learning.Rebuild());

            CreateText("Ayuda juegos",p,"Selecciona directamente la estructura pedida. En contrarreloj, un acierto suma 2,5 s y un error resta 3 s. Detective muestra solo una pista. Supervivencia termina al perder tres vidas.",new Vector2(0,-298),new Vector2(890,92),20,TextAnchor.MiddleCenter,Muted);
        }

        void BuildEnvironment(Transform p)
        {
            CreateText("Título de entorno",p,"TU LABORATORIO, EN TU ESPACIO",new Vector2(0,159),new Vector2(880,50),27,TextAnchor.MiddleLeft,Cyan);
            passthroughLabel=CreateButton("Passthrough",p,"",new Vector2(0,75),new Vector2(890,67),()=>{controller.RecordUndo("cambiar passthrough");controller.Environment.SetPassthrough(!controller.Environment.PassthroughEnabled);Refresh();}).GetComponentInChildren<Text>();
            effectsLabel=CreateButton("Efectos",p,"",new Vector2(-228,-12),new Vector2(434,67),()=>{controller.RecordUndo("cambiar efectos");controller.Environment.SetEffects(!controller.Environment.EffectsEnabled);Refresh();}).GetComponentInChildren<Text>();
            soundsLabel=CreateButton("Sonidos",p,"",new Vector2(228,-12),new Vector2(434,67),()=>{controller.RecordUndo("cambiar sonidos");controller.Environment.SetSounds(!controller.Environment.SoundsEnabled);Refresh();}).GetComponentInChildren<Text>();
            CreateButton("Voz menos",p,"VOZ  −",new Vector2(-228,-98),new Vector2(434,60),()=>controller.ControlApp("voice_quieter",0));
            CreateButton("Voz más",p,"VOZ  +",new Vector2(228,-98),new Vector2(434,60),()=>controller.ControlApp("voice_louder",0));
            CreateText("Órdenes de control",p,"«Atlas, oculta todo menos el corazón»\n«Atlas, abre sistemas»\n«Atlas, devuelve todas las piezas»\n«Atlas, desactiva los efectos»",new Vector2(0,-227),new Vector2(864,166),24,TextAnchor.MiddleLeft,Color.white);
        }

        void BuildHelp(Transform p)
        {
            CreateImage("Tarjeta manos",p,new Vector2(-230,2),new Vector2(432,360),new Color(.025f,.063f,.09f));
            CreateImage("Tarjeta mandos",p,new Vector2(230,2),new Vector2(432,360),new Color(.025f,.063f,.09f));
            CreateText("Título manos",p,"MANOS REALES",new Vector2(-230,143),new Vector2(390,42),26,TextAnchor.MiddleLeft,Cyan);
            CreateText("Instrucciones manos",p,"Apunta con la mano.\n\nJunta índice y pulgar para seleccionar o atraer una pieza.\n\nMantén el pellizco para moverla y suelta para dejarla.",new Vector2(-230,-12),new Vector2(384,244),24,TextAnchor.UpperLeft,Color.white);
            CreateText("Título mandos",p,"CON MANDOS",new Vector2(230,143),new Vector2(390,42),26,TextAnchor.MiddleLeft,Cyan);
            CreateText("Instrucciones mandos",p,"Gatillo: pulsar los paneles.\nGrab lateral: atraer y sujetar.\n\nMantén A o X: hablar con Atlas.\n\nPalanca izquierda: caminar.\nPalanca derecha: girar.\nBotón menú: volver a inicio.",new Vector2(230,-12),new Vector2(384,244),24,TextAnchor.UpperLeft,Color.white);
            CreateText("Escala y color",p,"DOS MANOS PARA CAMBIAR EL TAMAÑO\nCian: estás apuntando. Ámbar: estás sujetando.",new Vector2(0,-243),new Vector2(882,75),24,TextAnchor.MiddleCenter,Muted);
            CreateButton("Volver a explorar",p,"EXPLORAR SISTEMAS",new Vector2(0,-320),new Vector2(892,58),()=>ShowPage(1));
        }

        public void ShowPage(int page)
        {
            if(page<0 || page>=pages.Count) throw new ArgumentOutOfRangeException(nameof(page));
            CurrentPage=page;
            for(int i=0;i<pages.Count;i++)
            {
                pages[i].SetActive(i==page);
                tabs[i].image.color=i==page?new Color(.025f,.32f,.4f):new Color(.026f,.07f,.105f);
            }
            Refresh();
        }

        static void EnsureEventSystem()
        {
            if(AtlasWebPlatform.Active)return;
            EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventObject = new GameObject(
                    "Eventos de interacción Meta XR",
                    typeof(EventSystem),
                    typeof(PointableCanvasModule));
                eventSystem = eventObject.GetComponent<EventSystem>();
            }
            else if (eventSystem.GetComponent<PointableCanvasModule>() == null)
            {
                eventSystem.gameObject.AddComponent<PointableCanvasModule>();
            }
        }

        public void Refresh()
        {
            if (controller == null || systemButtons.Count != AtlasSystems.Ids.Length)
                return;

            if(learningStatus)
            {
                learningStatus.text=controller.Learning.Status;
                learningProgress.text=controller.Learning.Progress+(controller.CanUndo?"  ·  deshacer disponible":"");
                if(dissectionLabel)dissectionLabel.text=controller.Learning.DissectionActive?"DETENER DISECCIÓN":"INICIAR DISECCIÓN";
            }
            if(passthroughLabel) passthroughLabel.text=controller.Environment.PassthroughEnabled?"PASSTHROUGH: ENTORNO REAL":"FONDO VIRTUAL";
            if(effectsLabel) effectsLabel.text=controller.Environment.EffectsEnabled?"EFECTOS: ACTIVOS":"EFECTOS: APAGADOS";
            if(soundsLabel) soundsLabel.text=controller.Environment.SoundsEnabled?"SONIDOS: ACTIVOS":"SONIDOS: APAGADOS";
            textureLabel.text = controller.BoneTextureEnabled ? "ACABADO: MEJORADO" : "ACABADO: ORIGINAL";
            for (int i = 0; i < systemButtons.Count; i++)
            {
                bool visible = controller.IsSystemVisible(i);
                bool active = controller.IsActiveSystem(i);
                Color systemColor = AtlasSystems.ColorFor(AtlasSystems.Ids[i]);
                systemButtons[i].image.color = visible
                    ? Color.Lerp(systemColor, new Color(0.06f, 0.18f, 0.24f), 0.36f)
                    : new Color(0.035f, 0.085f, 0.12f, 0.96f);
                systemLabels[i].text =
                    (visible ? "✓  " : "○  ") +
                    AtlasSystems.Names[i];
                systemLabels[i].color = visible ? Color.white : new Color(0.66f, 0.76f, 0.82f);
            }
        }

        Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 position,
            Vector2 size,
            UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(QuestAtlasButton));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.035f, 0.085f, 0.12f, 0.96f);

            Button button = buttonObject.GetComponent<Button>();
            ((QuestAtlasButton)button).Menu = this;
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.74f, 0.92f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Text text = CreateText(
                "Texto",
                buttonObject.transform,
                label,
                Vector2.zero,
                size - new Vector2(18f, 10f),
                22,
                TextAnchor.MiddleCenter,
                Color.white);
            text.raycastTarget = false;
            return button;
        }

        Text CreateText(
            string name,
            Transform parent,
            string value,
            Vector2 position,
            Vector2 size,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        static Image CreateImage(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    

string GameTitle(string id,string fallback)
        {
            var settings=FindFirstObjectByType<QuestAtlasGameConfiguration>();
            var mode=settings?settings.Find(id):null;
            return mode!=null&&mode.habilitado&&!string.IsNullOrWhiteSpace(mode.titulo)?mode.titulo.ToUpperInvariant():fallback;
        }
}
}

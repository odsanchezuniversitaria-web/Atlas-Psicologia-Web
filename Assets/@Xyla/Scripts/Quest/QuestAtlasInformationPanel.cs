using UnityEngine;
using UnityEngine.UI;

namespace Xyla
{
    public sealed class QuestAtlasInformationPanel : MonoBehaviour
    {
        Text title, classification, description, function, functionHeading, scope;
        Image accent, background;
        Transform head;
        CanvasGroup hologram;
        AtlasDottedPointer pointer;
        bool anchored;
        Font font;

        public string SelectedPartId { get; private set; }
        public string DisplayedName => title.text;
        public string DisplayedDescription => description.text;
        public string DisplayedFunction => function.text;
        public string DisplayedKind => classification.text;

        public static QuestAtlasInformationPanel Create()
        {
            var root = new GameObject("Ficha de la pieza seleccionada", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var panel = root.AddComponent<QuestAtlasInformationPanel>();
            panel.Build();
            return panel;
        }

void Build()
        {
            if (QuestAtlasInformation.Count == 0)
                throw new System.InvalidOperationException("El catálogo de fichas anatómicas está vacío.");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 15;
            head = canvas.worldCamera.transform;
            hologram = gameObject.AddComponent<CanvasGroup>();
            GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            GetComponent<RectTransform>().sizeDelta = new Vector2(780f, 860f);
            transform.SetPositionAndRotation(new Vector3(-0.98f, 1.36f, 1.05f), Quaternion.Euler(0f, -15f, 0f));
            transform.localScale = Vector3.one * 0.00105f;

            background = MakeImage("Cristal holográfico", 0f, 0f, 780f, 860f, new Color(0.005f, 0.045f, 0.075f, 0.86f));
            MakeImage("Brillo interior", 10f, 10f, 760f, 840f, new Color(0.02f, 0.20f, 0.27f, 0.12f));
            MakeImage("Borde superior", 0f, 0f, 780f, 3f, new Color(0.12f, 0.90f, 1f, 0.82f));
            MakeImage("Borde inferior", 0f, 857f, 780f, 3f, new Color(0.12f, 0.90f, 1f, 0.55f));
            MakeImage("Borde izquierdo", 0f, 0f, 3f, 860f, new Color(0.12f, 0.90f, 1f, 0.66f));
            MakeImage("Borde derecho", 777f, 0f, 3f, 860f, new Color(0.12f, 0.90f, 1f, 0.66f));
            for (int i = 0; i < 7; i++)
                MakeImage("Línea holográfica " + (i + 1), 16f, 116f + i * 104f, 748f, 1f, new Color(0.12f, 0.75f, 0.92f, 0.075f));
            accent = MakeImage("Acento luminoso", 0f, 0f, 780f, 8f, new Color(0.10f, 0.78f, 0.94f));
            MakeImage("Esquina A", 14f, 16f, 80f, 3f, new Color(0.25f, 0.95f, 1f, 0.9f));
            MakeImage("Esquina B", 14f, 16f, 3f, 54f, new Color(0.25f, 0.95f, 1f, 0.9f));
            MakeImage("Esquina C", 686f, 16f, 80f, 3f, new Color(0.25f, 0.95f, 1f, 0.9f));
            MakeImage("Esquina D", 763f, 16f, 3f, 54f, new Color(0.25f, 0.95f, 1f, 0.9f));
            MakeText("Encabezado", "FICHA ANATÓMICA  ·  ENLACE HOLOGRÁFICO", 28f, 34f, 22, new Color(0.45f, 0.92f, 1f));
            title = MakeText("Nombre de la pieza", "", 79f, 114f, 40, Color.white);
            title.fontStyle = FontStyle.Bold;
            classification = MakeText("Tipo y grupo", "", 206f, 64f, 23, new Color(0.68f, 0.88f, 0.94f));
            MakeImage("Separador superior", 34f, 283f, 712f, 2f, new Color(0.12f, 0.60f, 0.74f, 0.58f));
            MakeText("Título de descripción", "QUÉ ES", 303f, 30f, 22, new Color(0.45f, 0.92f, 1f));
            description = MakeText("Descripción", "", 343f, 114f, 29, new Color(0.94f, 0.98f, 1f));
            functionHeading = MakeText("Título de función", "FUNCIÓN", 475f, 30f, 22, new Color(0.45f, 0.92f, 1f));
            function = MakeText("Función", "", 515f, 136f, 29, new Color(0.94f, 0.98f, 1f));
            scope = MakeText("Alcance de la ficha", "", 664f, 45f, 20, new Color(0.66f, 0.84f, 0.91f));
            MakeImage("Separador inferior", 34f, 722f, 712f, 2f, new Color(0.12f, 0.60f, 0.74f, 0.52f));
            MakeText("Ayuda breve", "Grab lateral: atraer y sujetar · Gatillo: panel\nDos manos: escalar · Menú: restablecer", 740f, 57f, 21, new Color(0.76f, 0.91f, 0.96f));
            MakeText("Referencia", "Referencia anatómica: OpenStax · Anatomy and Physiology 2e", 814f, 26f, 17, new Color(0.55f, 0.76f, 0.84f));
            pointer = gameObject.AddComponent<AtlasDottedPointer>();
            pointer.Initialize(transform, head, new Vector3(390f, 0f, 0f));
            ClearSelection();
        }

public void ShowPart(AtlasPart part, Transform targetTransform = null)
        {
            QuestAtlasInformation.Definition info = QuestAtlasInformation.Get(part.name);
            SelectedPartId = part.id;
            SetTitle(AtlasSpanish.Name(part.name));
            classification.text = info.kind + "\nGrupo del atlas: " + AtlasSystems.Name(part.system);
            description.text = info.description;
            function.text = info.function;
            functionHeading.text = info.general ? "FUNCIÓN GENERAL" : "FUNCIÓN";
            scope.text = info.general
                ? "Esta ficha resume el tipo de estructura indicado."
                : "Panel anclado · el enlace punteado marca la estructura seleccionada.";
            accent.color = AtlasSystems.ColorFor(part.system);
            PlaceForReading();
            pointer.SetTarget(targetTransform, AtlasSystems.ColorFor(part.system));
        }

public void ClearSelection()
        {
            SelectedPartId = null;
            SetTitle("Selecciona una pieza");
            classification.text = "Explora el atlas humano";
            description.text = "Apunta a una pieza y mantén Grab lateral para acercarla. También puedes agarrarla directamente con la mano.";
            functionHeading.text = "INFORMACIÓN DE LA PIEZA";
            function.text = "Aquí aparecerán su nombre, el tipo de estructura y una explicación de su función.";
            scope.text = "El panel quedará fijo y un enlace punteado señalará la pieza.";
            accent.color = new Color(0.10f, 0.78f, 0.94f);
            if (pointer) pointer.ClearTarget();
        }

void PlaceForReading()
        {
            if (anchored || !UnityEngine.XR.XRSettings.isDeviceActive) return;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) return;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 position = head.position + forward * 1.25f - right * 0.94f - Vector3.up * 0.08f;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - head.position, Vector3.up));
            anchored = true;
        }

void Update(){if(hologram)hologram.alpha=0.945f+Mathf.Sin(Time.unscaledTime*2.4f)*0.025f;if(background)background.color=new Color(0.005f,0.045f+Mathf.Sin(Time.unscaledTime*1.7f)*0.004f,0.075f,0.86f);}


        void SetTitle(string value)
        {
            title.text = value;
            title.fontSize = 40;
            if (title.preferredHeight > title.rectTransform.rect.height) title.fontSize = 32;
            if (title.preferredHeight > title.rectTransform.rect.height) title.fontSize = 26;
        }

        RectTransform MakeRect(string name, float x, float top, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -top);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        Text MakeText(string name, string value, float top, float height, int size, Color color)
        {
            var text = MakeRect(name, 34f, top, 712f, height).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.color = color;
            text.text = value;
            return text;
        }

        Image MakeImage(string name, float x, float top, float width, float height, Color color)
        {
            var image = MakeRect(name, x, top, width, height).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}

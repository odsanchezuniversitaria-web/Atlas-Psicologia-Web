using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Xyla;

namespace Xyla.Editor
{
    public static class QuestInterviewResultsSetup
    {
        [MenuItem("Atlas Humano Quest/Actualizar resultados de entrevista")]
        public static void Install()
        {
            var patient = Object.FindFirstObjectByType<QuestPatientSimulation>(FindObjectsInactive.Include);
            if (!patient) throw new System.InvalidOperationException("Abre la escena del Atlas.");
            Ensure(patient);
            EditorSceneManager.MarkSceneDirty(patient.gameObject.scene);
            EditorSceneManager.SaveScene(patient.gameObject.scene);
        }

        public static void Ensure(QuestPatientSimulation patient)
        {
            if (patient.resultsDashboard) return;
            Transform parent = patient.feedbackPage.transform;
            var root = new GameObject("03 · Resultados de entrevista · editable", typeof(RectTransform), typeof(QuestInterviewResults));
            root.transform.SetParent(parent, false);
            var view = root.GetComponent<QuestInterviewResults>();
            patient.resultsDashboard = view;
            patient.feedbackLabel.gameObject.SetActive(false);
            view.durationLabel = Label("Tiempo e intervenciones", root.transform, new Vector2(0,270), new Vector2(830,40), 23);
            view.durationLabel.text = "RESULTADOS DE LA ENTREVISTA";
            view.scoreLabel = Label("Puntuación de cobertura", root.transform, new Vector2(0,110), new Vector2(180,100), 44);
            view.scoreLabel.text = "0.0";
            view.basisLabel = Label("Criterio de puntuación", root.transform, new Vector2(0,-45), new Vector2(830,45), 18);
            view.detailLabel = Label("Evidencia y recomendación", root.transform, new Vector2(0,-190), new Vector2(800,205), 22);
            view.detailLabel.alignment = TextAnchor.UpperLeft;
            view.detailLabel.text = "Selecciona una habilidad para explorar sus resultados.";
            view.criterionLabels = new Text[6]; view.criterionBars = new Image[6];
            view.criterionButtons = new Button[6]; view.ringSegments = new Image[6];
            Sprite ring = Ring();
            string[] names = {"Inicio y permiso","Pregunta abierta","Preocupación y control","Duración e impacto","Validación","Cierre colaborativo"};
            for (int i=0;i<6;i++)
            {
                var segment = Box("Anillo · " + names[i], root.transform, new Vector2(0,110), new Vector2(235,235), new Color(.2f,.85f,1f));
                segment.sprite = ring; segment.type = Image.Type.Filled; segment.fillMethod = Image.FillMethod.Radial360;
                segment.fillOrigin = 2; segment.fillAmount = 1f/6f - .012f;
                segment.transform.localRotation = Quaternion.Euler(0,0,-i*60);
                view.ringSegments[i] = segment;
                Vector2 pos = new Vector2(i<3 ? -290 : 290, 205-(i%3)*95);
                var card = Box("Tarjeta · " + names[i], root.transform, pos, new Vector2(300,84), new Color(.035f,.08f,.15f,.98f));
                var outline = card.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.6f,.25f,1f); outline.effectDistance = new Vector2(2,-2);
                var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = card;
                button.navigation = new Navigation {mode = Navigation.Mode.None};
                card.raycastTarget = true;
                view.criterionButtons[i] = button;
                view.criterionLabels[i] = Label("Título y estado", card.transform, new Vector2(0,8), new Vector2(280,60), 19);
                view.criterionLabels[i].text = names[i];
                view.criterionBars[i] = Box("Estado", card.transform, new Vector2(0,-31), new Vector2(265,5), Color.cyan);
            }
            EditorUtility.SetDirty(patient); EditorUtility.SetDirty(view);
        }

        static Image Box(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>(); rect.anchoredPosition=pos; rect.sizeDelta=size;
            var image=go.GetComponent<Image>(); image.color=color; image.raycastTarget=false; return image;
        }
        static Text Label(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>(); rect.anchoredPosition=pos; rect.sizeDelta=size;
            var text=go.GetComponent<Text>(); text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize=fontSize; text.color=Color.white; text.alignment=TextAnchor.MiddleCenter; text.raycastTarget=false;
            return text;
        }
        static Sprite Ring()
        {
            const string path="Assets/@Xyla/Materials/InterviewResultsRing.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Sprite>(path); if(saved) return saved;
            var texture=new Texture2D(256,256,TextureFormat.RGBA32,false);
            for(int y=0;y<256;y++) for(int x=0;x<256;x++)
            {
                float radius=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(128,128));
                float alpha=Mathf.Clamp01(121-radius)*Mathf.Clamp01(radius-91);
                texture.SetPixel(x,y,new Color(1,1,1,alpha));
            }
            texture.Apply(); texture.name="Anillo"; texture.wrapMode=TextureWrapMode.Clamp;
            var sprite=Sprite.Create(texture,new Rect(0,0,256,256),new Vector2(.5f,.5f));
            sprite.name="Anillo de resultados"; AssetDatabase.CreateAsset(sprite,path);
            AssetDatabase.AddObjectToAsset(texture,sprite); AssetDatabase.SaveAssets(); return sprite;
        }
    }
}
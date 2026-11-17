using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace Xyla.Editor {
public static class QuestEmotionGaugeSetup {
 [MenuItem("Atlas Humano Quest/Psicología/Instalar medidor junto a la paciente")]
 public static void Install() {
  var p=Object.FindFirstObjectByType<QuestPatientSimulation>(FindObjectsInactive.Include);
  if(!p)throw new System.InvalidOperationException("Abre HumanAtlasQuest.");
  Ensure(p);EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
 }
 public static void Ensure(QuestPatientSimulation p) {
  var t=p.teaching;
  if(!t.emotionGauge)Create(p);
  t.emotionLabel.gameObject.SetActive(false);
  t.comfortFill.transform.parent.gameObject.SetActive(false);
  t.signalLabel.rectTransform.anchoredPosition=new Vector2(0,-177);
  t.signalLabel.rectTransform.sizeDelta=new Vector2(790,48);
  t.signalLabel.fontSize=15;
  t.signalLabel.resizeTextForBestFit=true;t.signalLabel.resizeTextMinSize=13;t.signalLabel.resizeTextMaxSize=15;
  t.adviceLabel.resizeTextForBestFit=true;t.adviceLabel.resizeTextMinSize=15;t.adviceLabel.resizeTextMaxSize=17;
  var atlas=Object.FindFirstObjectByType<QuestAtlasController>(FindObjectsInactive.Include);
  if(!atlas.clinicalPresentation) {
   var existing=atlas.GetComponent<QuestAtlasClinicalPresentation>();
   atlas.clinicalPresentation=existing?existing:atlas.gameObject.AddComponent<QuestAtlasClinicalPresentation>();
  }
  foreach(var graphic in t.emotionGauge.GetComponentsInChildren<Graphic>(true)) {
   if(!graphic.TryGetComponent<CanvasRenderer>(out var renderer))graphic.gameObject.AddComponent<CanvasRenderer>();
   graphic.SetAllDirty();
  }
  t.emotionGauge.Hide();
  EditorUtility.SetDirty(t);EditorUtility.SetDirty(atlas);
  EditorSceneManager.MarkSceneDirty(p.gameObject.scene);
 }
 static RectTransform Rect(Transform parent,string name,Vector2 pos,Vector2 size) {
  var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
  var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;return r;
 }
 static Text Label(Transform parent,string name,string text,Vector2 pos,Vector2 size,int fontSize,Font font) {
  var r=Rect(parent,name,pos,size);var label=r.gameObject.AddComponent<Text>();label.font=font;label.text=text;
  label.fontSize=fontSize;label.color=new Color(.90f,.97f,1);label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;return label;
 }
 static void Create(QuestPatientSimulation p) {
  var root=new GameObject("05 · Medidor emocional junto a María · editable",typeof(RectTransform),typeof(Canvas),typeof(QuestPatientEmotionGauge));
  root.transform.SetParent(p.transform,false);root.GetComponent<RectTransform>().sizeDelta=new Vector2(480,390);
  root.transform.localScale=Vector3.one*.00105f;
  var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=Camera.main;canvas.sortingOrder=40;
  var gauge=root.GetComponent<QuestPatientEmotionGauge>();p.teaching.emotionGauge=gauge;
  var backdrop=Rect(root.transform,"Cristal del medidor",Vector2.zero,new Vector2(480,390));
  var image=backdrop.gameObject.AddComponent<Image>();image.color=new Color(.008f,.025f,.045f,.94f);image.raycastTarget=false;
  var border=backdrop.gameObject.AddComponent<Outline>();border.effectColor=new Color(.10f,.80f,.95f,.65f);border.effectDistance=new Vector2(1,-1);
  Label(root.transform,"Título","VÍNCULO Y CONFIANZA",new Vector2(0,174),new Vector2(450,32),23,p.heading.font);
  for(int i=0;i<5;i++) {
   var segment=Rect(root.transform,"Zona "+(i+1)+" · "+new[]{"Reserva","Incomodidad","Neutral","Confianza","Apertura"}[i],new Vector2(0,-35),new Vector2(380,190));
   segment.pivot=new Vector2(.5f,0);
   var arc=segment.gameObject.AddComponent<QuestEmotionArcGraphic>();arc.startAngle=180-(i+1)*36+1;arc.endAngle=180-i*36-1;arc.color=gauge.colors[i];arc.raycastTarget=false;
   float angle=(162-i*36)*Mathf.Deg2Rad;
   var icon=Rect(root.transform,"Expresión "+(i+1),new Vector2(Mathf.Cos(angle)*157,Mathf.Sin(angle)*157-35),new Vector2(34,34));
   var face=icon.gameObject.AddComponent<QuestEmotionFaceGraphic>();face.mood=i*.5f-1;face.color=new Color(.12f,.16f,.08f);face.raycastTarget=false;
  }
  var pivot=Rect(root.transform,"Aguja · giro 180° a 0°",new Vector2(0,-35),Vector2.one);gauge.needlePivot=pivot;
  var pointer=Rect(pivot,"Punta fina",Vector2.zero,new Vector2(155,9));pointer.pivot=new Vector2(0,.5f);
  var needle=pointer.gameObject.AddComponent<QuestEmotionArcGraphic>();needle.needle=true;needle.color=new Color(.91f,.97f,1);needle.raycastTarget=false;
  var hub=Rect(root.transform,"Carita central",new Vector2(0,-35),new Vector2(82,82));
  gauge.face=hub.gameObject.AddComponent<QuestEmotionFaceGraphic>();gauge.face.disk=true;gauge.face.color=new Color(.08f,.14f,.16f);gauge.face.raycastTarget=false;
  gauge.valueLabel=Label(root.transform,"Porcentaje","40%",new Vector2(0,-98),new Vector2(220,42),34,p.heading.font);
  gauge.stateLabel=Label(root.transform,"Estado y variación","Construyendo vínculo",new Vector2(0,-138),new Vector2(450,36),19,p.heading.font);
  gauge.stateLabel.resizeTextForBestFit=true;gauge.stateLabel.resizeTextMinSize=16;gauge.stateLabel.resizeTextMaxSize=19;
  Label(root.transform,"Extremo bajo","BAJA",new Vector2(-168,-66),new Vector2(100,28),15,p.heading.font);
  Label(root.transform,"Extremo alto","ALTA",new Vector2(168,-66),new Vector2(100,28),15,p.heading.font);
  Label(root.transform,"Alcance educativo","Señal educativa · no mide mejoría clínica",new Vector2(0,-178),new Vector2(450,26),13,p.heading.font);
  gauge.SetProgress(40,40,0,false);
  gauge.ShowFor(p.visualRoot.transform,Camera.main);
  gauge.Hide();EditorUtility.SetDirty(p);
 }
}
}
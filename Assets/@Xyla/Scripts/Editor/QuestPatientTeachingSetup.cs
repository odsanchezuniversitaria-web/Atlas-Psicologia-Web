using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace Xyla.Editor {
public static class QuestPatientTeachingSetup {
 [MenuItem("Atlas Humano Quest/Instalar casos y estado emocional")]
 public static void Install() {
  EditorSceneManager.OpenScene("Assets/@Xyla/Scenes/HumanAtlasQuest.unity");
  var patient=Object.FindFirstObjectByType<QuestPatientSimulation>(FindObjectsInactive.Include);
  Ensure(patient); EditorSceneManager.SaveOpenScenes();
 }
 public static void Ensure(QuestPatientSimulation p) {
  var t=p.GetComponent<QuestPatientTeaching>();
  if(!t)t=Undo.AddComponent<QuestPatientTeaching>(p.gameObject);
  p.teaching=t;t.patient=p;
  if(!t.caseLabel) {
   Transform parent=p.interviewPage.transform; Font font=p.heading.font;
   p.heading.rectTransform.anchoredPosition=new Vector2(0,285);p.heading.fontSize=21;
   p.heading.rectTransform.sizeDelta=new Vector2(830,40);
   p.briefLabel.rectTransform.anchoredPosition=new Vector2(0,164);p.briefLabel.rectTransform.sizeDelta=new Vector2(790,65);p.briefLabel.fontSize=19;
   p.transcriptLabel.rectTransform.anchoredPosition=new Vector2(0,19);p.transcriptLabel.rectTransform.sizeDelta=new Vector2(790,65);p.transcriptLabel.fontSize=21;
   p.patientLabel.rectTransform.anchoredPosition=new Vector2(0,-72);p.patientLabel.rectTransform.sizeDelta=new Vector2(790,105);p.patientLabel.fontSize=22;
   var group=new GameObject("04 · Casos y acompañamiento · editable",typeof(RectTransform));
   group.transform.SetParent(parent,false);group.GetComponent<RectTransform>().sizeDelta=new Vector2(920,780);
   t.caseLabel=Label(group.transform,"Caso seleccionado",new Vector2(0,235),new Vector2(650,48),21,font);
   MakeButton(group.transform,"Caso anterior","‹",new Vector2(-375,235),new Vector2(64,46),t.PreviousCase,font);
   MakeButton(group.transform,"Caso siguiente","›",new Vector2(375,235),new Vector2(64,46),t.NextCase,font);
   var note=Label(group.transform,"Aviso al cambiar de caso",new Vector2(0,204),new Vector2(790,22),13,font);
   note.text="Cambiar de caso inicia una entrevista nueva · cuatro simulaciones con el avatar de María";
   t.emotionLabel=Label(group.transform,"Estado emocional simulado",new Vector2(0,-142),new Vector2(790,28),16,font);
   var bg=Panel(group.transform,"Barra · fondo",new Vector2(0,-167),new Vector2(790,14),new Color(.06f,.13f,.18f));
   t.comfortFill=Panel(bg.transform,"Barra · comodidad",new Vector2(-395,0),new Vector2(790,14),new Color(.2f,.9f,.75f));
   t.comfortFill.rectTransform.pivot=new Vector2(0,.5f);
   t.signalLabel=Label(group.transform,"Señal y alcance educativo",new Vector2(0,-190),new Vector2(790,27),13,font);
   Panel(group.transform,"Tarjeta de ayuda",new Vector2(0,-234),new Vector2(810,63),new Color(.025f,.14f,.18f,.96f));
   t.adviceLabel=Label(group.transform,"Recomendación breve",new Vector2(0,-234),new Vector2(780,60),17,font);
   MakeButton(p.panelRoot.transform,"Alternar ayudas","AYUDAS",new Vector2(350,337),new Vector2(120,32),t.ToggleHints,font);
  }
  QuestEmotionGaugeSetup.Ensure(p);
  t.PrepareCase();
  EditorUtility.SetDirty(t);EditorUtility.SetDirty(p);
  EditorSceneManager.MarkSceneDirty(p.gameObject.scene);
  Debug.Log("[Atlas] Cuatro casos, barra de comodidad y ayudas instalados y editables.");
 }
 static Image Panel(Transform parent,string name,Vector2 pos,Vector2 size,Color color) {
  var g=new GameObject(name,typeof(RectTransform),typeof(Image));g.transform.SetParent(parent,false);
  var r=g.GetComponent<RectTransform>();r.anchoredPosition=pos;r.sizeDelta=size;
  var i=g.GetComponent<Image>();i.color=color;i.raycastTarget=false;return i;
 }
 static Text Label(Transform parent,string name,Vector2 pos,Vector2 size,int fontSize,Font font) {
  var g=new GameObject(name,typeof(RectTransform),typeof(Text));g.transform.SetParent(parent,false);
  var r=g.GetComponent<RectTransform>();r.anchoredPosition=pos;r.sizeDelta=size;
  var t=g.GetComponent<Text>();t.font=font;t.fontSize=fontSize;t.color=new Color(.84f,.96f,1);
  t.alignment=TextAnchor.MiddleCenter;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.raycastTarget=false;return t;
 }
 static void MakeButton(Transform parent,string name,string title,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action,Font font) {
  var i=Panel(parent,name,pos,size,new Color(.06f,.32f,.4f));i.raycastTarget=true;
  var b=i.gameObject.AddComponent<Button>();b.targetGraphic=i;b.navigation=new Navigation{mode=Navigation.Mode.None};
  UnityEventTools.AddPersistentListener(b.onClick,action);
  Label(i.transform,"Etiqueta",Vector2.zero,size,19,font).text=title;
 }
}
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Xyla {
[Serializable] public sealed class QuestPsychologyCase {
 public string id, title;
 [TextArea(2,5)] public string brief, opening;
 [Range(0,100)] public int initialComfort=35;
 public QuestPsychologyCase(string i,string t,string b,string o,int c){id=i;title=t;brief=b;opening=o;initialComfort=c;}
}
public sealed class QuestPatientTeaching : MonoBehaviour {
 [Header("Casos ficticios independientes · avatar compartido")]
 public QuestPsychologyCase[] cases = {
 new QuestPsychologyCase("maria_anxiety_v1","Preocupación persistente","Explora preocupación, control, sueño e impacto cotidiano.","Hola. Últimamente me preocupa casi todo y no estoy durmiendo bien.",40),
 new QuestPsychologyCase("maria_social_v1","Temor a la evaluación social","Explora situaciones temidas, evitación y repercusiones; respeta su ritmo.","Hola. Me cuesta mucho hablar cuando siento que otras personas me están evaluando.",30),
 new QuestPsychologyCase("maria_grief_v1","Proceso de duelo","Da espacio a la pérdida; explora apoyos y rutinas sin apresurar el duelo.","Hola. Desde que murió mi abuela siento un vacío y no sé muy bien cómo hablar de ello.",35),
 new QuestPsychologyCase("maria_low_mood_v1","Ánimo bajo y pérdida de interés","Explora ánimo, interés, funcionamiento, apoyos y seguridad.","Hola. Desde hace varias semanas me cuesta levantarme y ya no disfruto las cosas como antes.",35)};
 public int selectedCase;
 [Header("Paneles editables")]
 public Text caseLabel, emotionLabel, adviceLabel, signalLabel;
 public Image comfortFill;
 public QuestPatientEmotionGauge emotionGauge;
 int lastDelta;
 bool pending;
 public bool showHints=true;
 public QuestPatientSimulation patient;
 readonly Queue<string> recent=new Queue<string>();
 public int WebComfort=>comfort;public string WebAdvice=>advice;public string WebSignal=>signal;
 int comfort; float displayed;
 string advice="",signal="";
 public void Initialize(QuestPatientSimulation owner) {
  patient=owner; patient.voice.InteractionReceived+=Receive;
 }
 public void PrepareCase() {
  if(cases==null || cases.Length==0)throw new InvalidOperationException("Falta catálogo de casos.");
  selectedCase=Mathf.Clamp(selectedCase,0,cases.Length-1);
  var c=cases[selectedCase]; patient.voice.CaseId=c.id;
  patient.caseTitle=c.title; patient.caseBrief=c.brief; patient.openingLine=c.opening;
  patient.clinicalSource="Simulación educativa · indicadores de interacción";
  ResetProgress();
 }
 public void ResetProgress() {
  comfort=cases[selectedCase].initialComfort; displayed=comfort;lastDelta=0;pending=false;
  recent.Clear(); advice=cases[selectedCase].brief; signal="Inicio · escucha y pide permiso"; Refresh();
  comfortFill.rectTransform.localScale=new Vector3(comfort/100f,1,1);
 }
 public void NextCase(){SwitchCase(1);}
 public void PreviousCase(){SwitchCase(-1);}
 void SwitchCase(int step) {
  selectedCase=(selectedCase+step+cases.Length)%cases.Length;
  PrepareCase();
  if(patient.IsOpen) {
   patient.heading.text="MARÍA · "+cases[selectedCase].title.ToUpperInvariant();
   patient.briefLabel.text=patient.caseBrief;
   patient.RestartInterview();
  }
 }
 public void ToggleHints(){showHints=!showHints;Refresh();}
 static string Fold(string value) {
  var b=new StringBuilder();
  foreach(char c in value.Normalize(NormalizationForm.FormD))
   if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)b.Append(char.ToLowerInvariant(c));
  return b.ToString();
 }
 public void ObserveQuestion(string question) {
  pending=true;
  signal="Analizando el sentido de tu intervención…";
  advice="La barra se actualizará junto con la respuesta de María."; Refresh();
 }
 void Receive(JObject value) {
  if(!patient.IsOpen || value==null)return;
  comfort=Mathf.Clamp(value.Value<int>("comfort"),0,100);
  int delta=value.Value<int?>("delta")??0;lastDelta=delta;pending=false;
  advice=Clip(value.Value<string>("recommendation")??advice,220);
  string reason=Clip(value.Value<string>("signal")??"Intervención analizada",150);
  string evidence=value.Value<string>("evidence")??"";
  signal=(delta>0?"+":"")+delta+" · "+reason;
  if(value.Value<string>("assessment_source")=="local_fallback")signal+=" · análisis básico";
  // The recommendation stays short; full quotes remain in the final evaluation.
  Debug.Log("[Atlas Patient Teaching] confianza="+comfort+" delta="+delta+" fuente="+value.Value<string>("assessment_source"));
  Refresh();
 }
 static string Clip(string value,int length)=>value.Length<=length?value:value.Substring(0,length)+"…";
 void Refresh() {
  caseLabel.text=(selectedCase+1)+"/"+cases.Length+" · "+cases[selectedCase].title;
  emotionLabel.text="COMODIDAD Y CONFIANZA · "+comfort+"% · "+(comfort<30?"Con reserva":comfort<65?"Construyendo vínculo":"Mayor apertura");
  signalLabel.text=signal;
  if(emotionGauge)emotionGauge.SetProgress(displayed,comfort,lastDelta,pending);
  adviceLabel.text=showHints?"AYUDA · "+advice:"Ayudas ocultas · continúa escuchando al paciente";
 }
 void Update() {
  if(!patient || !patient.IsOpen)return;
  displayed=Mathf.MoveTowards(displayed,comfort,Time.unscaledDeltaTime*35);
  comfortFill.fillAmount=displayed/100f;
  comfortFill.rectTransform.localScale=new Vector3(displayed/100f,1,1);
  if(emotionGauge)emotionGauge.SetProgress(displayed,comfort,lastDelta,pending);
  comfortFill.color=Color.Lerp(new Color(.95f,.55f,.26f),new Color(.2f,.9f,.75f),displayed/100f);
 }
 void OnDestroy(){if(patient && patient.voice)patient.voice.InteractionReceived-=Receive;}
}
}

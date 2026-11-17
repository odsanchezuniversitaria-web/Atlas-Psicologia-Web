using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace Xyla.Editor {
public static class QuestExperiencePolishSetup {
 const string Atlas="Assets/@Xyla/Scenes/HumanAtlasQuest.unity";
 const string Landing="Assets/@Xyla/Scenes/AtlasInicio.unity";
 const string Mat="Assets/@Xyla/Materials/ClinicalUiStable.mat";
 [MenuItem("Atlas Humano Quest/Aplicar mejoras · labios, lectura y transiciones")]
 public static void Apply(){
  if(EditorApplication.isPlaying)throw new InvalidOperationException("Detén Play para guardar las mejoras.");
  EditorSceneManager.SaveOpenScenes();
  var material=AssetDatabase.LoadAssetAtPath<Material>(Mat);
  if(!material){
   var shader=Shader.Find("Atlas/ReadableUI");
   if(!shader)throw new InvalidOperationException("Falta el shader ReadableUI.");
   material=new Material(shader);material.name="UI clínica · texto estable";AssetDatabase.CreateAsset(material,Mat);
  }
  EditorSceneManager.OpenScene(Atlas);EnsureTransition(material);EnsureHaptics();
  foreach(var transport in UnityEngine.Object.FindObjectsByType<QuestLiveKitVoice>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
   var meter=transport.GetComponent<QuestAudioEnvelope>()??Undo.AddComponent<QuestAudioEnvelope>(transport.gameObject);
   transport.speechEnvelope=meter;EditorUtility.SetDirty(transport);EditorUtility.SetDirty(meter);
  }
  var patient=UnityEngine.Object.FindFirstObjectByType<QuestPatientSimulation>(FindObjectsInactive.Include);
  var readable=patient.panelRoot.GetComponent<QuestClinicalReadability>()??Undo.AddComponent<QuestClinicalReadability>(patient.panelRoot);
  readable.panelCanvas=patient.panelRoot.GetComponent<Canvas>();readable.stableMaterial=material;readable.ApplyMaterials();
  foreach(var graphic in patient.panelRoot.GetComponentsInChildren<Graphic>(true)){
   EditorUtility.SetDirty(graphic);EditorUtility.SetDirty(graphic.rectTransform);
  }
  patient.panelRoot.GetComponent<CanvasScaler>().dynamicPixelsPerUnit=3f;
  EditorUtility.SetDirty(readable);EditorUtility.SetDirty(patient.panelRoot.GetComponent<CanvasScaler>());
  EditorSceneManager.MarkSceneDirty(patient.gameObject.scene);EditorSceneManager.SaveOpenScenes();
  EditorSceneManager.OpenScene(Landing);EnsureTransition(material);EnsureHaptics();EditorSceneManager.SaveOpenScenes();
  PlayerSettings.bundleVersion="2.3.2";PlayerSettings.Android.bundleVersionCode=60;AssetDatabase.SaveAssets();
  EditorSceneManager.OpenScene(Atlas);
  Debug.Log("[Atlas Polish] Escenas guardadas: medidores PCM, panel estable y transiciones editables; versión2.3.2.");
 }
 static void EnsureHaptics(){
  var transition=UnityEngine.Object.FindFirstObjectByType<QuestAtlasTransitions>(FindObjectsInactive.Include);
  if(!transition.GetComponent<QuestAtlasHaptics>())Undo.AddComponent<QuestAtlasHaptics>(transition.gameObject);
  var menu=UnityEngine.Object.FindFirstObjectByType<QuestAtlasSceneMenu>(FindObjectsInactive.Include);
  if(!menu.hapticsLabel){
   Button template=null;
   foreach(var b in menu.settingsPage.GetComponentsInChildren<Button>(true))if(b.name=="Sonidos")template=b;
   if(!template)throw new InvalidOperationException("Falta el botón de referencia para vibración.");
   var button=UnityEngine.Object.Instantiate(template,template.transform.parent);
   button.name="Vibración de los controles · intensidad";
   while(button.onClick.GetPersistentEventCount()>0)UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick,0);
   button.onClick.RemoveAllListeners();UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick,menu.CycleHaptics);
   var rect=button.GetComponent<RectTransform>();rect.anchoredPosition=new Vector2(0,-159);rect.sizeDelta=new Vector2(790,42);
   menu.hapticsLabel=button.GetComponentInChildren<Text>(true);menu.hapticsLabel.text="VIBRACIÓN · NORMAL";
   foreach(var b in menu.settingsPage.GetComponentsInChildren<Button>(true))if(b.name=="Volver al menú")b.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-224);
   EditorUtility.SetDirty(menu);
  }
  foreach(var b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))
   if(!(b is QuestAtlasButton) && !b.GetComponent<QuestHapticButton>())Undo.AddComponent<QuestHapticButton>(b.gameObject);
  EditorSceneManager.MarkSceneDirty(transition.gameObject.scene);
 }
 static void EnsureTransition(Material material){
  var existing=UnityEngine.Object.FindFirstObjectByType<QuestAtlasTransitions>(FindObjectsInactive.Include);
  if(existing)return;
  var root=new GameObject("TRANSICIONES XR · escenas, modos y sistemas · editable",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));
  root.layer=5;var rect=root.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(8000,8000);root.transform.localScale=Vector3.one*.001f;
  var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=32000;canvas.overrideSorting=true;canvas.worldCamera=Camera.main;
  var group=root.GetComponent<CanvasGroup>();group.alpha=0;group.blocksRaycasts=false;group.interactable=false;
  var veil=new GameObject("Velo médico suave",typeof(RectTransform),typeof(Image));veil.layer=5;veil.transform.SetParent(root.transform,false);
  veil.GetComponent<RectTransform>().sizeDelta=new Vector2(8000,8000);
  var image=veil.GetComponent<Image>();image.color=new Color(.004f,.02f,.035f,1f);image.raycastTarget=false;image.material=material;
  var caption=new GameObject("Estado de la transición",typeof(RectTransform),typeof(Text));caption.layer=5;caption.transform.SetParent(root.transform,false);
  caption.GetComponent<RectTransform>().sizeDelta=new Vector2(1200,140);
  var text=caption.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=24;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.material=material;text.color=new Color(.55f,.94f,1f);text.text="";
  var component=root.AddComponent<QuestAtlasTransitions>();component.canvas=canvas;component.group=group;component.veil=image;component.label=text;
  EditorSceneManager.MarkSceneDirty(root.scene);
 }
 [MenuItem("Atlas Humano Quest/Validar mejoras · PCM real y resultados")]
 public static void Validate(){
  if(!EditorApplication.isPlaying)throw new InvalidOperationException("Inicia Play en HumanAtlasQuest.");
  var go=new GameObject("QA · captura PCM",typeof(AudioSource),typeof(QuestAudioEnvelope));
  try{
   var type=typeof(LiveKit.Room).Assembly.GetType("LiveKit.AudioProbe",true);
   var probe=go.AddComponent(type);var ev=type.GetEvent("AudioRead");var field=type.GetField("AudioRead",BindingFlags.Instance|BindingFlags.NonPublic);
   var generator=Delegate.CreateDelegate(ev.EventHandlerType,typeof(QuestExperiencePolishSetup).GetMethod("GeneratePcm",BindingFlags.Static|BindingFlags.Public));
   ev.AddEventHandler(probe,generator);var envelope=go.GetComponent<QuestAudioEnvelope>();envelope.BindNativeStream(go.GetComponent<AudioSource>());
   var pcm=new float[960];((Delegate)field.GetValue(probe)).DynamicInvoke(pcm,1,24000);
   if(envelope.Level<.3f || envelope.Level>.9f)throw new Exception("El medidor no recibió el PCM generado.");
   float voiced=envelope.Level;envelope.BindNativeStream(go.GetComponent<AudioSource>());
   if(((Delegate)field.GetValue(probe)).GetInvocationList().Length!=2)throw new Exception("La reconexión duplicó el observador.");
   Array.Clear(pcm,0,pcm.Length);envelope.CaptureNativeAudio(pcm,1,24000);
   if(envelope.Level!=0)throw new Exception("La boca no se cierra al terminar el audio.");
   envelope.UnbindNativeStream();
   if(((Delegate)field.GetValue(probe)).GetInvocationList().Length!=1)throw new Exception("Se filtró un observador PCM.");
   Debug.Log("[Atlas Polish QA] PCM nativo sin clip: nivel="+voiced.ToString("F3")+"; silencio y reconexión sin duplicados correctos.");
  }finally{UnityEngine.Object.DestroyImmediate(go);}
  var p=UnityEngine.Object.FindFirstObjectByType<QuestPatientSimulation>(FindObjectsInactive.Include);
  p.Open();if(!p.visualRoot.activeInHierarchy)throw new Exception("Paciente no visible.");
  var readable=p.panelRoot.GetComponent<QuestClinicalReadability>();
  foreach(var text in p.panelRoot.GetComponentsInChildren<Text>(true)){
   if(text.material!=readable.stableMaterial)throw new Exception("Texto sin material estable: "+text.name);
   if(text.rectTransform.localPosition.z>=0)throw new Exception("Texto sin separación de capas.");
  }
  p.resultsDashboard.ShowPending(65,2);
  if(p.resultsDashboard.scoreLabel.text.Contains("%"))throw new Exception("Se mostró un porcentaje sin evaluación.");
  foreach(var button in p.resultsDashboard.criterionButtons)if(button.interactable)throw new Exception("Tarjeta sin evaluación activa.");
  QuestAtlasTransitions.Current.Pulse(true);
  if(QuestAtlasTransitions.Current.group.alpha<=0)throw new Exception("Transición de sistemas ausente.");
  Debug.Log("[Atlas Polish QA] Paciente, capas del panel, espera sin puntuaciones inventadas y transición verificados.");
 }
 public static void GeneratePcm(float[] data,int channels,int sampleRate){
  for(int i=0;i<data.Length;i++)data[i]=.03f*(float)Math.Sin(i*2*Math.PI*220/sampleRate);
 }
}}

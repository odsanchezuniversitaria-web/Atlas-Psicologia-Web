using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.SceneManagement;
using WebXR;
namespace Xyla {
[DefaultExecutionOrder(-10000)]
public sealed partial class AtlasWebRuntime : MonoBehaviour {
 public static AtlasWebRuntime Current {get;private set;}
 [Header("Referencias editables")]public Camera view;public Transform player;public WebXRController leftController,rightController;public LineRenderer laser;public bool landing;
 [Header("Controles PC / móvil")][Range(.2f,5f)]public float moveSpeed=1.5f;[Range(.02f,.5f)]public float lookSensitivity=.10f;public float rayDistance=4f;
 [Header("Seguridad de movimiento")]public Vector2 worldLimits=new Vector2(-8,12);public float eyeHeight=1.6f;
 public QuestAtlasController atlas;
 public QuestAtlasPiece Pointed {get;private set;}public QuestAtlasPiece Held {get;private set;}
 public bool InVR {get;private set;}public bool TalkHeld {get;private set;}
 float moveX,moveY,lookX,lookY,pointerX=.5f,pointerY=.5f,pitch,nextState,nextCanvas;bool pointerDown,pointerUp,dragging,typing,uiBlocked,touchTalk,snapReady=true;
 float dragDistance=1.05f;GameObject pressedUI;PointerEventData eventData;
 Renderer[] controlVisuals;
 readonly List<Canvas> canvases=new List<Canvas>();readonly Dictionary<Canvas,bool> canvasEnabled=new Dictionary<Canvas,bool>();
 readonly List<RaycastResult> hits=new List<RaycastResult>();JObject pendingResume;int startedFrame;
 void Awake(){Current=this;gameObject.name="AtlasWeb";Application.targetFrameRate=60;WebXRManager.OnXRChange+=XRChanged;Application.onBeforeRender+=Head;startedFrame=Time.frameCount;}
 void Start(){controlVisuals=new[]{leftController.GetComponentInChildren<Renderer>(),rightController.GetComponentInChildren<Renderer>()};foreach(var renderer in controlVisuals)renderer.enabled=false;player.position=Vector3.zero;view.transform.localPosition=new Vector3(0,eyeHeight,-.75f);view.stereoTargetEye=StereoTargetEyeMask.None;view.fieldOfView=62;var xr=WebXRManager.Instance;XRChanged(xr.XRState,xr.ViewsCount,xr.ViewsLeftRect,xr.ViewsRightRect);AtlasWebPlatform.Ready();}
 void OnDestroy(){WebXRManager.OnXRChange-=XRChanged;Application.onBeforeRender-=Head;Release();if(Current==this)Current=null;}
 public void Configure(string json){
  var value=JObject.Parse(json);AtlasWebPlatform.ApiBaseUrl=value.Value<string>("apiBaseUrl")??"https://api.oscar-vr.iudcdesarrollo.tech/api/v1/atlas";AtlasWebPlatform.AccessToken=value.Value<string>("accessToken")??"";AtlasWebPlatform.Profile=value.Value<string>("profileKey")??"practice";AtlasWebPlatform.Evaluated=value.Value<bool?>("evaluated")??false;
  if(value["checkpoint"] is JObject checkpoint){AtlasWebProgress.Restore(checkpoint);pendingResume=checkpoint;}
 }
 public void ReceiveInput(string json){
  var v=JObject.Parse(json);moveX=v.Value<float?>("moveX")??moveX;moveY=v.Value<float?>("moveY")??moveY;
  lookX+=v.Value<float?>("lookX")??0;lookY+=v.Value<float?>("lookY")??0;
  pointerX=v.Value<float?>("x")??pointerX;pointerY=v.Value<float?>("y")??pointerY;
  pointerDown|=v.Value<bool?>("down")??false;pointerUp|=v.Value<bool?>("up")??false;
  touchTalk=v.Value<bool?>("talk")??touchTalk;typing=v.Value<bool?>("typing")??typing;uiBlocked=v.Value<bool?>("ui")??uiBlocked;
  if(v.Value<bool?>("cancel")==true)ResetInput();
 }
 public void ResetInput(){EndTouchHold();moveX=moveY=lookX=lookY=0;touchTalk=false;pointerDown=false;pointerUp=true;Release();}
 void XRChanged(WebXRState state,int count,Rect left,Rect right){
  if (state==WebXRState.VR||state==WebXRState.AR) {
   if (touchEnabled && !string.IsNullOrEmpty(studyId) && atlas) atlas.PrepareWebStudyColliders();
   RestoreStudyCamera();
  }
  InVR=state==WebXRState.VR||state==WebXRState.AR;if(controlVisuals!=null)foreach(var renderer in controlVisuals)renderer.enabled=InVR;view.stereoTargetEye=InVR?StereoTargetEyeMask.Both:StereoTargetEyeMask.None;ResetInput();
  if(!InVR){view.transform.localPosition=new Vector3(0,eyeHeight,-.75f);view.transform.localRotation=Quaternion.identity;}
  foreach(var pair in canvasEnabled)if(pair.Key)pair.Key.enabled=InVR&&pair.Value;nextCanvas=0;nextState=0;
 }
 public void Pulse(float strength,float seconds){if(!InVR)return;leftController.Pulse(Mathf.Clamp01(strength),Mathf.Clamp(seconds*1000,5,200));rightController.Pulse(Mathf.Clamp01(strength),Mathf.Clamp(seconds*1000,5,200));}
 void Head(){
  if(!InVR)return;var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
  if(head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyePosition,out Vector3 position))view.transform.localPosition=position;
  if(head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyeRotation,out Quaternion rotation))view.transform.localRotation=rotation;
 }
 void Update(){
  if(Time.frameCount-startedFrame<2)return;
  if(Time.unscaledTime>=nextCanvas){nextCanvas=Time.unscaledTime+.5f;AdaptCanvases();}
  if(landing&&InVR){var ray=Pointer();HandleVRUI(ray);laser.enabled=true;laser.SetPosition(0,ray.origin);laser.SetPosition(1,ray.GetPoint(rayDistance));}
  if(!landing&&atlas&&atlas.Voice){if (!TouchExploration()) { HandleMovement(); HandlePointer(); } HandleTalk();RestorePoses();}
  if(Time.unscaledTime>=nextState){nextState=Time.unscaledTime+.25f;PublishState();}
  pointerDown=pointerUp=false;lookX=lookY=0;
 }
 void AdaptCanvases(){
  foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
   if(canvas==GetComponent<Canvas>()||canvas.GetComponent<QuestAtlasTransitions>()||canvas.GetComponent<QuestAtlasAvatar>())continue;
   if(!canvasEnabled.ContainsKey(canvas)){canvasEnabled.Add(canvas,canvas.enabled);canvases.Add(canvas);}
   canvas.enabled=InVR&&canvasEnabled[canvas];
  }
 }
 void HandleMovement(){
  Vector2 motion=new Vector2(moveX,moveY);
  if(!typing&&Keyboard.current!=null){if(Keyboard.current.wKey.isPressed)motion.y+=1;if(Keyboard.current.sKey.isPressed)motion.y-=1;if(Keyboard.current.aKey.isPressed)motion.x-=1;if(Keyboard.current.dKey.isPressed)motion.x+=1;}
  if(InVR){
   motion+=leftController.GetAxis2D(WebXRController.Axis2DTypes.Thumbstick);Head();
   float turn=rightController.GetAxis2D(WebXRController.Axis2DTypes.Thumbstick).x;if(Mathf.Abs(turn)<.25f)snapReady=true;
   if(snapReady&&Mathf.Abs(turn)>.7f){player.Rotate(0,Mathf.Sign(turn)*30,0);snapReady=false;}
  }else{
   Vector2 look=new Vector2(lookX,lookY);
   if(!typing&&Mouse.current!=null&&Mouse.current.rightButton.isPressed)look+=Mouse.current.delta.ReadValue();
   if(Held)look=Vector2.zero;
   player.Rotate(0,look.x*lookSensitivity,0);pitch=Mathf.Clamp(pitch-look.y*lookSensitivity,-65,65);view.transform.localRotation=Quaternion.Euler(pitch,0,0);
  }
  motion=Vector2.ClampMagnitude(motion,1);Vector3 forward=Vector3.ProjectOnPlane(view.transform.forward,Vector3.up).normalized;Vector3 right=Vector3.Cross(Vector3.up,forward);
  player.position+= (forward*motion.y+right*motion.x)*moveSpeed*Time.unscaledDeltaTime;
  player.position=new Vector3(Mathf.Clamp(player.position.x,worldLimits.x,worldLimits.y),0,Mathf.Clamp(player.position.z,worldLimits.x,worldLimits.y));
  if(!typing&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){if(atlas.patientSimulation.IsOpen)atlas.patientSimulation.Close();else atlas.sceneMenu.OpenMain();}
 }
 void HandleTalk(){
  bool keyboard=!typing&&Keyboard.current!=null&&Keyboard.current.spaceKey.isPressed;
  bool xr=InVR&&(rightController.GetButton(WebXRController.ButtonTypes.ButtonA)||leftController.GetButton(WebXRController.ButtonTypes.ButtonA));
  bool talk=touchTalk||keyboard||xr;
  if(talk!=TalkHeld){TalkHeld=talk;AtlasWebPlatform.Haptic(talk?.25f:.1f,.04f);}
  if(atlas.patientSimulation.IsOpen){atlas.Voice.SetPushToTalk(false);atlas.patientSimulation.voice.SetPushToTalk(talk);}else atlas.Voice.SetPushToTalk(talk);
 }
 Ray Pointer(){
  if(InVR)return new Ray(rightController.transform.position,rightController.transform.forward);
  Vector2 point=new Vector2(pointerX*Screen.width,pointerY*Screen.height);
  if(!Application.isMobilePlatform&&Mouse.current!=null)point=Mouse.current.position.ReadValue();
  return view.ScreenPointToRay(point);
 }
 void HandlePointer(){
  if(!InVR&&uiBlocked){if(pointerUp||(Mouse.current!=null&&Mouse.current.leftButton.wasReleasedThisFrame))Release();return;}
  Ray ray=Pointer();bool down=pointerDown,up=pointerUp;
  if(InVR){down|=rightController.GetButtonDown(WebXRController.ButtonTypes.Grip);up|=rightController.GetButtonUp(WebXRController.ButtonTypes.Grip);HandleVRUI(ray);}
  else if(!typing&&Mouse.current!=null&&!Application.isMobilePlatform){down|=Mouse.current.leftButton.wasPressedThisFrame;up|=Mouse.current.leftButton.wasReleasedThisFrame;}
  QuestAtlasPiece piece=null;float distance=rayDistance;
  if(Physics.Raycast(ray,out RaycastHit hit,rayDistance,~0,QueryTriggerInteraction.Ignore)){piece=hit.collider.GetComponentInParent<QuestAtlasPiece>();distance=hit.distance;}
  if(Pointed!=piece){if(Pointed)Pointed.WebHover(false);Pointed=piece;if(Pointed)Pointed.WebHover(true);}
  if(down&&Pointed&&pressedUI==null){Held=Pointed;Held.WebHold(true);if(!Held.Detached){Held.WebHold(false);Held=null;return;}dragging=true;dragDistance=Mathf.Clamp(distance,.65f,2);AtlasWebPlatform.Haptic(.4f,.06f);}
  if(Held&&dragging){
   Vector3 target=ray.GetPoint(Mathf.Min(dragDistance,1.25f));Held.transform.position=Vector3.Lerp(Held.transform.position,target,1-Mathf.Exp(-Time.unscaledDeltaTime*18));
   if(!InVR&&Mouse.current!=null){float wheel=Mouse.current.scroll.ReadValue().y;if(Mathf.Abs(wheel)>.1f)Held.transform.localScale*=Mathf.Clamp(1+wheel*.001f,.8f,1.2f);}
  }
  if(up){Release();SavePoses();}
  laser.enabled=InVR;laser.SetPosition(0,ray.origin);laser.SetPosition(1,ray.GetPoint(distance));
 }
 void HandleVRUI(Ray ray){
  GameObject target=null;float nearest=rayDistance;int depth=int.MinValue;
  foreach(var canvas in canvases){
   if(!canvas||!canvas.isActiveAndEnabled||canvas.renderMode!=RenderMode.WorldSpace)continue;
   var plane=new Plane(canvas.transform.forward,canvas.transform.position);if(!plane.Raycast(ray,out float distance)||distance<0||distance>nearest)continue;
   Vector2 screen=view.WorldToScreenPoint(ray.GetPoint(distance));
   foreach(var graphic in GraphicRegistry.GetGraphicsForCanvas(canvas)){
    if(!graphic||!graphic.raycastTarget||!graphic.isActiveAndEnabled||graphic.depth<0)continue;
    if(!RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform,screen,view)||!graphic.Raycast(screen,view))continue;
    int order=canvas.sortingOrder*10000+graphic.depth;if(distance<nearest-.01f||order>=depth){nearest=distance;depth=order;target=graphic.gameObject;}
   }
  }
  if(eventData==null)eventData=new PointerEventData(EventSystem.current){pointerId=-4242,button=PointerEventData.InputButton.Left};
  if(rightController.GetButtonDown(WebXRController.ButtonTypes.Trigger)&&target){
   eventData.position=view.WorldToScreenPoint(ray.GetPoint(nearest));pressedUI=ExecuteEvents.GetEventHandler<IPointerDownHandler>(target);
   if(pressedUI)ExecuteEvents.Execute(pressedUI,eventData,ExecuteEvents.pointerDownHandler);AtlasWebPlatform.Haptic(.2f,.03f);
  }
  if(rightController.GetButtonUp(WebXRController.ButtonTypes.Trigger)&&pressedUI){
   ExecuteEvents.Execute(pressedUI,eventData,ExecuteEvents.pointerUpHandler);
   if(target&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(target)==pressedUI)ExecuteEvents.Execute(pressedUI,eventData,ExecuteEvents.pointerClickHandler);
   pressedUI=null;
  }
 }
 void Release(){if(Held){Held.WebHold(false);Held=null;}dragging=false;if(pressedUI&&eventData!=null)ExecuteEvents.Execute(pressedUI,eventData,ExecuteEvents.pointerUpHandler);pressedUI=null;}
 void OnApplicationFocus(bool focused){if(!focused)ResetInput();}
 void OnApplicationPause(bool paused){if(paused)ResetInput();}
 void SavePoses(){
  if(!atlas)return;var poses=new JArray();
  foreach(var piece in FindObjectsByType<QuestAtlasPiece>(FindObjectsSortMode.None)){
   if(!piece.Detached||poses.Count>=128)continue;var t=piece.transform;
   poses.Add(new JObject{["id"]=atlas.data.parts[piece.Index].id,["position"]=new JArray(t.localPosition.x,t.localPosition.y,t.localPosition.z),["rotation"]=new JArray(t.localRotation.x,t.localRotation.y,t.localRotation.z,t.localRotation.w),["scale"]=new JArray(t.localScale.x,t.localScale.y,t.localScale.z)});
  }AtlasWebProgress.SetPoses(poses);
 }
 void RestorePoses(){
  if(pendingResume==null)return;var checkpoint=pendingResume;pendingResume=null;
  foreach(JObject pose in checkpoint["poses"] as JArray??new JArray()){
   int index=atlas.FindPartIndex(pose.Value<string>("id"));if(index<0)continue;
   var piece=atlas.EnsureLearningPiece(index);var p=(JArray)pose["position"];var r=(JArray)pose["rotation"];var s=(JArray)pose["scale"];
   piece.RestorePose(atlas.transform.TransformPoint(new Vector3((float)p[0],(float)p[1],(float)p[2])),atlas.transform.rotation*new Quaternion((float)r[0],(float)r[1],(float)r[2],(float)r[3]),new Vector3((float)s[0],(float)s[1],(float)s[2]),true);
  }
 }
 public void Command(string json){
  try{
   var value=JObject.Parse(json);string op=value.Value<string>("op");int index=value.Value<int?>("index")??0;float number=value.Value<float?>("value")??0;
   if(op=="start_atlas"||op=="start_patient"){var menu=FindFirstObjectByType<QuestAtlasSceneMenu>(FindObjectsInactive.Include);if(op=="start_atlas")menu.EnterAtlas();else menu.EnterPsychology();return;}
   if(op=="vr"){WebXRManager.Instance.ToggleVR();return;}
   if(op=="ar"){WebXRManager.Instance.ToggleAR();return;}
   if(!atlas||!atlas.Voice)throw new InvalidOperationException("Espera a que termine de preparar el laboratorio.");
   if (op=="system"||op=="none"||op=="all"||op=="reset"||op=="undo"||op=="scale"||op=="rotate") studyId="";
   switch(op){
    case "study": ShowWebStudy(value.Value<string>("target"));break;
    case "system":atlas.ToggleSystem(index);break;case "all":atlas.ShowAllSystems();break;case "none":atlas.HideAllSystems();break;case "reset":Release();atlas.ResetAtlas();SavePoses();break;case "undo":Release();atlas.UndoLast();SavePoses();break;
    case "scale":if(Held)atlas.ManipulatePart(atlas.data.parts[Held.Index].id,"scale",number);else atlas.ScaleModel(number);break;
    case "rotate":atlas.RotateModel(number);break;case "return":if(Pointed)Pointed.RequestReturnToOrigin();break;
    case "patient":atlas.OpenPatientSimulation();break;case "neuro":if(atlas.patientSimulation.IsOpen)atlas.patientSimulation.Close();atlas.OpenPsychology();break;
    case "mission":atlas.OpenPsychology();atlas.psychologyPanel.StartMission(index);break;case "hint":atlas.psychologyPanel.ShowHint();break;case "next_mission":atlas.psychologyPanel.NextMission();break;
    case "tutorial":atlas.psychologyPanel.OpenTutorial();break;case "tutorial_next":atlas.psychologyPanel.NextTutorial();break;
    case "case":var p=atlas.patientSimulation;if(!p.IsOpen)atlas.OpenPatientSimulation();p.teaching.selectedCase=Mathf.Clamp(index,0,p.teaching.cases.Length-1);p.teaching.PrepareCase();p.RestartInterview();break;
    case "finish":atlas.patientSimulation.FinishInterview();break;case "restart":atlas.patientSimulation.RestartInterview();break;case "close_patient":atlas.patientSimulation.Close();break;case "hints":atlas.patientSimulation.teaching.ToggleHints();break;
    case "lesson":atlas.Learning.StartLesson(Array.Empty<string>(),Array.Empty<string>());break;case "exam":atlas.Learning.StartExam(Array.Empty<string>(),Array.Empty<string>());break;case "dissection":atlas.Learning.ToggleDissection();break;
    case "time":atlas.Learning.StartTimeAttack();break;case "detective":atlas.Learning.StartDetective();break;case "survival":atlas.Learning.StartSurvival();break;case "stop_exam":atlas.Learning.StopExam();break;case "stop_game":atlas.Learning.StopGame();break;
    case "next_lesson":atlas.Learning.NextLesson();break;case "repeat":atlas.Voice.RepeatAnswer();break;case "listen":atlas.Voice.ToggleListening();break;
    case "guide":atlas.spokenGuide.PlayGuide(atlas.patientSimulation.IsOpen);break;
    case "effects":atlas.sceneMenu.ToggleEffects();break;case "sounds":atlas.sceneMenu.ToggleSounds();break;case "haptics":atlas.sceneMenu.CycleHaptics();break;
    case "volume":atlas.Voice.SetVoiceVolume(number);PlayerPrefs.SetFloat("Atlas.VoiceVolume",number);break;
    case "home":SavePoses();atlas.sceneMenu.gameObject.SetActive(true);atlas.sceneMenu.ReturnToStart();break;
    default:throw new ArgumentException("Comando web desconocido.");
   }nextState=0;
  }catch(Exception error){AtlasWebPlatform.State(new JObject{["error"]=error.Message});}
 }
 void PublishState(){
  var state=new JObject{["landing"]=landing,["ready"]=landing||(atlas&&atlas.Voice!=null),["vr"]=InVR,["evaluated"]=AtlasWebPlatform.Evaluated,["profile"]=AtlasWebPlatform.Profile,["checkpoint"]=AtlasWebProgress.Capture()};
  if(!landing&&atlas&&atlas.Voice){
   var systems=new JArray();for(int i=0;i<AtlasSystems.Ids.Length;i++)systems.Add(new JObject{["name"]=AtlasSystems.Names[i],["id"]=AtlasSystems.Ids[i],["visible"]=atlas.IsSystemVisible(i)});
   var patient=atlas.patientSimulation;var missions=new JArray();for(int i=0;i<atlas.psychologyPanel.missions.Length;i++)missions.Add(new JObject{["title"]=atlas.psychologyPanel.missions[i].title,["index"]=i,["done"]=AtlasWebProgress.GetInt("Atlas.Psychology.Mission."+i)==1});
   state["study"]=studyId;state["systems"]=systems;state["missions"]=missions;state["patientOpen"]=patient.IsOpen;state["status"]=patient.IsOpen?patient.voice.Status:atlas.Voice.Status;
   state["transcript"]=patient.IsOpen?patient.voice.Transcript:atlas.Voice.Transcript;state["answer"]=patient.IsOpen?patient.voice.Answer:atlas.Voice.Answer;
   state["volume"]=atlas.Voice.VoiceVolume;
   state["speaking"]=patient.IsOpen?patient.voice.Speaking:atlas.Voice.Speaking;state["thinking"]=patient.IsOpen?patient.voice.WaitingForReply:atlas.Voice.Busy;state["talk"]=TalkHeld;
   state["learning"]=new JObject{["status"]=atlas.Learning.Status,["progress"]=atlas.Learning.Progress};
   state["neuro"]=new JObject{["question"]=atlas.psychologyPanel.missionQuestion.text,["feedback"]=atlas.psychologyPanel.feedbackBody.text,["tutorial"]=atlas.psychologyPanel.tutorialBody.text};
   var cases=new JArray();foreach(var c in patient.teaching.cases)cases.Add(new JObject{["id"]=c.id,["title"]=c.title,["brief"]=c.brief});state["cases"]=cases;state["caseIndex"]=patient.teaching.selectedCase;
   state["emotion"]=new JObject{["comfort"]=patient.teaching.WebComfort,["advice"]=patient.teaching.WebAdvice,["signal"]=patient.teaching.WebSignal};
   state["results"]=patient.resultsDashboard.CurrentReport;
   var info=FindFirstObjectByType<QuestAtlasInformationPanel>(FindObjectsInactive.Include);if(info&&info.SelectedPartId!=null)state["part"]=new JObject{["id"]=info.SelectedPartId,["name"]=info.DisplayedName,["description"]=info.DisplayedDescription,["function"]=info.DisplayedFunction};
  }AtlasWebPlatform.State(state);
 }
}}

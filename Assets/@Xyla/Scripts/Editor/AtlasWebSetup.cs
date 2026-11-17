using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;
using WebXR;
namespace Xyla.Editor {
public static class AtlasWebSetup {
 const string Root="Assets/@Xyla/Scenes/";
 [MenuItem("Atlas Web/Preparar escenas y perfiles editables")]
 public static void Apply(){
  Guard();if(EditorApplication.isPlaying)throw new InvalidOperationException("Detén Play.");
  MakeCopy("AtlasInicio","AtlasWebInicio");MakeCopy("HumanAtlasQuest","AtlasWebLab");
  Prepare("AtlasWebInicio",true);Prepare("AtlasWebLab",false);ConfigureXR();
  PlayerSettings.productName="Atlas Psicología Web";PlayerSettings.companyName="Oscar VR";PlayerSettings.bundleVersion="0.1.0";
  PlayerSettings.WebGL.template="PROJECT:AtlasNexus";PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Brotli;PlayerSettings.WebGL.decompressionFallback=true;PlayerSettings.WebGL.dataCaching=true;PlayerSettings.WebGL.initialMemorySize=256;PlayerSettings.WebGL.maximumMemorySize=1024;
  PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.runInBackground=false;
  PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL,new[]{GraphicsDeviceType.OpenGLES3});
  EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"AtlasWebInicio.unity",true),new EditorBuildSettingsScene(Root+"AtlasWebLab.unity",true)};
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(Root+"AtlasWebInicio.unity");
  Debug.Log("[Atlas Web] Dos escenas y WebXR preparados únicamente en la copia independiente.");
 }
 static void Guard(){if(!Application.dataPath.Replace('\\','/').Equals("D:/1a Atlas Psicologia/Assets",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Esta herramienta solo trabaja en la copia independiente.");}
 static void MakeCopy(string source,string target){if(!File.Exists(Root+target+".unity")&&!AssetDatabase.CopyAsset(Root+source+".unity",Root+target+".unity"))throw new InvalidOperationException("No se pudo copiar "+source);}
 static void Prepare(string sceneName,bool landing){
  EditorSceneManager.OpenScene(Root+sceneName+".unity");
  if(UnityEngine.Object.FindFirstObjectByType<AtlasWebRuntime>(FindObjectsInactive.Include)){foreach(var module in UnityEngine.Object.FindObjectsByType<BaseInputModule>(FindObjectsInactive.Include,FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(module);EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();return;}
  foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())if(root.CompareTag("EditorOnly"))UnityEngine.Object.DestroyImmediate(root);
  foreach(var rig in UnityEngine.Object.FindObjectsByType<OVRCameraRig>(FindObjectsInactive.Include,FindObjectsSortMode.None))rig.gameObject.SetActive(false);
  foreach(var behavior in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
   if(!behavior)continue;string n=behavior.GetType().FullName??"";
   if(n.StartsWith("Oculus.")||n.StartsWith("Meta.")||n.StartsWith("OVR"))behavior.enabled=false;
  }
  foreach(var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None)){camera.enabled=false;if(camera.CompareTag("MainCamera"))camera.tag="Untagged";}
  foreach(var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include,FindObjectsSortMode.None))listener.enabled=false;
  foreach(var events in UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include,FindObjectsSortMode.None))events.gameObject.SetActive(false);
  var web=new GameObject("AtlasWeb");var runtime=web.AddComponent<AtlasWebRuntime>();runtime.landing=landing;
  var player=new GameObject("WEB · Jugador PC, celular y WebXR");player.transform.SetParent(web.transform,false);runtime.player=player.transform;
  var viewObject=new GameObject("WEB · Cámara principal",typeof(Camera),typeof(AudioListener),typeof(UniversalAdditionalCameraData));viewObject.tag="MainCamera";viewObject.transform.SetParent(player.transform,false);viewObject.transform.localPosition=new Vector3(0,1.6f,-.75f);
  var view=viewObject.GetComponent<Camera>();view.nearClipPlane=.05f;view.farClipPlane=60;view.fieldOfView=62;view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.007f,.022f,.04f);view.stereoTargetEye=StereoTargetEyeMask.None;runtime.view=view;
  var cameraData=viewObject.GetComponent<UniversalAdditionalCameraData>();cameraData.renderPostProcessing=false;cameraData.renderShadows=false;
  var manager=new GameObject("WEB · Gestor WebXR",typeof(WebXRManager));manager.transform.SetParent(web.transform,false);
  runtime.leftController=Controller(player.transform,WebXRControllerHand.LEFT);runtime.rightController=Controller(player.transform,WebXRControllerHand.RIGHT);
  var laserObject=new GameObject("WEB · Rayo de selección");laserObject.transform.SetParent(web.transform,false);var line=laserObject.AddComponent<LineRenderer>();line.positionCount=2;line.useWorldSpace=true;line.startWidth=.003f;line.endWidth=.005f;
  const string materialPath="Assets/@Xyla/Materials/AtlasWebPointer.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.color=new Color(.1f,.85f,1);AssetDatabase.CreateAsset(material,materialPath);}line.sharedMaterial=material;line.enabled=false;runtime.laser=line;
  var eventObject=new GameObject("WEB · Eventos de interfaz",typeof(EventSystem));foreach(var module in eventObject.GetComponents<BaseInputModule>())UnityEngine.Object.DestroyImmediate(module);
  runtime.atlas=UnityEngine.Object.FindFirstObjectByType<QuestAtlasController>(FindObjectsInactive.Include);
  if(!landing){
   runtime.atlas.patientSimulation.office.floorOrigin=player.transform;
   runtime.atlas.spokenGuide.atlasSteps=new[]{"Soy Atlas. Mantén el botón Hablar, la barra espaciadora en PC o A en realidad virtual. Suelta al terminar.","Señala una pieza y pregunta qué es o para qué sirve. También puedes preguntarme sobre anatomía sin seleccionar nada.","Puedes pedirme mostrar u ocultar sistemas y acercar piezas. En PC arrastra una pieza con el mouse; en celular tócala y arrastra; en VR usa el agarre.","Muévete con WASD, el joystick táctil o la palanca izquierda en VR. Usa las pestañas del panel para entrar a misiones, juegos o la paciente."};
   runtime.atlas.spokenGuide.patientSteps=new[]{"Soy Atlas. Esta es una entrevista educativa con María. Elige el caso y escucha su experiencia antes de proponer conclusiones.","Mantén el botón Hablar o espacio mientras hablas, o A en realidad virtual, y suelta para enviar. El indicador distingue tu turno, pensamiento y respuesta de María.","Comienza con un saludo y una pregunta abierta, por ejemplo, hola María, qué te trae hoy. Respeta su ritmo y explora cómo afecta lo que vive a su vida cotidiana.","Termina y evalúa para ver porcentajes, evidencias y recomendaciones. Los resultados de práctica no se envían a Nexus sin una sesión evaluada."};
   EditorUtility.SetDirty(runtime.atlas.spokenGuide);EditorUtility.SetDirty(runtime.atlas.patientSimulation.office);
  }
  if(!UnityEngine.Object.FindFirstObjectByType<Light>()){var light=new GameObject("WEB · Luz de referencia",typeof(Light));light.GetComponent<Light>().type=LightType.Directional;light.transform.rotation=Quaternion.Euler(45,-30,0);}
  EditorSceneManager.MarkSceneDirty(web.scene);EditorSceneManager.SaveOpenScenes();
 }
 static WebXRController Controller(Transform parent,WebXRControllerHand hand){
  var root=new GameObject("WEB · Control "+hand);root.transform.SetParent(parent,false);var controller=root.AddComponent<WebXRController>();controller.hand=hand;
  var visual=GameObject.CreatePrimitive(PrimitiveType.Capsule);visual.name="Mando holográfico editable";visual.transform.SetParent(root.transform,false);visual.transform.localPosition=new Vector3(0,-.035f,-.055f);visual.transform.localScale=new Vector3(.035f,.055f,.035f);UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());visual.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;return controller;
 }
 static void ConfigureXR(){
  const string folder="Assets/@Xyla/Settings";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/@Xyla","Settings");
  XRGeneralSettingsPerBuildTarget perBuild;EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,out perBuild);
  if(!perBuild){perBuild=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();AssetDatabase.CreateAsset(perBuild,folder+"/AtlasWebXRTargets.asset");EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey,perBuild,true);}
  if(!perBuild.HasSettingsForBuildTarget(BuildTargetGroup.WebGL))perBuild.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.WebGL);
  if(!perBuild.HasManagerSettingsForBuildTarget(BuildTargetGroup.WebGL))perBuild.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.WebGL);
  var general=perBuild.SettingsForBuildTarget(BuildTargetGroup.WebGL);var settings=perBuild.ManagerSettingsForBuildTarget(BuildTargetGroup.WebGL);
  var loader=AssetDatabase.LoadAssetAtPath<WebXRLoader>(folder+"/AtlasWebXRLoader.asset");if(!loader){loader=ScriptableObject.CreateInstance<WebXRLoader>();AssetDatabase.CreateAsset(loader,folder+"/AtlasWebXRLoader.asset");}
  if(!settings.activeLoaders.Contains(loader)&&!settings.TryAddLoader(loader))throw new InvalidOperationException("No se pudo asignar WebXR.");
  settings.automaticLoading=true;settings.automaticRunning=true;general.AssignedSettings=settings;general.InitManagerOnStart=true;perBuild.SetSettingsForBuildTarget(BuildTargetGroup.WebGL,general);
  var standalone=perBuild.SettingsForBuildTarget(BuildTargetGroup.Standalone);if(standalone){standalone.InitManagerOnStart=false;EditorUtility.SetDirty(standalone);}
  var web=WebXRSettings.GetSettings();if(!web){web=ScriptableObject.CreateInstance<WebXRSettings>();AssetDatabase.CreateAsset(web,folder+"/AtlasWebXRSettings.asset");EditorBuildSettings.AddConfigObject("WebXR.Settings",web,true);}
  web.AutoLoadWebXRManager=false;web.AutoLoadWebXRInputSystem=false;web.DisableXRDisplaySubsystem=false;web.UseFramebufferScaleFactor=true;web.FramebufferScaleFactor=.85f;web.VRRequiredReferenceSpace=WebXRSettings.ReferenceSpaceTypes.local_floor;
  foreach(var asset in new UnityEngine.Object[]{perBuild,general,settings,loader,web})EditorUtility.SetDirty(asset);
 }
 [MenuItem("Atlas Web/Compilar Web PC, Celular y WebVR")]
 public static void Build(){Guard();Apply();var report=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes,"Builds/AtlasPages",BuildTarget.WebGL,BuildOptions.None);if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Build web falló.");}
}
}

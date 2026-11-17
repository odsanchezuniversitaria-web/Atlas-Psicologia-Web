using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace Xyla {
public sealed class QuestAtlasTransitions : MonoBehaviour {
 public static QuestAtlasTransitions Current {get;private set;}
 [Header("Referencias editables")] public Canvas canvas; public CanvasGroup group; public Image veil; public Text label;
 [Header("Cambio de escena")][Range(.08f,.5f)] public float fadeOutSeconds=.16f;
 [Range(.08f,.5f)] public float fadeInSeconds=.22f;
 [Range(.25f,1f)]public float overlayDistance=.55f;
 [Header("Modos y sistemas")][Range(.05f,.5f)]public float modeSeconds=.18f;
 [Range(.02f,.4f)]public float modeOpacity=.16f;
 [Range(.02f,.3f)]public float systemOpacity=.10f;
 Camera viewer;Coroutine animation;bool covering,loading;
 void Awake(){
  if(Current && Current!=this){Destroy(gameObject);return;}
  Current=this;DontDestroyOnLoad(gameObject);
  group.alpha=0;group.blocksRaycasts=false;group.interactable=false;
  viewer=Camera.main;SceneManager.sceneLoaded+=SceneLoaded;Application.onBeforeRender+=Align;
 }
 void Align(){
  if(!viewer)viewer=Camera.main;if(!viewer)return;
  canvas.worldCamera=viewer;
  transform.SetPositionAndRotation(viewer.transform.position+viewer.transform.forward*overlayDistance,viewer.transform.rotation);
 }
 void LateUpdate(){Align();}
 public IEnumerator Cover(string message){
  covering=loading=true;if(animation!=null)StopCoroutine(animation);label.text=message;
  yield return Fade(1f,fadeOutSeconds);
 }
 public void SetProgress(string message){if(covering)label.text=message;}
 void SceneLoaded(Scene scene,LoadSceneMode mode){viewer=Camera.main;if(covering)animation=StartCoroutine(Reveal());}
 public void CancelCover(){if(animation!=null)StopCoroutine(animation);animation=StartCoroutine(Reveal());}
 IEnumerator Reveal(){
  yield return null;yield return null;yield return Fade(0f,fadeInSeconds);
  covering=loading=false;label.text="";animation=null;
 }
 public void Pulse(bool system=false){
  if(system)QuestAtlasHaptics.Play(AtlasHapticCue.System);
  if(loading || PlayerPrefs.GetInt("Atlas.Effects",1)==0)return;
  if(animation!=null)StopCoroutine(animation);label.text="";
  group.alpha=Mathf.Max(group.alpha,system?systemOpacity:modeOpacity);
  animation=StartCoroutine(PulseFade());
 }
 IEnumerator PulseFade(){yield return Fade(0f,modeSeconds);animation=null;}
 IEnumerator Fade(float target,float seconds){
  float start=group.alpha,elapsed=0;
  while(elapsed<seconds){
   elapsed+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(elapsed/seconds);t=t*t*(3f-2f*t);
   group.alpha=Mathf.Lerp(start,target,t);Align();yield return null;
  }
  group.alpha=target;
 }
 void OnDestroy(){
  if(Current!=this)return;SceneManager.sceneLoaded-=SceneLoaded;Application.onBeforeRender-=Align;Current=null;
 }
}}

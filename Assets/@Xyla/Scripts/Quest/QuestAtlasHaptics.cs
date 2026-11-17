using UnityEngine;
namespace Xyla {
public enum AtlasHapticCue { Button, Grab, Release, System, Voice }
public sealed class QuestAtlasHaptics : MonoBehaviour {
 public static QuestAtlasHaptics Current {get;private set;}
 [Header("Vibración de los mandos · editable")][Range(0,1)] public float intensity=.65f;
 [Range(.01f,.15f)] public float buttonSeconds=.025f, grabSeconds=.065f, releaseSeconds=.025f, systemSeconds=.045f, voiceSeconds=.06f;
 [Range(0,1)] public float buttonStrength=.25f, grabStrength=.55f, releaseStrength=.18f, systemStrength=.35f, voiceStrength=.55f;
 float leftUntil,rightUntil,leftLast=-1,rightLast=-1;
 public int SentCount {get;private set;}
 void Awake(){if(Current && Current!=this){enabled=false;return;}Current=this;}
 public static void Play(AtlasHapticCue cue,OVRInput.Controller hand=OVRInput.Controller.Active){if(Current)Current.Pulse(cue,hand);}
 public void Pulse(AtlasHapticCue cue,OVRInput.Controller hand=OVRInput.Controller.Active){
  float gain=intensity*PlayerPrefs.GetFloat("Atlas.Haptics",1f);
  if(gain<=0)return;
  if(AtlasWebPlatform.Active){AtlasWebPlatform.Haptic(.35f*gain,.04f);return;}
  if(hand==OVRInput.Controller.Active){
   bool left=Pressed(OVRInput.Controller.LTouch),right=Pressed(OVRInput.Controller.RTouch);
   if(left||right){if(left)Send(cue,OVRInput.Controller.LTouch,gain);if(right)Send(cue,OVRInput.Controller.RTouch,gain);return;}
   hand=OVRInput.GetActiveController();
  }
  if(hand==OVRInput.Controller.Touch){Send(cue,OVRInput.Controller.LTouch,gain);Send(cue,OVRInput.Controller.RTouch,gain);}
  else if(hand==OVRInput.Controller.LTouch||hand==OVRInput.Controller.RTouch)Send(cue,hand,gain);
 }
 static bool Pressed(OVRInput.Controller hand){
  return OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger,hand)||OVRInput.Get(OVRInput.Button.PrimaryHandTrigger,hand)||OVRInput.Get(OVRInput.Button.One,hand);
 }
 void Send(AtlasHapticCue cue,OVRInput.Controller hand,float gain){
  if(!OVRInput.IsControllerConnected(hand)||!OVRInput.GetControllerPositionTracked(hand))return;
  bool left=hand==OVRInput.Controller.LTouch;float now=Time.unscaledTime;
  if(now-(left?leftLast:rightLast)<.035f)return;
  float seconds=buttonSeconds,strength=buttonStrength;
  switch(cue){
   case AtlasHapticCue.Grab:seconds=grabSeconds;strength=grabStrength;break;
   case AtlasHapticCue.Release:seconds=releaseSeconds;strength=releaseStrength;break;
   case AtlasHapticCue.System:seconds=systemSeconds;strength=systemStrength;break;
   case AtlasHapticCue.Voice:seconds=voiceSeconds;strength=voiceStrength;break;
  }
  OVRInput.SetControllerVibration(.55f,Mathf.Clamp01(strength*gain),hand);SentCount++;
  if(left){leftUntil=now+seconds;leftLast=now;}else{rightUntil=now+seconds;rightLast=now;}
 }
 void Update(){
  if(leftUntil>0&&Time.unscaledTime>=leftUntil){OVRInput.SetControllerVibration(0,0,OVRInput.Controller.LTouch);leftUntil=0;}
  if(rightUntil>0&&Time.unscaledTime>=rightUntil){OVRInput.SetControllerVibration(0,0,OVRInput.Controller.RTouch);rightUntil=0;}
 }
 void StopAll(){
  if(leftUntil>0)OVRInput.SetControllerVibration(0,0,OVRInput.Controller.LTouch);
  if(rightUntil>0)OVRInput.SetControllerVibration(0,0,OVRInput.Controller.RTouch);
  leftUntil=rightUntil=0;
 }
 void OnApplicationPause(bool paused){if(paused)StopAll();}
 void OnApplicationFocus(bool focused){if(!focused)StopAll();}
 void OnDisable(){StopAll();}
 void OnDestroy(){if(Current==this)Current=null;}
}}

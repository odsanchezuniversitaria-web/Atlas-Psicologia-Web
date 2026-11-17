using UnityEngine;
using UnityEngine.UI;
namespace Xyla {
public sealed class QuestClinicalReadability : MonoBehaviour {
 [Header("Panel estable · editable")] public Canvas panelCanvas;
 public Material stableMaterial;
 public bool reducePeripheralFoveation=true;
 public OVRManager.FixedFoveatedRenderingLevel readingFoveation=OVRManager.FixedFoveatedRenderingLevel.Low;
 bool heldSettings,savedDynamic; OVRManager.FixedFoveatedRenderingLevel savedFoveation;
 void OnEnable(){
  if(!Application.isPlaying || !reducePeripheralFoveation || AtlasWebPlatform.Active)return;
  savedFoveation=OVRManager.fixedFoveatedRenderingLevel;savedDynamic=OVRManager.useDynamicFixedFoveatedRendering;
  OVRManager.useDynamicFixedFoveatedRendering=false;OVRManager.fixedFoveatedRenderingLevel=readingFoveation;heldSettings=true;
 }
 public void ApplyMaterials(){
  panelCanvas.overrideSorting=true;
  foreach(var graphic in GetComponentsInChildren<Graphic>(true))graphic.material=stableMaterial;
  foreach(var text in GetComponentsInChildren<Text>(true)){
   var color=text.color;color.a=1f;text.color=color;
   var position=text.rectTransform.localPosition;position.z=-2f;text.rectTransform.localPosition=position;
  }
 }
 void OnDisable(){
  if(!heldSettings)return;OVRManager.fixedFoveatedRenderingLevel=savedFoveation;
  OVRManager.useDynamicFixedFoveatedRendering=savedDynamic;heldSettings=false;
 }
}}

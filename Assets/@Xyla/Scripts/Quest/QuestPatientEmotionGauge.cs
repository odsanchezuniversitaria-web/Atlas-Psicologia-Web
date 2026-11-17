using UnityEngine;
using UnityEngine.UI;
namespace Xyla {
public sealed class QuestPatientEmotionGauge : MonoBehaviour {
 [Header("Posición editable · al lado de la paciente")]
 public Vector3 offsetFromFloor=new Vector3(-.62f,1.07f,-.08f);
 public bool faceViewer=true;
 public Transform needlePivot;
 public QuestEmotionFaceGraphic face;
 public Text valueLabel,stateLabel;
 [Header("Colores · confianza baja a alta")]
 public Color[] colors={new Color(1,.25f,.20f),new Color(1,.60f,.06f),new Color(1,.85f,.10f),new Color(.52f,.78f,.05f),new Color(.03f,.76f,.32f)};
 Camera viewer;
 public void ShowFor(Transform subject,Camera camera) {
  if(!needlePivot || !face || !valueLabel || !stateLabel)throw new System.InvalidOperationException("Completa las referencias del medidor de confianza.");
  viewer=camera;
  Vector3 forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
  if(forward.sqrMagnitude<.001f)forward=Vector3.forward;
  Vector3 right=Vector3.Cross(Vector3.up,forward);
  transform.position=subject.position+right*offsetFromFloor.x+Vector3.up*offsetFromFloor.y+forward*offsetFromFloor.z;
  FaceViewer();gameObject.SetActive(true);
 }
 public void Hide()=>gameObject.SetActive(false);
 public void SetProgress(float displayed,int target,int delta,bool pending) {
  float value=Mathf.Clamp(displayed,0,100);
  needlePivot.localRotation=Quaternion.Euler(0,0,180-value*1.8f);
  int band=Mathf.Min(4,Mathf.FloorToInt(value/20f));
  face.SetExpression(value/50f-1,colors[band]);
  valueLabel.text=target+"%";
  string state=target<20?"Muy reservada":target<40?"Con reserva":target<60?"Construyendo vínculo":target<80?"Más confianza":"Mayor apertura";
  stateLabel.text=pending?"Analizando tu intervención…":state+(delta==0?"":" · "+(delta>0?"+":"")+delta);
 }
 void FaceViewer() {
  if(faceViewer && viewer) {
   Vector3 direction=transform.position-viewer.transform.position;
   if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.LookRotation(direction,Vector3.up);
  }
 }
 void LateUpdate()=>FaceViewer();
}
}
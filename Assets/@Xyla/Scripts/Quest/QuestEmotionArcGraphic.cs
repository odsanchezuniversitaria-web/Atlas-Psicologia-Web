using UnityEngine;
using UnityEngine.UI;
namespace Xyla {
[RequireComponent(typeof(CanvasRenderer))]
[AddComponentMenu("Atlas/Psicología/Segmento de medidor")]
public sealed class QuestEmotionArcGraphic : MaskableGraphic {
 [Range(0,180)] public float startAngle, endAngle=36;
 [Range(.1f,.95f)] public float innerRadius=.68f;
 public bool needle;
 protected override void OnPopulateMesh(VertexHelper vh) {
  vh.Clear(); Rect rect=rectTransform.rect;
  if(needle) {
   var v=UIVertex.simpleVert;v.color=color;
   v.position=new Vector3(0,-rect.height*.5f);vh.AddVert(v);
   v.position=new Vector3(rect.width,0);vh.AddVert(v);
   v.position=new Vector3(0,rect.height*.5f);vh.AddVert(v);
   vh.AddTriangle(0,1,2);return;
  }
  float radius=Mathf.Min(rect.width*.5f,rect.height), inside=radius*innerRadius;
  int steps=Mathf.Max(4,Mathf.CeilToInt((endAngle-startAngle)/3));
  for(int i=0;i<=steps;i++) {
   float a=Mathf.Lerp(startAngle,endAngle,i/(float)steps)*Mathf.Deg2Rad;
   Vector2 direction=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
   var v=UIVertex.simpleVert;v.color=color;v.position=direction*inside;vh.AddVert(v);
   v.position=direction*radius;vh.AddVert(v);
   if(i>0){int k=i*2;vh.AddTriangle(k-2,k-1,k);vh.AddTriangle(k,k-1,k+1);}
  }
 }
}
}
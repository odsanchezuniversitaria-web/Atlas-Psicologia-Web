using UnityEngine;
using UnityEngine.UI;
namespace Xyla {
[RequireComponent(typeof(CanvasRenderer))]
[AddComponentMenu("Atlas/Psicología/Expresión de medidor")]
public sealed class QuestEmotionFaceGraphic : MaskableGraphic {
 [Range(-1,1)] public float mood;
 public bool disk;
 public Color backgroundColor=new Color(.95f,.65f,.12f);
 public void SetExpression(float value,Color background) {
  if(Mathf.Abs(mood-value)<.002f && backgroundColor==background)return;
  mood=value;backgroundColor=background;SetVerticesDirty();
 }
 void Circle(VertexHelper vh,Vector2 center,float radius,Color tint) {
  int first=vh.currentVertCount;var v=UIVertex.simpleVert;v.color=tint;v.position=center;vh.AddVert(v);
  const int sides=24;
  for(int i=0;i<=sides;i++){float a=i*Mathf.PI*2/sides;v.position=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;vh.AddVert(v);if(i>0)vh.AddTriangle(first,first+i,first+i+1);}
 }
 void Stroke(VertexHelper vh,Vector2 a,Vector2 b,float width) {
  Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
  int k=vh.currentVertCount;var v=UIVertex.simpleVert;v.color=color;
  foreach(Vector2 p in new[]{a-n,a+n,b-n,b+n}){v.position=p;vh.AddVert(v);}
  vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k+2,k+1,k+3);
 }
 protected override void OnPopulateMesh(VertexHelper vh) {
  vh.Clear();float r=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.5f;
  if(disk)Circle(vh,Vector2.zero,r,backgroundColor);
  Circle(vh,new Vector2(-r*.30f,r*.20f),r*.085f,color);
  Circle(vh,new Vector2(r*.30f,r*.20f),r*.085f,color);
  Vector2 previous=Vector2.zero;
  for(int i=0;i<=16;i++) {
   float x=Mathf.Lerp(-.46f,.46f,i/16f);
   Vector2 point=new Vector2(x*r,(-.20f+mood*(x*x/.2116f-.5f)*.35f)*r);
   if(i>0)Stroke(vh,previous,point,Mathf.Max(1,r*.07f));previous=point;
  }
 }
}
}
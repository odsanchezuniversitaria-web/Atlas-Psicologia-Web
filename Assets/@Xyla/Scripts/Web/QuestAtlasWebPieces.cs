using UnityEngine;
namespace Xyla {
public sealed partial class QuestAtlasController {
 QuestAtlasPiece CreateWebPiece(int index){
  var part=data.parts[index];var root=new GameObject(part.id+" · "+SpanishName(part.name));root.transform.SetParent(transform,false);root.transform.localPosition=part.bounds.center;
  var body=root.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
  var visual=new GameObject("Malla anatómica");visual.transform.SetParent(root.transform,false);visual.transform.localPosition=-part.bounds.center;visual.AddComponent<MeshFilter>().sharedMesh=part.mesh;
  var renderer=visual.AddComponent<MeshRenderer>();var material=IsBone(part)?boneMaterial:IsBrainTissue(part)?brainMaterial:MaterialForTissue(part);
  var materials=new Material[Mathf.Max(1,part.mesh.subMeshCount)];for(int i=0;i<materials.Length;i++)materials[i]=material;renderer.sharedMaterials=materials;renderer.enabled=false;
  renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
  var collider=root.AddComponent<BoxCollider>();var size=part.bounds.size;collider.size=new Vector3(Mathf.Max(.006f,size.x),Mathf.Max(.006f,size.y),Mathf.Max(.006f,size.z));
  var piece=root.AddComponent<QuestAtlasPiece>();piece.InitializeWeb(this,index,renderer,collider);voiceContext.RegisterWeb(piece);piece.SetStandaloneVisual(false);piece.SetInteractionAvailable(part.system==ActiveSystemId&&IsSystemVisible(activeSystem));return piece;
 }
}}

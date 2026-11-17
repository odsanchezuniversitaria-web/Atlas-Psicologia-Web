using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Xyla
{
 public class AtlasController : MonoBehaviour
 {
  public AtlasData data;
  public Camera viewCamera;
  public AtlasUI ui;
  public bool Ready { get; private set; }
  public float Progress => data == null ? 0 : (float)loaded / data.parts.Length;
  public float Explosion { get; private set; }
  public bool Isolated { get; private set; }
  public bool AutoRotate { get; private set; }
  public AtlasConcept Selected { get; private set; }
  public int VisibleCount { get; private set; }
  public string ViewName { get; private set; } = "¾";
  public readonly HashSet<string> Systems = new HashSet<string>();
  public readonly HashSet<int> Selection = new HashSet<int>();
  public event Action Changed;
  Texture2D states;
  Color[] statePixels;
  Vector3[] offsets;
  MeshCollider[] colliders;
  readonly Dictionary<Collider,int> colliderIndex = new Dictionary<Collider,int>();
  readonly Dictionary<string,int> partIndex = new Dictionary<string,int>();
  readonly List<Material> materials = new List<Material>();
  readonly List<GameObject> batchObjects = new List<GameObject>();
  int loaded, lastW, lastH;
  float currentExplosion, yaw=15, pitch=0, zoom=1;
  Vector3 center, pan;
  Bounds assembledBounds, inventoryBounds;
  bool pointerActive, gestureBlocked, dragged;
  Vector2 pressPosition, previousPosition, previousMidpoint;
  float previousPinch;
  int previousTouches;
  bool dirty=true;

  void Start()
  {
   if(data == null || data.parts.Length == 0) throw new InvalidOperationException("Faltan los datos del atlas. Ejecuta Atlas Humano / Preparar aplicación.");
   Application.targetFrameRate=60;
   Screen.autorotateToPortrait=false; Screen.autorotateToPortraitUpsideDown=false;
   Screen.autorotateToLandscapeLeft=true; Screen.autorotateToLandscapeRight=true;
   Screen.orientation=ScreenOrientation.AutoRotation;
   Screen.sleepTimeout=SleepTimeout.NeverSleep;
   Physics.queriesHitBackfaces=true;
   states=new Texture2D(Mathf.NextPowerOfTwo(data.parts.Length),1,TextureFormat.RGBAFloat,false,true);
   states.filterMode=FilterMode.Point; states.wrapMode=TextureWrapMode.Clamp;
   statePixels=new Color[states.width]; offsets=new Vector3[data.parts.Length]; colliders=new MeshCollider[data.parts.Length];
   foreach(string s in AtlasSystems.Ids) if(s!="integumentary") Systems.Add(s);
   for(int i=0;i<data.parts.Length;i++) partIndex.Add(data.parts[i].id,i);
   foreach(var b in data.batches)
   {
    var go=new GameObject(b.system+" · dibujo agrupado"); go.transform.SetParent(transform,false);
    go.AddComponent<MeshFilter>().sharedMesh=b.mesh;
    var material=new Material(data.shader); material.SetColor("_Tint",AtlasSystems.ColorFor(b.system)); material.SetTexture("_PartData",states);
    var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
    materials.Add(material); batchObjects.Add(go); go.SetActive(false);
   }
   ui.Initialize(this);
  }

  void Update()
  {
   if(!Ready)
   {
    for(int n=0;n<32 && loaded<data.parts.Length;n++,loaded++)
    {
     var p=data.parts[loaded];
     var go=new GameObject(p.id+" · "+p.name); go.transform.SetParent(transform,false);
     var c=go.AddComponent<MeshCollider>(); c.sharedMesh=p.mesh; c.enabled=false;
     colliders[loaded]=c; colliderIndex[c]=loaded;
    }
    ui.SetProgress(Progress);
    if(loaded==data.parts.Length)
    {
     Ready=true; RebuildLayout(); foreach(var go in batchObjects)go.SetActive(true);
     ui.SetProgress(1); Notify();
    }
    return;
   }
   if(lastW!=Screen.width || lastH!=Screen.height)
   {
    lastW=Screen.width; lastH=Screen.height; ui.Resize(); RebuildLayout();
   }
   if(Mathf.Abs(currentExplosion-Explosion)>.0002f)
   {
    currentExplosion=Mathf.Lerp(currentExplosion,Explosion,1-Mathf.Exp(-Time.unscaledDeltaTime*9));
    if(Mathf.Abs(currentExplosion-Explosion)<.0002f)currentExplosion=Explosion;
    dirty=true;
   }
   if(dirty)ApplyState();
   HandleInput();
   if(AutoRotate && Explosion<.01f)yaw+=Time.unscaledDeltaTime*12;
   PositionCamera();
  }

  bool Visible(int i) => Isolated ? Selection.Contains(i) : Systems.Contains(data.parts[i].system);
  void ApplyState()
  {
   VisibleCount=0;
   for(int i=0;i<data.parts.Length;i++)
   {
    bool visible=Visible(i); if(visible)VisibleCount++;
    Vector3 offset=offsets[i]*currentExplosion;
    statePixels[i]=new Color(offset.x,offset.y,offset.z,visible ? (Selection.Contains(i)?2:1) : 0);
    colliders[i].enabled=visible;
    colliders[i].transform.localPosition=offset;
   }
   states.SetPixels(statePixels); states.Apply(false,false); dirty=false;
  }
  public void RebuildLayout()
  {
   if(!Ready)return;
   var ids=Enumerable.Range(0,data.parts.Length).Where(Visible).ToList();
   assembledBounds=ids.Count>0?data.parts[ids[0]].bounds:new Bounds(new Vector3(0,.85f,0),Vector3.one);
   float area=0,maxWidth=.3f;
   foreach(int i in ids)
   {
    Bounds b=data.parts[i].bounds; assembledBounds.Encapsulate(b);
    float w=Mathf.Max(.035f,b.size.x)+.04f,h=Mathf.Max(.035f,b.size.y)+.04f;
    area+=w*h; maxWidth=Mathf.Max(maxWidth,w);
   }
   float aspect=Mathf.Clamp(viewCamera.aspect,.5f,1.5f);
   float target=Mathf.Max(maxWidth,Mathf.Sqrt(area*aspect)*1.18f);
   ids.Sort((a,b)=> { int c=data.parts[b].bounds.size.y.CompareTo(data.parts[a].bounds.size.y); return c==0?string.CompareOrdinal(data.parts[a].id,data.parts[b].id):c; });
   float x=0,y=0,row=0,width=0;
   foreach(int i in ids)
   {
    var b=data.parts[i].bounds;
    float w=Mathf.Max(.035f,b.size.x)+.04f,h=Mathf.Max(.035f,b.size.y)+.04f;
    if(x>0 && x+w>target){x=0;y+=row;row=0;}
    offsets[i]=new Vector3(x+w/2,-y-h/2,0); x+=w;width=Mathf.Max(width,x);row=Mathf.Max(row,h);
   }
   float height=y+row;
   foreach(int i in ids)offsets[i]+=new Vector3(-width/2,height/2,0)-data.parts[i].bounds.center;
   inventoryBounds=new Bounds(Vector3.zero,new Vector3(Mathf.Max(.1f,width),Mathf.Max(.1f,height),.15f));
   pan=Vector3.zero; zoom=1;dirty=true;
  }
  void PositionCamera()
  {
   Bounds b=assembledBounds;
   float extY=Mathf.Lerp(b.extents.y,inventoryBounds.extents.y,currentExplosion);
   float extX=Mathf.Lerp(Mathf.Max(b.extents.x,b.extents.z),inventoryBounds.extents.x,currentExplosion);
   float radius=Mathf.Max(extY,extX/Mathf.Max(.1f,viewCamera.aspect));
   float dist=(radius/Mathf.Tan(viewCamera.fieldOfView*Mathf.Deg2Rad/2)+Mathf.Lerp(b.extents.z,0,currentExplosion))*1.13f;
   center=Vector3.Lerp(b.center,Vector3.zero,currentExplosion)+pan;
   Quaternion rot=Quaternion.Euler(pitch*(1-currentExplosion),yaw*(1-currentExplosion),0);
   viewCamera.transform.position=center+rot*Vector3.back*Mathf.Max(.045f,dist*zoom);
   viewCamera.transform.rotation=rot;
  }
  void Notify(){dirty=true;Changed?.Invoke();}
  public void ToggleSystem(string id){if(!Systems.Remove(id))Systems.Add(id);Isolated=false;Selection.Clear();Selected=null;RebuildLayout();Notify();}
  public void Preset(string name)
  {
   Systems.Clear();
   if(name=="all") foreach(string id in AtlasSystems.Ids)Systems.Add(id);
   else if(name=="skeleton")Systems.Add("skeletal");
   else if(name=="organs") foreach(string id in new[]{"cardiac","respiratory","digestive","urinary","endocrine","reproductive"})Systems.Add(id);
   else if(name!="none")Systems.Add(name);
   Isolated=false;Selection.Clear();Selected=null;RebuildLayout();Notify();
  }
  public void SetExplosion(float amount)
  {
   Explosion=Mathf.Clamp01(amount);
   if(Explosion>0){AutoRotate=false;ViewName="F";yaw=0;pitch=0;}
   if(Isolated){Isolated=false;RebuildLayout();}
   Notify();
  }
  public void SelectConcept(AtlasConcept concept)
  {
   Selected=concept;Selection.Clear();
   foreach(string id in concept.elements)if(partIndex.TryGetValue(id,out int i)){Selection.Add(i);Systems.Add(data.parts[i].system);}
   if(Selection.Count==0)throw new InvalidOperationException("Concepto sin piezas: "+concept.id);
   Isolated=false;AutoRotate=false;RebuildLayout();ui.ShowSelection();Notify();
  }
  public void SelectPart(int index)
  {
   var p=data.parts[index];
   SelectConcept(new AtlasConcept{id=p.conceptId,name=p.name,elements=new[]{p.id}});
  }
  public void ToggleIsolation()
  {
   if(Selection.Count==0)return;
   Isolated=!Isolated;Explosion=0;currentExplosion=0;RebuildLayout();Notify();
  }
  public void ClearSelection(){Isolated=false;Selection.Clear();Selected=null;RebuildLayout();Notify();}
  public void SetView(string view)
  {
   if(Explosion>.01f)return;
   ViewName=view;pitch=0;yaw=view=="F"?0:view=="L"?90:view=="P"?180:15;AutoRotate=false;pan=Vector3.zero;zoom=1;Notify();
  }
  public void ToggleRotation(){if(Explosion<.01f)AutoRotate=!AutoRotate;Notify();}
  public void ResetAtlas()
  {
   Systems.Clear();foreach(string s in AtlasSystems.Ids)if(s!="integumentary")Systems.Add(s);
   Explosion=0;currentExplosion=0;Isolated=false;Selected=null;Selection.Clear();AutoRotate=false;yaw=15;pitch=0;ViewName="¾";RebuildLayout();Notify();
  }
  public AtlasConcept[] Search(string query)
  {
   query=AtlasSpanish.Normalize(query);
   var words=query.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
   return data.concepts.Where(c=>query.Length==0
    ? new[]{"heart","brain","liver","stomach","femur","skull"}.Contains(c.name.ToLowerInvariant())
    : AtlasSpanish.Matches(c.name,words) || c.id.Equals(query,StringComparison.OrdinalIgnoreCase) || c.elements.Any(id=>id.Equals(query,StringComparison.OrdinalIgnoreCase)))
    .OrderBy(c=>AtlasSpanish.Name(c.name).Length).ThenBy(c=>AtlasSpanish.Name(c.name)).Take(80).ToArray();
  }
  void Pick(Vector2 point)
  {
   Physics.SyncTransforms();
   if(Physics.Raycast(viewCamera.ScreenPointToRay(point),out RaycastHit hit,200f) && colliderIndex.TryGetValue(hit.collider,out int i))
   {
    SelectPart(i);return;
   }
   // Small inventory pieces remain tappable within a modest screen-space radius.
   if(Explosion<.45f)return;
   float nearest=Mathf.Pow(Mathf.Max(14,Screen.height*.035f),2);int best=-1;
   for(int index=0;index<data.parts.Length;index++)
   {
    if(!Visible(index))continue;
    Vector3 projected=viewCamera.WorldToScreenPoint(data.parts[index].bounds.center+offsets[index]*currentExplosion);
    if(projected.z<=0)continue;
    float distance=((Vector2)projected-point).sqrMagnitude;
    if(distance<nearest){nearest=distance;best=index;}
   }
   if(best>=0)SelectPart(best);
  }
  void HandleInput()
  {
   if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame){ui.ClosePanels();return;}
   var ts=Touchscreen.current;
   UnityEngine.InputSystem.Controls.TouchControl first=null,second=null;
   int count=0;
   if(ts!=null)foreach(var touch in ts.touches)if(touch.press.isPressed){if(count==0)first=touch;else if(count==1)second=touch;count++;}
   if(count>=2)
   {
    Vector2 a=first.position.ReadValue(), b=second.position.ReadValue();
    float distance=Vector2.Distance(a,b);Vector2 midpoint=(a+b)*.5f;
    if(previousTouches>=2 && !gestureBlocked)
    {
     zoom=Mathf.Clamp(zoom*previousPinch/Mathf.Max(1,distance),.08f,8f);
     Vector2 delta=midpoint-previousMidpoint;
     float units=Vector3.Distance(viewCamera.transform.position,center)/Mathf.Max(1,Screen.height);
     pan-=(viewCamera.transform.right*delta.x+viewCamera.transform.up*delta.y)*units;
    }
    else gestureBlocked=ui.Hit(a)||ui.Hit(b);
    previousPinch=distance;previousMidpoint=midpoint;previousTouches=count;pointerActive=false;dragged=true;return;
   }
   if(previousTouches>=2){previousTouches=count;pointerActive=false;gestureBlocked=true;return;}
   previousTouches=count;
   bool down=false,held=false,up=false;Vector2 pos=default;
   if(ts!=null && (count==1 || ts.primaryTouch.press.wasReleasedThisFrame))
   {
    pos=ts.primaryTouch.position.ReadValue();down=ts.primaryTouch.press.wasPressedThisFrame;held=ts.primaryTouch.press.isPressed;up=ts.primaryTouch.press.wasReleasedThisFrame;
   }
   else if(Mouse.current!=null)
   {
    var mouse=Mouse.current;pos=mouse.position.ReadValue();down=mouse.leftButton.wasPressedThisFrame;held=mouse.leftButton.isPressed;up=mouse.leftButton.wasReleasedThisFrame;
    if(!ui.Hit(pos))zoom=Mathf.Clamp(zoom*Mathf.Exp(-mouse.scroll.ReadValue().y*.001f),.08f,8f);
   }
   if(down){pointerActive=true;gestureBlocked=ui.Hit(pos);dragged=false;pressPosition=previousPosition=pos;}
   if(held && pointerActive && !gestureBlocked)
   {
    Vector2 delta=pos-previousPosition;previousPosition=pos;
    if(Vector2.Distance(pos,pressPosition)>12)dragged=true;
    if(dragged)
    {
     AutoRotate=false;
     if(Explosion>.45f)pan-=new Vector3(delta.x,delta.y,0)*(Vector3.Distance(viewCamera.transform.position,center)/Mathf.Max(1,Screen.height));
     else {yaw+=delta.x*.3f;pitch=Mathf.Clamp(pitch-delta.y*.25f,-80,80);}
    }
   }
   if(up && pointerActive){if(!gestureBlocked&&!dragged)Pick(pos);pointerActive=false;}
  }
  void OnDestroy()
  {
   if(states!=null)Destroy(states);foreach(var m in materials)Destroy(m);
   Screen.sleepTimeout=SleepTimeout.SystemSetting;
  }
 }
}

using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Xyla
{
 public class AtlasUI : MonoBehaviour
 {
  public UIDocument document;
  AtlasController atlas;
  VisualElement root,header,bottom,layers,search,detail,about,loading,cameraBar;
  Label count,percent,title,description,reference,caption,loadText,separationTitle;
  Slider explosion;
  TextField searchInput;
  ScrollView results,members;
  Button isolation,systemsButton,searchButton;
  Toggle[] toggles;
  string panel="";
  string lastSelected="";
  float scale=1;
  public void Initialize(AtlasController controller)
  {
   atlas=controller;root=document.rootVisualElement;root.Clear();root.name="atlas";
   root.styleSheets.Add(Resources.Load<StyleSheet>("AtlasStyle"));root.pickingMode=PickingMode.Ignore;
   header=Box(root,"header");
   var branding=Box(header,"branding");Text(branding,"Atlas Humano","brand");Text(branding,"ANATOMÍA 3D","eyebrow");
   var tools=Box(header,"row");
   systemsButton=Button(tools,"Sistemas",()=>OpenPanel("systems"));
   searchButton=Button(tools,"Buscar",()=>{OpenPanel("search");if(panel=="search")searchInput.Focus();});
   Button(tools,"Info",()=>OpenPanel("about"));

   layers=Box(root,"panel drawer layers");
   var lh=Box(layers,"row");Text(lh,"Sistemas","section-title");Button(lh,"Cerrar",()=>ClosePanels());
   var presets=Box(layers,"row presets");Button(presets,"Todos",()=>ChoosePreset("all"));Button(presets,"Esqueleto",()=>ChoosePreset("skeleton"));Button(presets,"Órganos",()=>ChoosePreset("organs"));
   var list=new ScrollView();list.AddToClassList("system-list");layers.Add(list);
   toggles=new Toggle[AtlasSystems.Ids.Length];
   for(int i=0;i<toggles.Length;i++)
   {
    string id=AtlasSystems.Ids[i];var row=Box(list,"system-row");
    var dot=Box(row,"dot");dot.style.backgroundColor=AtlasSystems.ColorFor(id);
    var b=Button(row,AtlasSystems.Names[i],()=>ChoosePreset(id));b.AddToClassList("system-name");
    var toggle=new Toggle();toggle.tooltip="Mostrar u ocultar "+AtlasSystems.Names[i];toggle.SetValueWithoutNotify(atlas.Systems.Contains(id));
    toggle.RegisterValueChangedCallback(e=>atlas.ToggleSystem(id));row.Add(toggle);toggles[i]=toggle;
   }
   var footer=Box(layers,"row");count=Text(footer,"Preparando…","muted");Button(footer,"Ocultar todo",()=>atlas.Preset("none"));

   cameraBar=Box(root,"camera-bar");
   foreach(string v in new[]{"¾","F","L","P"})
   {
    string view=v;string label=v=="F"?"Frente":v=="L"?"Lado":v=="P"?"Atrás":"¾";
    var b=Button(cameraBar,label,()=>atlas.SetView(view));b.name="view-"+v;
   }
   var rotate=Button(cameraBar,"Girar",()=>atlas.ToggleRotation());rotate.name="rotate";
   Button(cameraBar,"Inicio",()=>{ClosePanels();atlas.ResetAtlas();});
   caption=Text(root,"Arrastra para girar · Pellizca para acercar","caption");caption.pickingMode=PickingMode.Ignore;

   bottom=Box(root,"panel bottom");
   var sliderBox=Box(bottom,"slider-box");var sliderTitle=Box(sliderBox,"row");separationTitle=Text(sliderTitle,"Separar piezas","");percent=Text(sliderTitle,"0 %","badge");
   explosion=new Slider(0,1);explosion.name="explosion";explosion.RegisterValueChangedCallback(e=>atlas.SetExplosion(e.newValue));sliderBox.Add(explosion);
   Button(bottom,"Reunir",()=>atlas.SetExplosion(0));

   search=Box(root,"panel drawer search");
   var sh=Box(search,"row");Text(sh,"Buscar estructura","section-title");Button(sh,"Cerrar",()=>ClosePanels());
   searchInput=new TextField();searchInput.name="anatomy-search";searchInput.AddToClassList("search-input");search.Add(searchInput);
   Text(search,"Busca en español: fémur, corazón…","muted");
   results=new ScrollView();results.AddToClassList("results");search.Add(results);
   searchInput.RegisterValueChangedCallback(e=>Search(e.newValue));Search("");

   detail=Box(root,"panel drawer detail");
   var dh=Box(detail,"row");Text(dh,"Selección","section-title");Button(dh,"Cerrar",()=>ClosePanels());
   var scroll=new ScrollView();scroll.AddToClassList("detail-scroll");detail.Add(scroll);
   title=Text(scroll,"","detail-title");
   description=Text(scroll,"","description");reference=Text(scroll,"","muted");members=new ScrollView();scroll.Add(members);
   isolation=Button(detail,"Aislar estructura",()=>atlas.ToggleIsolation());isolation.name="isolate";isolation.AddToClassList("primary");
   Button(detail,"Quitar selección",()=>atlas.ClearSelection());

   about=Box(root,"panel drawer about");
   var ah=Box(about,"row");Text(ah,"Acerca del atlas","section-title");Button(ah,"Cerrar",()=>ClosePanels());
   var credits=new ScrollView();about.Add(credits);
   Text(credits,"BodyParts3D 4.0","detail-title");
   Text(credits,"Anatomía humana masculina adulta. 2.234 piezas y 3.432 conceptos. Un concepto puede agrupar varias piezas. Nombres en español.","description");
   Text(credits,atlas.data.attribution,"description");
   Text(credits,"Visualizador educativo. No destinado a diagnóstico ni cirugía.","muted");
   Button(credits,"Fuente anatómica",()=>Application.OpenURL("https://dbarchive.biosciencedbc.jp/en/bodyparts3d/download.html"));

   loading=Box(root,"loading panel");loadText=Text(loading,"Preparando anatomía…","section-title");
   atlas.Changed+=Refresh;Resize();Refresh();
  }
  VisualElement Box(VisualElement parent,string classes){var e=new VisualElement();foreach(var c in classes.Split(' '))e.AddToClassList(c);e.AddToClassList("block");parent.Add(e);return e;}
  Label Text(VisualElement parent,string value,string cls){var t=new Label(value);if(cls!="")t.AddToClassList(cls);parent.Add(t);return t;}
  Button Button(VisualElement parent,string text,Action action){var b=new Button(action){text=text};parent.Add(b);return b;}
  void Display(VisualElement e,bool show){e.style.display=show?DisplayStyle.Flex:DisplayStyle.None;}
  public void SetProgress(float value){loadText.text="Preparando anatomía · "+Mathf.RoundToInt(value*100)+" %";Display(loading,value<1);}
  public void OpenPanel(string name){panel=panel==name?"":name;RenderPanels();}
  void ChoosePreset(string name){atlas.Preset(name);panel="";RenderPanels();}
  void Search(string query)
  {
   results.Clear();var matches=atlas.Search(query);
   if(matches.Length==0)Text(results,"No se encontraron estructuras.","muted");
   foreach(var c in matches)
   {
    var concept=c;var b=Button(results,AtlasSpanish.Name(c.name)+"\n"+c.elements.Length+(c.elements.Length==1?" pieza":" piezas"),()=>{panel="detail";atlas.SelectConcept(concept);});b.AddToClassList("search-result");
   }
  }
  void Refresh()
  {
   for(int i=0;i<toggles.Length;i++)toggles[i].SetValueWithoutNotify(atlas.Systems.Contains(AtlasSystems.Ids[i]));
   int visible=atlas.Isolated?atlas.Selection.Count:atlas.data.parts.Count(p=>atlas.Systems.Contains(p.system));
   count.text=visible+" piezas";percent.text=Mathf.RoundToInt(atlas.Explosion*100)+" %";explosion.SetValueWithoutNotify(atlas.Explosion);
   caption.text=atlas.Isolated?AtlasSpanish.Name(atlas.Selected.name):atlas.Explosion>.45f?"Arrastra para desplazar · Pellizca para acercar":"Arrastra para girar · Toca una estructura";
   if(atlas.Selected!=null)
   {
    string key=atlas.Selected.id+":"+string.Join(",",atlas.Selected.elements);
    if(lastSelected!=key)panel="detail";lastSelected=key;
    title.text=AtlasSpanish.Name(atlas.Selected.name);title.style.fontSize=title.text.Length>72?18:title.text.Length>40?20:24;string system=atlas.data.parts[atlas.Selection.First()].system;
    description.text=AtlasSystems.Name(system)+"\n\n"+SystemDescription(system);
    reference.text="Contexto general del sistema\n"+atlas.Selected.id+" · "+atlas.Selection.Count+(atlas.Selection.Count==1?" pieza":" piezas");
    members.Clear();
    if(atlas.Selection.Count>1)foreach(int index in atlas.Selection.Take(100)){int i=index;Button(members,AtlasSpanish.Name(atlas.data.parts[i].name),()=>atlas.SelectPart(i)).AddToClassList("search-result");}
    isolation.text=atlas.Isolated?"Ver alrededor":"Aislar estructura";
   }
   else {lastSelected="";if(panel=="detail")panel="";}
   foreach(var b in cameraBar.Children().OfType<Button>())
   {
    bool controlled=(!string.IsNullOrEmpty(b.name)&&b.name.StartsWith("view-"))||b.name=="rotate";b.SetEnabled(!controlled||atlas.Explosion<.01f);
    b.EnableInClassList("active",b.name=="view-"+atlas.ViewName||(b.name=="rotate"&&atlas.AutoRotate));
   }
   RenderPanels();
  }
  string SystemDescription(string id)
  {
   switch(id)
   {
    case "skeletal":return "Los huesos sostienen el cuerpo, protegen órganos y sirven de inserción a los músculos. El grupo incluye también dientes y estructuras asociadas.";
    case "muscular":return "Los músculos esqueléticos producen movimiento, estabilizan las articulaciones y contribuyen a mantener la postura.";
    case "nervous":return "El encéfalo, la médula espinal y los nervios procesan y transmiten señales para la sensibilidad, el movimiento y la regulación del organismo.";
    case "cardiac":return "El corazón impulsa la sangre por la circulación pulmonar y sistémica.";
    case "arterial":return "Las arterias conducen la sangre desde el corazón hacia los tejidos y los pulmones.";
    case "venous":return "Las venas devuelven la sangre al corazón.";
    case "respiratory":return "Las vías respiratorias conducen el aire hasta los pulmones, donde ocurre el intercambio gaseoso.";
    case "digestive":return "El aparato digestivo procesa los alimentos, absorbe nutrientes y elimina residuos.";
    case "urinary":return "Los riñones filtran la sangre. La orina pasa por los uréteres hasta la vejiga y sale por la uretra.";
    default:return "Estructura de la referencia BodyParts3D. Nombre y pertenencia anatómica procedentes del catálogo original.";
   }
  }
  public void Resize()
  {
   scale=Mathf.Max(.5f,Screen.height/420f);document.panelSettings.scale=scale;
   Rect safe=Screen.safeArea;
   root.style.position=Position.Absolute;root.style.left=safe.xMin/scale;root.style.right=(Screen.width-safe.xMax)/scale;
   root.style.top=(Screen.height-safe.yMax)/scale;root.style.bottom=safe.yMin/scale;
   RenderPanels();
  }
  void RenderPanels()
  {
   if(root==null)return;
   Display(layers,panel=="systems");Display(search,panel=="search");Display(about,panel=="about");Display(detail,panel=="detail");
   systemsButton.EnableInClassList("active",panel=="systems");searchButton.EnableInClassList("active",panel=="search");
   Rect safe=Screen.safeArea;
   float logicalWidth=safe.width/scale;
   float drawerWidth=Mathf.Clamp(logicalWidth*.36f,280,330);
   foreach(var drawer in new[]{layers,search,detail,about})drawer.style.width=drawerWidth;
   float right=panel==""?12:drawerWidth+24;
   bottom.style.right=right;caption.style.right=right;
   separationTitle.text=string.Empty;separationTitle.text="Separar piezas";
   float left=88*scale,low=86*scale,high=62*scale;
   atlas.viewCamera.rect=new Rect((safe.x+left)/Screen.width,(safe.y+low)/Screen.height,Mathf.Max(.1f,(safe.width-left-right*scale)/Screen.width),Mathf.Max(.1f,(safe.height-low-high)/Screen.height));
  }
  public bool Hit(Vector2 position)
  {
   if(root?.panel==null)return true;
   Vector2 point=RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(position.x,Screen.height-position.y));
   var element=root.panel.Pick(point);
   while(element!=null){if(element.ClassListContains("block"))return true;element=element.parent;}
   return false;
  }
  public void ShowSelection(){panel="detail";RenderPanels();}
  public void ClosePanels(){panel="";RenderPanels();}
  void OnDestroy(){if(atlas!=null)atlas.Changed-=Refresh;}
 }
}

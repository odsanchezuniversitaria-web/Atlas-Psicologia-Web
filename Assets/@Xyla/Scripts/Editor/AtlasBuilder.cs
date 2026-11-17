using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;

namespace Xyla.Editor
{
 public static class AtlasBuilder
 {
  const string Folder="Assets/@Xyla";
  [MenuItem("Atlas Humano/Preparar aplicación")]
  public static void Prepare()
  {
   string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
   string source=Path.GetFullPath(Path.Combine(project,"../human-atlas-main/human-atlas-main"));
   string modelDir=Path.Combine(source,"public/models");
   if(!File.Exists(Path.Combine(modelDir,"atlas.json")))throw new FileNotFoundException("No se encuentra atlas.json en "+modelDir);
   if(File.Exists(Folder+"/Data/Atlas.asset"))throw new InvalidOperationException("El atlas ya está importado. No se sobrescribe automáticamente.");
   Directory.CreateDirectory(Folder+"/Data");Directory.CreateDirectory(Folder+"/Resources");Directory.CreateDirectory(Folder+"/Scenes");
   File.WriteAllText(Folder+"/Resources/AtlasStyle.uss","#atlas { color: rgb(30,49,58); font-size: 16px; }\nLabel { white-space: normal; flex-shrink: 0; }\nButton { min-height: 44px; border-width: 0; border-radius: 10px; background-color: rgb(231,238,240); color: rgb(35,57,66); padding: 8px 13px; margin: 2px; font-size: 16px; flex-shrink: 0; }\nButton:hover { background-color: rgb(207,226,225); }\nButton:active, Button.active, Button.primary { background-color: rgb(31,91,91); color: white; }\nButton:disabled { opacity: .38; }\n.row { flex-direction: row; align-items: center; justify-content: space-between; flex-shrink: 0; }\n.panel { background-color: rgb(255,255,255); border-radius: 16px; padding: 14px; }\n.header { position: absolute; left: 16px; right: 12px; top: 8px; height: 48px; flex-direction: row; justify-content: space-between; align-items: center; }\n.branding { flex-direction: row; align-items: center; }\n.brand { font-size: 23px; -unity-font-style: bold; }\n.eyebrow { font-size: 11px; letter-spacing: 1px; color: rgb(66,103,106); margin-left: 14px; }\n.muted { font-size: 13px; color: rgb(77,95,104); margin-top: 6px; margin-bottom: 6px; }\n.section-title { font-size: 19px; -unity-font-style: bold; }\n.drawer { position: absolute; top: 64px; right: 12px; bottom: 12px; }\n.system-list { flex-grow: 1; margin-top: 7px; margin-bottom: 5px; }\n.system-row { flex-direction: row; align-items: center; min-height: 46px; flex-shrink: 0; }\n.dot { width: 7px; height: 7px; border-radius: 4px; margin-right: 7px; }\n.system-name { background-color: transparent; flex-grow: 1; font-size: 16px; -unity-text-align: middle-left; padding-left: 0; margin-left: 0; }\nToggle { width: 46px; min-height: 44px; align-items: center; justify-content: center; margin: 0; }\nToggle .unity-toggle__checkmark { width: 32px; height: 20px; border-width: 0; border-radius: 10px; background-color: rgb(200,211,215); background-image: none; }\nToggle:checked .unity-toggle__checkmark { background-color: rgb(31,115,110); }\n.presets Button { padding-left: 6px; padding-right: 6px; font-size: 14px; flex-grow: 1; }\n.camera-bar { position: absolute; top: 64px; left: 12px; width: 66px; bottom: 12px; justify-content: flex-start; }\n.camera-bar Button { font-size: 14px; padding: 6px 3px; margin-bottom: 3px; min-height: 44px; }\n.caption { position: absolute; bottom: 89px; left: 90px; right: 12px; -unity-text-align: middle-center; font-size: 13px; color: rgb(63,91,99); }\n.bottom { position: absolute; bottom: 12px; left: 90px; right: 12px; height: 70px; flex-direction: row; align-items: center; padding: 8px 12px; }\n.slider-box { flex-grow: 1; margin-right: 15px; }\nSlider { height: 30px; margin-top: 3px; margin-left: 6px; margin-right: 6px; }\nSlider .unity-base-slider__tracker { height: 4px; border-width: 0; background-color: rgb(206,220,222); border-radius: 3px; }\nSlider .unity-base-slider__dragger { width: 22px; height: 22px; border-width: 2px; border-color: white; border-radius: 11px; background-color: rgb(31,91,91); margin-top: -9px; }\n.badge { background-color: rgb(233,241,240); padding: 3px 9px; border-radius: 5px; font-size: 14px; color: rgb(31,91,91); }\n.search-input { margin-top: 9px; min-height: 44px; font-size: 18px; flex-shrink: 0; }\n.search-input .unity-text-field__input { border-width: 1px; border-color: rgb(150,178,179); border-radius: 8px; padding: 8px; background-color: rgb(245,249,249); color: rgb(30,49,58); }\n.results { flex-grow: 1; margin-top: 7px; }\n.search-result { min-height: 58px; -unity-text-align: middle-left; font-size: 16px; white-space: normal; }\n.detail-title { font-size: 24px; -unity-font-style: bold; margin-top: 7px; margin-bottom: 9px; }\n.detail-scroll { flex-grow: 1; margin-bottom: 9px; }\n.description { font-size: 16px; white-space: normal; margin-bottom: 12px; }\n.about ScrollView { flex-grow: 1; }\n.loading { position: absolute; left: 25%; right: 25%; top: 42%; align-items: center; padding: 24px; }\n.unity-scroller__low-button, .unity-scroller__high-button { display: none; }\n.unity-scroller--vertical { width: 9px; min-width: 9px; }\n");
   File.WriteAllText(Folder+"/Resources/AtlasTheme.tss","@import url(\"unity-theme://default\");");
   File.WriteAllText(Folder+"/Data/Anatomy.shader","Shader \"HumanAtlas/Anatomy\"\n{\n Properties { _Tint(\"Color\",Color)=(1,1,1,1) _PartData(\"Part data\",2D)=\"white\" {} }\n SubShader {\n Tags { \"RenderType\"=\"Opaque\" \"RenderPipeline\"=\"UniversalPipeline\" }\n Pass {\n Tags { \"LightMode\"=\"UniversalForward\" }\n Cull Off\n HLSLPROGRAM\n #pragma vertex vert\n #pragma fragment frag\n #pragma target 3.0\n #include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl\"\n TEXTURE2D(_PartData); SAMPLER(sampler_PartData);\n CBUFFER_START(UnityPerMaterial)\n float4 _Tint;\n CBUFFER_END\n struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 part:TEXCOORD1; };\n struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float state:TEXCOORD1; };\n Varyings vert(Attributes v) {\n  Varyings o;\n  float4 p=SAMPLE_TEXTURE2D_LOD(_PartData,sampler_PartData,float2(v.part.x,.5),0);\n  o.positionCS=TransformObjectToHClip(v.positionOS.xyz+p.xyz);\n  o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.state=p.w; return o;\n }\n half4 frag(Varyings i):SV_Target {\n  clip(i.state-.5);\n  float3 n=normalize(i.normalWS);\n  float diffuse=saturate(dot(n,normalize(float3(-.35,.7,-1))));\n  float soft=saturate(dot(n,normalize(float3(.5,.2,1))));\n  float3 baseColor=lerp(_Tint.rgb,float3(.35,.72,.69),saturate(i.state-1));\n  return half4(baseColor*(.52+.48*diffuse+.10*soft),1);\n }\n ENDHLSL\n }\n }\n}");
   File.Copy(Path.Combine(source,"LICENSE"),Folder+"/Data/SOURCE-LICENSE.txt",true);
   File.Copy(Path.Combine(source,"public/ATTRIBUTION.md"),Folder+"/Data/ATTRIBUTION.txt",true);
   AssetDatabase.Refresh();
   var json=JObject.Parse(File.ReadAllText(Path.Combine(modelDir,"atlas.json")));
   var partJson=(JArray)json["parts"];
   var atlas=ScriptableObject.CreateInstance<AtlasData>();atlas.name="BodyParts3D Atlas";
   atlas.parts=new AtlasPart[partJson.Count];
   atlas.concepts=json["concepts"].ToObject<AtlasConcept[]>();
   atlas.triangles=(int)json["triangles"];
   atlas.attribution="Modelos: BodyParts3D, © The Database Center for Life Science. Licencia Creative Commons Atribución 4.0 Internacional. Aplicación web original: Human Atlas, © 2026 ashemag (licencia MIT). Adaptación a Unity con geometría simplificada y nombres en español; se conservan los identificadores originales.";
   atlas.shader=AssetDatabase.LoadAssetAtPath<Shader>(Folder+"/Data/Anatomy.shader");
   AssetDatabase.CreateAsset(atlas,Folder+"/Data/Atlas.asset");
   var chunks=((JArray)json["chunks"]).Select((c,i)=>File.ReadAllBytes(Path.Combine(modelDir,"body-"+i+".bin"))).ToArray();
   int textureWidth=Mathf.NextPowerOfTwo(partJson.Count),triangles=0;
   AssetDatabase.StartAssetEditing();
   try
   {
    for(int i=0;i<partJson.Count;i++)
    {
     var p=partJson[i];byte[] bytes=chunks[(int)p["chunk"]];
     int vc=(int)p["vertexCount"],ic=(int)p["indexCount"],po=(int)p["positions"],no=(int)p["normals"],io=(int)p["indices"];
     var vertices=new Vector3[vc];var normals=new Vector3[vc];var uv=new Vector2[vc];var indices=new int[ic];
     if(po+vc*12>bytes.Length||no+vc*6>bytes.Length||io+ic*4>bytes.Length)throw new InvalidDataException("Buffer incompleto: "+p["id"]);
     for(int v=0;v<vc;v++)
     {
      vertices[v]=new Vector3(BitConverter.ToSingle(bytes,po+v*12),BitConverter.ToSingle(bytes,po+v*12+4),-BitConverter.ToSingle(bytes,po+v*12+8));
      normals[v]=new Vector3(BitConverter.ToInt16(bytes,no+v*6)/32767f,BitConverter.ToInt16(bytes,no+v*6+2)/32767f,-BitConverter.ToInt16(bytes,no+v*6+4)/32767f).normalized;
      uv[v]=new Vector2((i+.5f)/textureWidth,0);
     }
     for(int t=0;t<ic;t+=3)
     {
      indices[t]=(int)BitConverter.ToUInt32(bytes,io+t*4);
      indices[t+1]=(int)BitConverter.ToUInt32(bytes,io+(t+2)*4);
      indices[t+2]=(int)BitConverter.ToUInt32(bytes,io+(t+1)*4);
     }
     if(indices.Any(index=>index<0||index>=vc))throw new InvalidDataException("Índice inválido: "+p["id"]);
     var mesh=new Mesh{name=(string)p["id"]+" "+(string)p["name"],indexFormat=IndexFormat.UInt32};
     mesh.vertices=vertices;mesh.normals=normals;mesh.uv2=uv;mesh.triangles=indices;mesh.RecalculateBounds();
     AssetDatabase.AddObjectToAsset(mesh,atlas);
     atlas.parts[i]=new AtlasPart{id=(string)p["id"],name=(string)p["name"],conceptId=(string)p["conceptId"],system=(string)p["system"],mesh=mesh,bounds=mesh.bounds};
     triangles+=ic/3;
    }
    if(triangles!=atlas.triangles)throw new InvalidDataException("El conteo de triángulos no coincide.");
    atlas.batches=atlas.parts.GroupBy(p=>p.system).Select(group=>
    {
     var mesh=new Mesh{name=group.Key+" batch",indexFormat=IndexFormat.UInt32};
     mesh.CombineMeshes(group.Select(p=>new CombineInstance{mesh=p.mesh,transform=Matrix4x4.identity}).ToArray(),true,false);
     mesh.bounds=new Bounds(Vector3.zero,Vector3.one*100);
     AssetDatabase.AddObjectToAsset(mesh,atlas);
     return new AtlasBatch{system=group.Key,mesh=mesh};
    }).ToArray();
   }
   finally{AssetDatabase.StopAssetEditing();}
   EditorUtility.SetDirty(atlas);AssetDatabase.SaveAssets();
   CreateScene(atlas);
   File.WriteAllText(Path.Combine(project,"IMPORT-REPORT.txt"),"Imported "+atlas.parts.Length+" parts, "+atlas.concepts.Length+" concepts, "+triangles+" triangles and "+atlas.batches.Length+" render batches. All binary ranges and indices validated.");
   Debug.Log("HUMAN ATLAS: importación completa.");
  }
  public static void CreateScene(AtlasData atlas)
  {
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   var bg=new GameObject("Background Camera").AddComponent<Camera>();bg.depth=-10;bg.cullingMask=0;bg.clearFlags=CameraClearFlags.SolidColor;bg.backgroundColor=new Color(.93f,.95f,.95f);
   var light=new GameObject("Key Light").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(40,-25,0);
   var root=new GameObject("Human Atlas");
   var controller=root.AddComponent<AtlasController>();controller.data=atlas;
   var cameraGo=new GameObject("Main Camera");cameraGo.tag="MainCamera";
   var cam=cameraGo.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.93f,.95f,.95f);cam.fieldOfView=36;cam.nearClipPlane=.005f;cam.farClipPlane=200;
   cameraGo.transform.position=new Vector3(0,.9f,-3.5f);controller.viewCamera=cam;
   var panel=ScriptableObject.CreateInstance<PanelSettings>();panel.name="Atlas Panel";panel.scaleMode=PanelScaleMode.ConstantPixelSize;
   panel.themeStyleSheet=AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Folder+"/Resources/AtlasTheme.tss");
   AssetDatabase.CreateAsset(panel,Folder+"/Data/AtlasPanel.asset");
   var uiGo=new GameObject("Interface");var doc=uiGo.AddComponent<UIDocument>();doc.panelSettings=panel;
   var ui=uiGo.AddComponent<AtlasUI>();ui.document=doc;controller.ui=ui;
   EditorSceneManager.SaveScene(scene,Folder+"/Scenes/HumanAtlas.unity");
   EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Folder+"/Scenes/HumanAtlas.unity",true)};
   PlayerSettings.companyName="Human Atlas";PlayerSettings.productName="Atlas Humano";PlayerSettings.bundleVersion="1.0.1";PlayerSettings.Android.bundleVersionCode=2;
   PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.humanatlas.explorer");
   PlayerSettings.defaultInterfaceOrientation=UIOrientation.AutoRotation;
   PlayerSettings.allowedAutorotateToPortrait=false;PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
   PlayerSettings.allowedAutorotateToLandscapeLeft=true;PlayerSettings.allowedAutorotateToLandscapeRight=true;
   PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;
   PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
   PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
   PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Android,ManagedStrippingLevel.Low);
   PlayerSettings.runInBackground=true;
   AssetDatabase.SaveAssets();
  }
  [MenuItem("Atlas Humano/Compilar APK Android")]
  public static void BuildAndroid()
  {
   string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Builds"));
   Directory.CreateDirectory(output);
   var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
   {
    scenes=new[]{Folder+"/Scenes/HumanAtlas.unity"},locationPathName=Path.Combine(output,"HumanAtlas.apk"),
    target=BuildTarget.Android,options=BuildOptions.None
   });
   File.WriteAllText(Path.Combine(output,"build-report.txt"),report.summary.result+"\nBytes: "+report.summary.totalSize+"\nErrors: "+report.summary.totalErrors+"\nDuration: "+report.summary.totalTime);
   if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Falló la compilación Android: "+report.summary.result);
   Debug.Log("HUMAN ATLAS APK: "+Path.Combine(output,"HumanAtlas.apk"));
  }
 }
}
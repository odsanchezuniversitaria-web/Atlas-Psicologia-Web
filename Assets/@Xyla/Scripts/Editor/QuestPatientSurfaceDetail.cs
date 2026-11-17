using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Xyla.Editor
{
 public static class QuestPatientSurfaceDetail
 {
  const string Source="Assets/@Xyla/Models/Patient/MariaFacialSuave.asset";
  const string Output="Assets/@Xyla/Models/Patient/MariaPacienteDetalle.asset";
  const string Hdr="Assets/@Xyla/Textures/PatientOffice/ExteriorConsultorio.hdr";
  const string Sky="Assets/@Xyla/Materials/PatientOffice/ExteriorConsultorio.mat";

  [MenuItem("Atlas Humano Quest/Mejorar superficie y exterior del consultorio")]
  public static void Apply()
  {
   if(Application.isPlaying)throw new InvalidOperationException("Salir de Play Mode.");
   var patient=UnityEngine.Object.FindFirstObjectByType<QuestPatientSimulation>(FindObjectsInactive.Include);
   if(!patient || !patient.realisticDriver)throw new InvalidOperationException("Abrir HumanAtlasQuest.");
   QuestPatientFacialPolish.Apply();
   Mesh source=AssetDatabase.LoadAssetAtPath<Mesh>(Source);
   Mesh detail=Subdivide(source);
   Mesh existing=AssetDatabase.LoadAssetAtPath<Mesh>(Output);
   if(existing){EditorUtility.CopySerialized(detail,existing);UnityEngine.Object.DestroyImmediate(detail);detail=existing;EditorUtility.SetDirty(detail);}
   else AssetDatabase.CreateAsset(detail,Output);
   var driver=patient.realisticDriver;
   driver.faceAndBody.sharedMesh=detail;
   driver.mouthWeight=95f;driver.mouthGain=3.2f;driver.motionProfileVersion=4;
   PrefabUtility.RecordPrefabInstancePropertyModifications(driver.faceAndBody);
   EditorUtility.SetDirty(driver.faceAndBody);EditorUtility.SetDirty(driver);
   var importer=(TextureImporter)AssetImporter.GetAtPath(Hdr);
   importer.textureType=TextureImporterType.Default;
   importer.sRGBTexture=false;importer.mipmapEnabled=true;importer.maxTextureSize=2048;
   importer.wrapMode=TextureWrapMode.Repeat;importer.isReadable=false;
   importer.textureCompression=TextureImporterCompression.Uncompressed;
   var android=importer.GetPlatformTextureSettings("Android");
   android.overridden=true;android.maxTextureSize=2048;android.format=TextureImporterFormat.RGB9E5;
   importer.SetPlatformTextureSettings(android);importer.SaveAndReimport();
   Shader shader=Shader.Find("Skybox/Panoramic");
   if(!shader)throw new InvalidOperationException("Falta Skybox/Panoramic.");
   if(!AssetDatabase.IsValidFolder("Assets/@Xyla/Materials/PatientOffice"))AssetDatabase.CreateFolder("Assets/@Xyla/Materials","PatientOffice");
   Material material=AssetDatabase.LoadAssetAtPath<Material>(Sky);
   if(!material){material=new Material(shader){name="Exterior HDRI · consulta"};AssetDatabase.CreateAsset(material,Sky);}
   material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Hdr));
   material.SetFloat("_Exposure",.8f);material.SetFloat("_Rotation",90f);
   patient.office.consultationSkybox=material;patient.office.immersiveConsultation=true;
   var camera=Camera.main;
   var skybox=camera.GetComponent<Skybox>();
   if(!skybox){skybox=camera.gameObject.AddComponent<Skybox>();skybox.enabled=false;}
   patient.office.cameraSkybox=skybox;
   EditorUtility.SetDirty(skybox);
   EditorUtility.SetDirty(material);EditorUtility.SetDirty(patient.office);
   EditorSceneManager.MarkSceneDirty(patient.gameObject.scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();
   Debug.Log("[Atlas Clinic] Superficie suavizada: "+detail.vertexCount+" vértices, "+detail.triangles.Length/3+" triángulos; "+detail.blendShapeCount+" expresiones; HDRI 2K RGB9E5. Huesos="+driver.faceAndBody.bones.Length);
  }

  static Vector3[] SmoothNormals(Vector3[] vertices,int[][] triangles)
  {
   var sums=new Dictionary<Vector3Int,Vector3>();
   var keys=vertices.Select(v=>new Vector3Int(Mathf.RoundToInt(v.x*100000),Mathf.RoundToInt(v.y*100000),Mathf.RoundToInt(v.z*100000))).ToArray();
   foreach(var sub in triangles)for(int i=0;i<sub.Length;i+=3)
   {
    int a=sub[i],b=sub[i+1],c=sub[i+2];
    Vector3 normal=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
    foreach(int index in new[]{a,b,c}){sums.TryGetValue(keys[index],out Vector3 prior);sums[keys[index]]=prior+normal;}
   }
   return keys.Select(k=>sums.TryGetValue(k,out Vector3 n)?n.normalized:Vector3.up).ToArray();
  }

  static BoneWeight Mix(BoneWeight a,BoneWeight b)
  {
   var weights=new Dictionary<int,float>();
   int[] bones={a.boneIndex0,a.boneIndex1,a.boneIndex2,a.boneIndex3,b.boneIndex0,b.boneIndex1,b.boneIndex2,b.boneIndex3};
   float[] values={a.weight0,a.weight1,a.weight2,a.weight3,b.weight0,b.weight1,b.weight2,b.weight3};
   for(int i=0;i<8;i++){weights.TryGetValue(bones[i],out float previous);weights[bones[i]]=previous+values[i]*.5f;}
   var top=weights.OrderByDescending(x=>x.Value).Take(4).ToArray();float sum=top.Sum(x=>x.Value);
   return new BoneWeight{boneIndex0=top[0].Key,weight0=top[0].Value/sum,
    boneIndex1=top.Length>1?top[1].Key:0,weight1=top.Length>1?top[1].Value/sum:0,
    boneIndex2=top.Length>2?top[2].Key:0,weight2=top.Length>2?top[2].Value/sum:0,
    boneIndex3=top.Length>3?top[3].Key:0,weight3=top.Length>3?top[3].Value/sum:0};
  }

  static Mesh Subdivide(Mesh source)
  {
   Vector3[] original=source.vertices;
   int[][] subs=Enumerable.Range(0,source.subMeshCount).Select(source.GetTriangles).ToArray();
   if(subs.Sum(t=>t.Length)/3*4>200000)throw new InvalidOperationException("La subdivisión excedería 200k triángulos.");
   Vector3[] normals=SmoothNormals(original,subs);
   var vertices=original.ToList();var uv=source.uv.ToList();var weights=source.boneWeights.ToList();
   var edges=new Dictionary<ulong,int>();var parents=new List<Vector2Int>();
   int Mid(int a,int b)
   {
    uint low=(uint)Mathf.Min(a,b),high=(uint)Mathf.Max(a,b);ulong key=((ulong)low<<32)|high;
    if(edges.TryGetValue(key,out int cached))return cached;
    Vector3 m=(original[a]+original[b])*.5f;
    Vector3 pa=m-Vector3.Dot(m-original[a],normals[a])*normals[a];
    Vector3 pb=m-Vector3.Dot(m-original[b],normals[b])*normals[b];
    int index=vertices.Count;
    vertices.Add(Vector3.Lerp(m,(pa+pb)*.5f,.6f));uv.Add((uv[a]+uv[b])*.5f);
    weights.Add(Mix(weights[a],weights[b]));parents.Add(new Vector2Int(a,b));edges[key]=index;return index;
   }
   var outputSubs=new int[subs.Length][];
   for(int s=0;s<subs.Length;s++)
   {
    var indices=new List<int>(subs[s].Length*4);
    for(int i=0;i<subs[s].Length;i+=3)
    {
     int a=subs[s][i],b=subs[s][i+1],c=subs[s][i+2],ab=Mid(a,b),bc=Mid(b,c),ca=Mid(c,a);
     indices.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});
    }outputSubs[s]=indices.ToArray();
   }
   var mesh=new Mesh{name="María · superficie redondeada y rostro articulado",indexFormat=IndexFormat.UInt32};
   Vector3[] positions=vertices.ToArray();Vector3[] smooth=SmoothNormals(positions,outputSubs);
   mesh.vertices=positions;mesh.normals=smooth;mesh.uv=uv.ToArray();mesh.boneWeights=weights.ToArray();mesh.bindposes=source.bindposes;
   mesh.subMeshCount=outputSubs.Length;
   for(int i=0;i<outputSubs.Length;i++)mesh.SetTriangles(outputSubs[i],i);
   mesh.RecalculateTangents();mesh.RecalculateBounds();
   for(int shape=0;shape<source.blendShapeCount;shape++)
   {
    Vector3[] delta=new Vector3[positions.Length],old=new Vector3[original.Length];
    source.GetBlendShapeFrameVertices(shape,0,old,null,null);Array.Copy(old,delta,old.Length);
    for(int i=0;i<parents.Count;i++)delta[original.Length+i]=(old[parents[i].x]+old[parents[i].y])*.5f;
    Vector3[] posed=positions.Select((v,i)=>v+delta[i]).ToArray(),posedNormals=SmoothNormals(posed,outputSubs);
    Vector3[] dn=posedNormals.Select((n,i)=>n-smooth[i]).ToArray();
    mesh.AddBlendShapeFrame(source.GetBlendShapeName(shape),100,delta,dn,null);
   }
   return mesh;
  }
 }
}

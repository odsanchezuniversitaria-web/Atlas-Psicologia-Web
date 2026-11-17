using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Xyla.Editor
{
    public static class QuestPatientFacialPolish
    {
        const string ModelPath = "Assets/@Xyla/Models/Patient/MariaRealista.fbx";
        const string MeshPath = "Assets/@Xyla/Models/Patient/MariaFacialSuave.asset";

        [MenuItem("Atlas Humano Quest/Corregir deformaciones faciales de Maria")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Salir de Play Mode antes de editar la malla.");
            var patient=UnityEngine.Object.FindFirstObjectByType<QuestPatientSimulation>(FindObjectsInactive.Include);
            if (!patient || !patient.realisticDriver) throw new InvalidOperationException("Abrir HumanAtlasQuest con Maria.");
            var importer=(ModelImporter)AssetImporter.GetAtPath(ModelPath);
            bool readable=importer.isReadable;
            importer.meshCompression=ModelImporterMeshCompression.Off;
            try
            {
                if (!readable) { importer.isReadable=true; importer.SaveAndReimport(); }
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                var renderer=model.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Mesh source=renderer.sharedMesh;
                var vertices=source.vertices;
                var world=vertices.Select(v=>renderer.transform.TransformPoint(v)).ToArray();
                Mesh output=UnityEngine.Object.Instantiate(source);
                output.name="Maria - parpadeo suave y labios";
                output.ClearBlendShapes();
                string[] names={"Hablar","ParpadeoIzq","ParpadeoDer"};
                string report="[Paciente IA] Expresiones corregidas:";
                foreach (string name in names)
                {
                    int index=source.GetBlendShapeIndex(name);
                    if (index<0) throw new InvalidOperationException("Falta expresion original: "+name);
                    var original=new Vector3[vertices.Length];
                    source.GetBlendShapeFrameVertices(index,source.GetBlendShapeFrameCount(index)-1,original,null,null);
                    var affected=Enumerable.Range(0,vertices.Length)
                        .Where(i=>renderer.transform.TransformVector(original[i]).sqrMagnitude>0.0000000001f).ToArray();
                    if (affected.Length<5) throw new InvalidOperationException("Expresion vacia: "+name);
                    Bounds region=new Bounds(world[affected[0]],Vector3.zero);
                    foreach (int i in affected) region.Encapsulate(world[i]);
                    var deltas=new Vector3[vertices.Length];
                    if (name=="Hablar")
                    {
                        // Vertical lip articulation only: remove the old forward inflation.
                        foreach (int i in affected)
                        {
                            Vector3 old=renderer.transform.TransformVector(original[i]);
                            float y=Mathf.Clamp(old.y*1.6f,-.006f,.0015f);
                            deltas[i]=renderer.transform.InverseTransformVector(Vector3.up*y);
                        }
                    }
                    else
                    {
                        Vector3 center=region.center;
                        float rx=Mathf.Clamp(region.size.x*.70f,.014f,.030f);
                        float ry=Mathf.Clamp(region.size.y*.75f,.008f,.016f);
                        float rz=Mathf.Max(.022f,region.size.z);
                        for (int i=0;i<vertices.Length;i++)
                        {
                            Vector3 d=world[i]-center;
                            if (Mathf.Abs(d.x)>rx*1.8f || Mathf.Abs(d.y)>ry*1.8f || Mathf.Abs(d.z)>rz*1.7f) continue;
                            float x=Mathf.Exp(-Mathf.Pow(d.x/rx,6));
                            float y=Mathf.Exp(-Mathf.Pow(d.y/ry,4));
                            float z=Mathf.Exp(-Mathf.Pow(d.z/rz,4));
                            // Monotonic vertical compression: never folds one eyelid through the other.
                            Vector3 movement=Vector3.up*(-d.y*.98f*x*y*z);
                            deltas[i]=renderer.transform.InverseTransformVector(movement);
                        }
                    }
                    Mesh normalPreview=UnityEngine.Object.Instantiate(source);
                    var posed=new Vector3[vertices.Length];
                    for (int i=0;i<vertices.Length;i++) posed[i]=vertices[i]+deltas[i];
                    normalPreview.vertices=posed; normalPreview.RecalculateNormals();
                    Vector3[] normalDelta=new Vector3[vertices.Length];
                    Vector3[] baseNormals=source.normals;
                    Vector3[] targetNormals=normalPreview.normals;
                    for (int i=0;i<vertices.Length;i++)
                        if (deltas[i].sqrMagnitude>0.0000000001f) normalDelta[i]=targetNormals[i]-baseNormals[i];
                    UnityEngine.Object.DestroyImmediate(normalPreview);
                    output.AddBlendShapeFrame(name,100f,deltas,normalDelta,null);
                    float max=0; int count=0;
                    for (int i=0;i<vertices.Length;i++)
                    {
                        float distance=renderer.transform.TransformVector(deltas[i]).magnitude;
                        if (distance>.00001f) count++;
                        max=Mathf.Max(max,distance);
                    }
                    report+=" "+name+"="+count+"v/"+(max*1000f).ToString("F2")+"mm centro"+region.center.ToString("F3")+";";
                }
                Mesh existing=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                if (existing)
                {
                    EditorUtility.CopySerialized(output,existing);
                    UnityEngine.Object.DestroyImmediate(output);
                    output=existing; EditorUtility.SetDirty(output);
                }
                else AssetDatabase.CreateAsset(output,MeshPath);
                var driver=patient.realisticDriver;
                driver.faceAndBody.sharedMesh=output;
                driver.mouthWeight=95f; driver.blinkWeight=100f;
                for(int i=0;i<output.blendShapeCount;i++) driver.faceAndBody.SetBlendShapeWeight(i,0);
                EditorUtility.SetDirty(driver.faceAndBody);
                EditorUtility.SetDirty(driver);
                PrefabUtility.RecordPrefabInstancePropertyModifications(driver.faceAndBody);
                EditorSceneManager.MarkSceneDirty(patient.gameObject.scene);
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();
                Debug.Log(report);
            }
            finally
            {
                if (importer.isReadable!=readable) { importer.isReadable=readable; importer.SaveAndReimport(); }
            }
        }
    }
}

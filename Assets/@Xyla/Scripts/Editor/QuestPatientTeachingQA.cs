using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace Xyla.Editor {
public static class QuestPatientTeachingQA {
 [MenuItem("Atlas Humano Quest/Validar y capturar acompañamiento")]
 public static void Run() {
  var p=UnityEngine.Object.FindFirstObjectByType<QuestPatientSimulation>(FindObjectsInactive.Include);
  var t=p.teaching;int original=t.selectedCase;
  for(int i=0;i<t.cases.Length;i++) {
   t.selectedCase=i;t.PrepareCase();
   if(p.voice.CaseId!=t.cases[i].id)throw new Exception("Caso no vinculado al protocolo.");
   t.ObserveQuestion("Entiendo, debe ser difícil");
   if(!t.emotionLabel.text.Contains((t.cases[i].initialComfort+6)+"%"))throw new Exception("Validación no sube la barra.");
   t.ObserveQuestion("Eso no es nada");
   if(!t.emotionLabel.text.Contains((t.cases[i].initialComfort-4)+"%"))throw new Exception("Invalidación no reduce la barra.");
   if(string.IsNullOrEmpty(t.adviceLabel.text))throw new Exception("Sin recomendación.");
  }
  t.selectedCase=original;t.PrepareCase();
  string folder=Path.Combine("Builds","TeachingQA");Directory.CreateDirectory(folder);
  GameObject clone=null, cameraObject=null;
  RenderTexture rt=null; var old=RenderTexture.active;
  try {
   clone=UnityEngine.Object.Instantiate(p.panelRoot);
   clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);clone.SetActive(true);
   foreach(var child in clone.GetComponentsInChildren<Transform>(true)) {
    child.gameObject.layer=31;
    if(child.name==p.feedbackPage.name)child.gameObject.SetActive(false);
    if(child.name==p.interviewPage.name)child.gameObject.SetActive(true);
   }
   cameraObject=new GameObject("QA temporal",typeof(Camera));
   cameraObject.hideFlags=HideFlags.HideAndDontSave;
   var camera=cameraObject.GetComponent<Camera>();
   camera.transform.SetPositionAndRotation(new Vector3(0,0,-2),Quaternion.identity);
   camera.orthographic=true;camera.orthographicSize=.425f;camera.cullingMask=1<<31;
   camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.008f,.015f,.025f);
   clone.GetComponent<Canvas>().worldCamera=camera;
   rt=RenderTexture.GetTemporary(1100,1000,24);camera.targetTexture=rt;
   Canvas.ForceUpdateCanvases();camera.Render();camera.Render();
   RenderTexture.active=rt;
   var image=new Texture2D(1100,1000,TextureFormat.RGB24,false);
   image.ReadPixels(new Rect(0,0,1100,1000),0,0);image.Apply();
   File.WriteAllBytes(Path.Combine(folder,"Panel-casos-animo-ayudas.png"),image.EncodeToPNG());
   UnityEngine.Object.DestroyImmediate(image);
  } finally {
   RenderTexture.active=old;
   if(cameraObject)UnityEngine.Object.DestroyImmediate(cameraObject);
   if(clone)UnityEngine.Object.DestroyImmediate(clone);
   if(rt)RenderTexture.ReleaseTemporary(rt);
   t.selectedCase=original;t.PrepareCase();
  }
  Debug.Log("[Atlas] QA: cuatro casos, protocolo, subida/bajada y ayudas verificados; captura TeachingQA.");
 }
}
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace Xyla.Editor {
public static class QuestAtlasGuideSetup {
 [MenuItem("Atlas Humano Quest/Preparar guía hablada")]
 public static void Ensure() {
 var atlas=Object.FindFirstObjectByType<QuestAtlasController>();
 if(!atlas) return;
 var guide=Object.FindFirstObjectByType<QuestAtlasSpokenGuide>(FindObjectsInactive.Include);
 if(!guide) {
 var root=new GameObject("GUÍA HABLADA · voz de Atlas · pasos editables");
 guide=root.AddComponent<QuestAtlasSpokenGuide>();
 guide.speaker=root.AddComponent<AudioSource>();guide.speaker.playOnAwake=false;guide.speaker.spatialBlend=0;guide.speaker.volume=.85f;
 var canvasObject=new GameObject("Panel de instrucciones",typeof(RectTransform),typeof(Canvas));
 canvasObject.transform.SetParent(root.transform,false);
 canvasObject.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
 var rect=canvasObject.GetComponent<RectTransform>(); rect.sizeDelta=new Vector2(760,260);rect.position=new Vector3(0,1.3f,1.25f);rect.localScale=Vector3.one*.0013f;
 var background=canvasObject.AddComponent<Image>();background.color=new Color(.015f,.055f,.09f,.93f);background.raycastTarget=false;
 var textObject=new GameObject("Paso actual",typeof(RectTransform),typeof(Text));textObject.transform.SetParent(rect,false);
 var textRect=textObject.GetComponent<RectTransform>();textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;textRect.offsetMin=new Vector2(28,20);textRect.offsetMax=new Vector2(-28,-20);
 guide.caption=textObject.GetComponent<Text>();guide.caption.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");guide.caption.fontSize=25;guide.caption.color=new Color(.7f,.95f,1f);guide.caption.alignment=TextAnchor.MiddleCenter;guide.caption.raycastTarget=false;
 }
 atlas.spokenGuide=guide;
 EditorUtility.SetDirty(atlas);EditorUtility.SetDirty(guide);
 EditorSceneManager.MarkSceneDirty(atlas.gameObject.scene);EditorSceneManager.SaveScene(atlas.gameObject.scene);
 }
}
}

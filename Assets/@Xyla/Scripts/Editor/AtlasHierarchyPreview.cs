using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
namespace Xyla.Editor {
[InitializeOnLoad] public static class AtlasHierarchyPreview {
const string RootName = "ANATOMÍA · vista de edición · no incluida en APK";
static AtlasHierarchyPreview() { EditorApplication.playModeStateChanged += state => {
 if (state != PlayModeStateChange.ExitingEditMode && state != PlayModeStateChange.EnteredEditMode) return;
 foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
 if (root.name == RootName) root.SetActive(state == PlayModeStateChange.EnteredEditMode);
}; }
[MenuItem("Atlas Humano Quest/Ver anatomía en Hierarchy")]
public static void Show() {
 if (EditorApplication.isPlayingOrWillChangePlaymode) return;
 var atlas = UnityEngine.Object.FindFirstObjectByType<QuestAtlasController>();
 if (!atlas || !atlas.data) throw new InvalidOperationException("Abre HumanAtlasQuest primero.");
 foreach (var existing in SceneManager.GetActiveScene().GetRootGameObjects())
 if (existing.name == RootName) { Selection.activeGameObject = existing; SceneView.lastActiveSceneView?.FrameSelected(); return; }
 var shader = Shader.Find("Universal Render Pipeline/Lit");
 if (!shader) throw new InvalidOperationException("Falta shader URP Lit.");
 const string folder = "Assets/@Xyla/Materials/EditorPreview";
 System.IO.Directory.CreateDirectory(folder); AssetDatabase.Refresh();
 var root = new GameObject(RootName); root.tag = "EditorOnly";
 Undo.RegisterCreatedObjectUndo(root,"Mostrar anatomía en Hierarchy");
 root.transform.position = atlas.transform.position;
 root.transform.rotation = atlas.transform.rotation;
 root.transform.localScale = atlas.transform.lossyScale;
 var groups = new Dictionary<string,Transform>();
 var mats = new Dictionary<string,Material>();
 foreach (string id in AtlasSystems.Ids) {
 var group = new GameObject(AtlasSystems.Name(id)); group.transform.SetParent(root.transform,false); groups[id] = group.transform;
 string path = folder + "/" + id + ".mat";
 var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
 if (!mat) { mat = new Material(shader); mat.SetColor("_BaseColor",AtlasSystems.ColorFor(id)); mat.SetFloat("_Smoothness",.25f); AssetDatabase.CreateAsset(mat,path); }
 mats[id] = mat;
 group.SetActive(id != "integumentary");
 }
 var isBone = typeof(QuestAtlasController).GetMethod("IsBone",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance);
 var isBrain = typeof(QuestAtlasController).GetMethod("IsBrainTissue",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance);
 foreach (var part in atlas.data.parts) {
 if (!part.mesh) continue;
 var piece = new GameObject(AtlasSpanish.Name(part.name) + " · " + part.id);
 piece.transform.SetParent(groups[part.system],false); piece.transform.localPosition = part.bounds.center;
 var mesh = new GameObject("Malla"); mesh.transform.SetParent(piece.transform,false); mesh.transform.localPosition = -part.bounds.center;
 mesh.AddComponent<MeshFilter>().sharedMesh = part.mesh;
 Material mat = mats[part.system];
 if (isBone != null && (bool)isBone.Invoke(atlas,new object[]{part})) mat = Resources.Load<Material>("CorticalBone") ?? mat;
 else if (isBrain != null && (bool)isBrain.Invoke(atlas,new object[]{part})) mat = Resources.Load<Material>("BrainTissue") ?? mat;
 var renderer = mesh.AddComponent<MeshRenderer>();
 var materials = new Material[Mathf.Max(1,part.mesh.subMeshCount)]; for (int i=0;i<materials.Length;i++) materials[i]=mat;
 renderer.sharedMaterials = materials; renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
 }
 AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(root.scene); EditorSceneManager.SaveScene(root.scene);
 Selection.activeGameObject=root; SceneView.lastActiveSceneView?.FrameSelected();
 Debug.Log("[Atlas Hierarchy] Vista de edición creada: " + atlas.data.parts.Length + " piezas. Cambios de Transform de esta vista no alteran las posiciones de la app.");
}
}
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
namespace Xyla.Editor
{
    // Offline vertex shading: no raycasts, extra draw calls or real-time shadows on Quest.
    public static class AtlasBoneSurfaceBake
    {
        [MenuItem("Atlas Quest/Hornear sombreado de huesos")]
        public static void Bake()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Ejecuta el horneado en Play Mode.");
            if (EditorPrefs.GetString("AtlasBoneBake") == "RUNNING") return;
            var data = UnityEngine.Object.FindFirstObjectByType<QuestAtlasController>().data;
            string assetPath = AssetDatabase.GetAssetPath(data);
            string backup = Application.dataPath + "/../Backups/Before-1.3.0/Atlas.asset";
            if (!System.IO.File.Exists(backup)) System.IO.File.Copy(assetPath, backup);
            var meshes = data.batches.Where(b => b.system == "skeletal").Select(b => b.mesh).ToArray();
            if (meshes.Length != 1) throw new InvalidOperationException("Revisar lotes del esqueleto antes de hornear.");
            var mesh = meshes[0];
            var vertices = mesh.vertices; var normals = mesh.normals; var parts = mesh.uv2;
            var colors = Enumerable.Repeat(new Color32(255,255,255,255),vertices.Length).ToArray();
            var cache = new Dictionary<Vector3,byte>();
            var offset = new Vector3(100, 0, 0);
            var root = new GameObject("Horneado temporal · huesos") { hideFlags = HideFlags.HideAndDontSave };
            foreach (var part in data.parts.Where(QuestAtlasController.IsBone))
            {
                var go = new GameObject(part.id) { layer = 31, hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(root.transform, false);
                go.transform.position = offset;
                go.AddComponent<MeshCollider>().sharedMesh = part.mesh;
            }
            Physics.SyncTransforms();
            int width = Mathf.NextPowerOfTwo(data.parts.Length), index = 0;
            EditorPrefs.SetString("AtlasBoneBake", "RUNNING");
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                try
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    int end = Mathf.Min(index + 1500, vertices.Length);
                    while (index < end && watch.ElapsedMilliseconds < 18)
                    {
                        int i = index++;
                        int p = Mathf.RoundToInt(parts[i].x * width - .5f);
                        if (p < 0 || p >= data.parts.Length || !QuestAtlasController.IsBone(data.parts[p])) continue;
                        byte ao;
                        if (!cache.TryGetValue(vertices[i], out ao))
                        {
                            Vector3 n = normals[i].normalized;
                            Vector3 tangent = Vector3.Cross(n, Mathf.Abs(n.y) > .9f ? Vector3.right : Vector3.up).normalized;
                            Vector3 bitangent = Vector3.Cross(n, tangent);
                            float occluded = 0f;
                            for (int sample = 0; sample < 12; sample++)
                            {
                                float u = (sample + .5f) / 12f, angle = sample * 2.39996323f;
                                Vector3 direction = n * Mathf.Sqrt(1f-u)
                                    + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * Mathf.Sqrt(u);
                                RaycastHit hit;
                                if (Physics.Raycast(vertices[i] + offset + n * .0005f, direction, out hit, .045f, 1 << 31, QueryTriggerInteraction.Ignore))
                                    occluded += 1f - hit.distance / .045f;
                            }
                            ao = (byte)Mathf.RoundToInt(Mathf.Clamp01(1f - .9f * occluded / 12f) * 255f);
                            cache[vertices[i]] = ao;
                        }
                        colors[i] = new Color32(ao,ao,ao,255);
                    }
                    EditorPrefs.SetString("AtlasBoneBakeProgress", index + "/" + vertices.Length);
                    if (index < vertices.Length) return;
                    mesh.colors32 = colors; EditorUtility.SetDirty(mesh);
                    foreach (var part in data.parts.Where(QuestAtlasController.IsBone))
                    {
                        var pc = part.mesh.vertices.Select(v => {
                            byte ao; if (!cache.TryGetValue(v, out ao)) throw new InvalidOperationException("Vértice sin hornear: " + part.id);
                            return new Color32(ao,ao,ao,255);
                        }).ToArray();
                        part.mesh.colors32 = pc; EditorUtility.SetDirty(part.mesh);
                    }
                    AssetDatabase.SaveAssets();
                    EditorPrefs.SetString("AtlasBoneBake", "PASS · " + cache.Count + " vértices únicos");
                    EditorApplication.update -= step;
                    UnityEngine.Object.Destroy(root);
                }
                catch (Exception ex)
                {
                    EditorPrefs.SetString("AtlasBoneBake", "FAIL " + ex);
                    EditorApplication.update -= step;
                    UnityEngine.Object.Destroy(root);
                }
            };
            EditorApplication.update += step;
        }
    }
}
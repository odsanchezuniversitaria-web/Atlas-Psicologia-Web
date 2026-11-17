using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Xyla.Editor
{
    /// <summary>Imports the static Higgsfield office without adding a runtime glTF dependency.</summary>
    public static class QuestPatientOfficeGlbImporter
    {
        sealed class Reader
        {
            public JObject json;
            public byte[] binary;
            public int binaryOffset;
            public string sourceDirectory;
            public string destination;
            public readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();
            public readonly Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();
            public readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
            public readonly List<Object> createdAssets = new List<Object>();
            public int triangleCount;

            public Reader(string source, string target)
            {
                byte[] bytes = File.ReadAllBytes(source);
                if (bytes.Length < 20 || BitConverter.ToUInt32(bytes, 0) != 0x46546C67 ||
                    BitConverter.ToUInt32(bytes, 4) != 2 || BitConverter.ToUInt32(bytes, 8) != bytes.Length)
                    throw new InvalidDataException("El archivo no es un GLB 2.0 completo.");
                int offset = 12;
                while (offset + 8 <= bytes.Length)
                {
                    int length = checked((int)BitConverter.ToUInt32(bytes, offset));
                    uint type = BitConverter.ToUInt32(bytes, offset + 4);
                    offset += 8;
                    if (length < 0 || length > bytes.Length - offset)
                        throw new InvalidDataException("Chunk GLB fuera de límites.");
                    if (type == 0x4E4F534A) json = JObject.Parse(Encoding.UTF8.GetString(bytes, offset, length).TrimEnd('\0', ' '));
                    else if (type == 0x004E4942) { binary = bytes; binaryOffset = offset; }
                    offset += length;
                }
                if (json == null || binary == null) throw new InvalidDataException("GLB sin JSON o geometría BIN.");
                foreach (JToken extension in (JArray)json["extensionsRequired"] ?? new JArray())
                    if ((string)extension != "KHR_materials_unlit" && (string)extension != "KHR_lights_punctual")
                        throw new NotSupportedException("Extensión glTF requerida no soportada: " + extension);
                sourceDirectory = Path.GetDirectoryName(source);
                destination = target;
            }

            JObject Accessor(int id) => (JObject)json["accessors"][id];

            int ElementOffset(JObject accessor, int element, int components, int size)
            {
                if (accessor["sparse"] != null) throw new NotSupportedException("Accessors sparse no soportados en este importador estático.");
                if (accessor["bufferView"] == null) throw new InvalidDataException("Accessor sin bufferView.");
                JObject view = (JObject)json["bufferViews"][(int)accessor["bufferView"]];
                if ((int?)view["buffer"] != 0) throw new NotSupportedException("Sólo se admite el buffer BIN incorporado.");
                int stride = (int?)view["byteStride"] ?? components * size;
                int relative = ((int?)accessor["byteOffset"] ?? 0) + element * stride;
                int length = (int)view["byteLength"];
                if (stride < components * size || relative < 0 || relative + components * size > length)
                    throw new InvalidDataException("Accessor fuera de los límites de bufferView.");
                int absolute = binaryOffset + ((int?)view["byteOffset"] ?? 0) + relative;
                if (absolute < binaryOffset || absolute + components * size > binary.Length)
                    throw new InvalidDataException("Accessor fuera de los límites BIN.");
                return absolute;
            }

            float[][] Floats(int accessorIndex, string type, int components)
            {
                JObject accessor = Accessor(accessorIndex);
                if ((string)accessor["type"] != type || (int)accessor["componentType"] != 5126)
                    throw new NotSupportedException("El atributo " + type + " debe estar exportado como float32.");
                int count = (int)accessor["count"];
                var result = new float[count][];
                for (int i = 0; i < count; i++)
                {
                    int offset = ElementOffset(accessor, i, components, 4);
                    var values = new float[components];
                    for (int j = 0; j < components; j++)
                    {
                        values[j] = BitConverter.ToSingle(binary, offset + j * 4);
                        if (float.IsNaN(values[j]) || float.IsInfinity(values[j]))
                            throw new InvalidDataException("La malla contiene coordenadas no finitas.");
                    }
                    result[i] = values;
                }
                return result;
            }

            int[] Indices(int accessorIndex)
            {
                JObject accessor = Accessor(accessorIndex);
                if ((string)accessor["type"] != "SCALAR") throw new InvalidDataException("Índices no escalares.");
                int type = (int)accessor["componentType"];
                int size = type == 5121 ? 1 : type == 5123 ? 2 : type == 5125 ? 4 : 0;
                if (size == 0) throw new NotSupportedException("Formato de índice glTF no soportado.");
                var result = new int[(int)accessor["count"]];
                for (int i = 0; i < result.Length; i++)
                {
                    int at = ElementOffset(accessor, i, 1, size);
                    result[i] = size == 1 ? binary[at] : size == 2 ? BitConverter.ToUInt16(binary, at) :
                        checked((int)BitConverter.ToUInt32(binary, at));
                }
                return result;
            }

            public Mesh Mesh(int meshIndex, int primitiveIndex)
            {
                string key = meshIndex + "_" + primitiveIndex;
                if (meshes.TryGetValue(key, out Mesh existing)) return existing;
                JObject meshJson = (JObject)json["meshes"][meshIndex];
                JObject primitive = (JObject)meshJson["primitives"][primitiveIndex];
                if (((int?)primitive["mode"] ?? 4) != 4) throw new NotSupportedException("Sólo se admiten triángulos.");
                if (primitive["extensions"]?["KHR_draco_mesh_compression"] != null)
                    throw new NotSupportedException("Exporta el GLB sin compresión Draco.");
                if (primitive["targets"] != null) throw new NotSupportedException("El consultorio debe ser estático, sin morph targets.");
                JObject attributes = (JObject)primitive["attributes"];
                float[][] positions = Floats((int)attributes["POSITION"], "VEC3", 3);
                var vertices = new Vector3[positions.Length];
                for (int i = 0; i < vertices.Length; i++) vertices[i] = new Vector3(positions[i][0], positions[i][1], -positions[i][2]);
                int[] indices;
                if (primitive["indices"] != null) indices = Indices((int)primitive["indices"]);
                else { indices = new int[vertices.Length]; for (int i = 0; i < indices.Length; i++) indices[i] = i; }
                if (indices.Length % 3 != 0) throw new InvalidDataException("La malla no contiene triángulos completos.");
                for (int i = 0; i < indices.Length; i += 3)
                {
                    for (int j = 0; j < 3; j++)
                        if (indices[i + j] < 0 || indices[i + j] >= vertices.Length) throw new InvalidDataException("Índice fuera de rango.");
                    int swap = indices[i + 1]; indices[i + 1] = indices[i + 2]; indices[i + 2] = swap;
                }
                var mesh = new Mesh { name = ((string)meshJson["name"] ?? "Malla") + "_" + primitiveIndex,
                    indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.vertices = vertices;
                mesh.triangles = indices;
                if (attributes["NORMAL"] != null)
                {
                    float[][] normals = Floats((int)attributes["NORMAL"], "VEC3", 3);
                    if (normals.Length != vertices.Length) throw new InvalidDataException("Normales y vértices no coinciden.");
                    var values = new Vector3[normals.Length];
                    for (int i = 0; i < values.Length; i++) values[i] = new Vector3(normals[i][0], normals[i][1], -normals[i][2]);
                    mesh.normals = values;
                }
                else mesh.RecalculateNormals();
                if (attributes["TEXCOORD_0"] != null)
                {
                    float[][] uv = Floats((int)attributes["TEXCOORD_0"], "VEC2", 2);
                    if (uv.Length != vertices.Length) throw new InvalidDataException("UV y vértices no coinciden.");
                    var values = new Vector2[uv.Length];
                    for (int i = 0; i < values.Length; i++) values[i] = new Vector2(uv[i][0], 1f - uv[i][1]);
                    mesh.uv = values;
                    mesh.RecalculateTangents();
                }
                mesh.RecalculateBounds();
                triangleCount += indices.Length / 3;
                AssetDatabase.CreateAsset(mesh, destination + "/Meshes/Mesh_" + key + ".asset");
                createdAssets.Add(mesh);
                meshes.Add(key, mesh);
                return mesh;
            }

            Texture2D Texture(int textureIndex)
            {
                if (textures.TryGetValue(textureIndex, out Texture2D existing)) return existing;
                JObject texture = (JObject)json["textures"][textureIndex];
                JObject image = (JObject)json["images"][(int)texture["source"]];
                string mime = (string)image["mimeType"] ?? "image/png";
                byte[] data;
                if (image["bufferView"] != null)
                {
                    JObject view = (JObject)json["bufferViews"][(int)image["bufferView"]];
                    if ((int?)view["buffer"] != 0) throw new NotSupportedException("Imagen fuera del GLB.");
                    int offset = binaryOffset + ((int?)view["byteOffset"] ?? 0);
                    data = new byte[(int)view["byteLength"]];
                    Buffer.BlockCopy(binary, offset, data, 0, data.Length);
                }
                else
                {
                    string uri = (string)image["uri"];
                    if (uri == null || !uri.StartsWith("data:")) throw new NotSupportedException("Las texturas deben estar incorporadas en el GLB.");
                    data = Convert.FromBase64String(uri.Substring(uri.IndexOf(',') + 1));
                    if (uri.StartsWith("data:image/jpeg")) mime = "image/jpeg";
                }
                string path = destination + "/Textures/Texture_" + textureIndex + (mime == "image/jpeg" ? ".jpg" : ".png");
                File.WriteAllBytes(path, data);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.maxTextureSize = 2048;
                importer.mipmapEnabled = true;
                importer.textureCompression = TextureImporterCompression.Compressed;
                var platform = importer.GetPlatformTextureSettings("Android");
                platform.overridden = true; platform.maxTextureSize = 2048;
                platform.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(platform);
                importer.SaveAndReimport();
                Texture2D result = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                textures.Add(textureIndex, result);
                return result;
            }

            public Material Material(int index)
            {
                if (materials.TryGetValue(index, out Material existing)) return existing;
                JObject source = index >= 0 ? (JObject)json["materials"][index] : new JObject();
                JObject pbr = source["pbrMetallicRoughness"] as JObject ?? new JObject();
                JArray baseFactor = pbr["baseColorFactor"] as JArray;
                bool bakedEmission = source["emissiveTexture"] != null && pbr["baseColorTexture"] == null &&
                    baseFactor != null && (float)baseFactor[0] == 0f && (float)baseFactor[1] == 0f && (float)baseFactor[2] == 0f;
                bool unlit = source["extensions"]?["KHR_materials_unlit"] != null || bakedEmission;
                Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
                if (!shader) throw new InvalidOperationException("Falta el shader URP para el consultorio.");
                var material = new Material(shader) { name = (string)source["name"] ?? "Material" + index, enableInstancing = true };
                JArray color = bakedEmission ? new JArray(1f, 1f, 1f, 1f) :
                    pbr["baseColorFactor"] as JArray ?? new JArray(1f, 1f, 1f, 1f);
                material.SetColor("_BaseColor", new Color((float)color[0], (float)color[1], (float)color[2], (float)color[3]));
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", (float?)pbr["metallicFactor"] ?? 1f);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 1f - ((float?)pbr["roughnessFactor"] ?? 1f));
                material.SetFloat("_Cull", (bool?)source["doubleSided"] == true ? 0 : 2);
                if (material.HasProperty("_EnvironmentReflections")) material.SetFloat("_EnvironmentReflections", 0f);
                if (bakedEmission)
                    material.SetTexture("_BaseMap", Texture((int)source["emissiveTexture"]["index"]));
                else if (pbr["baseColorTexture"] != null)
                    material.SetTexture("_BaseMap", Texture((int)pbr["baseColorTexture"]["index"]));
                if (pbr["metallicRoughnessTexture"] != null || source["normalTexture"] != null)
                    throw new NotSupportedException("Exporta esta versión del consultorio con materiales baseColor sólidos o textura de color; mapas PBR adicionales necesitan conversión.");
                if ((string)source["alphaMode"] == "MASK")
                {
                    material.SetFloat("_AlphaClip", 1f);
                    material.SetFloat("_Cutoff", (float?)source["alphaCutoff"] ?? .5f);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.renderQueue = (int)RenderQueue.AlphaTest;
                }
                else if ((string)source["alphaMode"] == "BLEND")
                    throw new NotSupportedException("El consultorio Quest usa ventanas abiertas y materiales opacos; elimina el cristal transparente del GLB.");
                JArray emission = source["emissiveFactor"] as JArray;
                if (!unlit && emission != null && ((float)emission[0] + (float)emission[1] + (float)emission[2]) > 0)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", new Color((float)emission[0], (float)emission[1], (float)emission[2]));
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                AssetDatabase.CreateAsset(material, destination + "/Materials/Material_" + index + ".mat");
                createdAssets.Add(material);
                materials.Add(index, material);
                return material;
            }

            public GameObject Node(int index, Transform parent, HashSet<int> ancestors)
            {
                if (!ancestors.Add(index)) throw new InvalidDataException("Ciclo en jerarquía glTF.");
                JObject node = (JObject)json["nodes"][index];
                if (node["skin"] != null) throw new NotSupportedException("El consultorio debe exportarse sin esqueletos.");
                var go = new GameObject((string)node["name"] ?? "Objeto_" + index);
                go.transform.SetParent(parent, false);
                ApplyTransform(go.transform, node);
                if (node["mesh"] != null)
                {
                    int meshIndex = (int)node["mesh"];
                    JArray primitives = (JArray)json["meshes"][meshIndex]["primitives"];
                    for (int i = 0; i < primitives.Count; i++)
                    {
                        GameObject part = primitives.Count == 1 ? go : new GameObject("Superficie_" + i);
                        if (part != go) part.transform.SetParent(go.transform, false);
                        part.AddComponent<MeshFilter>().sharedMesh = Mesh(meshIndex, i);
                        var renderer = part.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = Material((int?)primitives[i]["material"] ?? -1);
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        renderer.receiveShadows = false;
                        renderer.lightProbeUsage = LightProbeUsage.Off;
                        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                        // The room moves when an interview opens; scene static batching would bake the old transforms.
                    }
                }
                foreach (JToken child in node["children"] as JArray ?? new JArray())
                    Node((int)child, go.transform, ancestors);
                ancestors.Remove(index);
                return go;
            }
        }

        static void ApplyTransform(Transform target, JObject node)
        {
            if (node["matrix"] is JArray values)
            {
                var matrix = new Matrix4x4();
                for (int column = 0; column < 4; column++)
                    for (int row = 0; row < 4; row++) matrix[row, column] = (float)values[column * 4 + row];
                var reflect = Matrix4x4.Scale(new Vector3(1, 1, -1));
                matrix = reflect * matrix * reflect;
                Vector3 scale = new Vector3(matrix.GetColumn(0).magnitude, matrix.GetColumn(1).magnitude, matrix.GetColumn(2).magnitude);
                if (matrix.determinant < 0) scale.x = -scale.x;
                if (Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)) < .000001f)
                    throw new InvalidDataException("Transformación degenerada en " + target.name);
                Vector3 right = (Vector3)matrix.GetColumn(0) / scale.x;
                Vector3 up = (Vector3)matrix.GetColumn(1) / scale.y;
                Vector3 forward = (Vector3)matrix.GetColumn(2) / scale.z;
                if (Mathf.Abs(Vector3.Dot(right, up)) > .001f || Mathf.Abs(Vector3.Dot(up, forward)) > .001f ||
                    Mathf.Abs(Vector3.Dot(right, forward)) > .001f)
                    throw new NotSupportedException("Transformación con shear no soportada: " + target.name);
                target.localPosition = matrix.GetColumn(3);
                target.localRotation = Quaternion.LookRotation(forward, up);
                target.localScale = scale;
                return;
            }
            JArray t = node["translation"] as JArray;
            JArray r = node["rotation"] as JArray;
            JArray s = node["scale"] as JArray;
            target.localPosition = t == null ? Vector3.zero : new Vector3((float)t[0], (float)t[1], -(float)t[2]);
            target.localRotation = r == null ? Quaternion.identity : new Quaternion(-(float)r[0], -(float)r[1], (float)r[2], (float)r[3]);
            target.localScale = s == null ? Vector3.one : new Vector3((float)s[0], (float)s[1], (float)s[2]);
        }

        public static GameObject Import(string sourcePath, string outputFolder)
        {
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("No se encuentra el consultorio GLB de Higgsfield.", sourcePath);
            EnsureFolder(outputFolder);
            EnsureFolder(outputFolder + "/Meshes");
            EnsureFolder(outputFolder + "/Materials");
            EnsureFolder(outputFolder + "/Textures");
            string prefabPath = outputFolder + "/ConsultorioHiggsfield.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
                throw new InvalidOperationException("El consultorio ya está importado. Usa otra carpeta para conservar tus ajustes.");
            var reader = new Reader(sourcePath, outputFolder);
            var root = new GameObject("CONSULTORIO · Higgsfield · objetos editables");
            try
            {
                int sceneIndex = (int?)reader.json["scene"] ?? 0;
                JArray nodes = (JArray)reader.json["scenes"][sceneIndex]["nodes"];
                foreach (JToken node in nodes) reader.Node((int)node, root.transform, new HashSet<int>());
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Atlas] Consultorio importado: " + reader.triangleCount + " triángulos únicos, " +
                    reader.meshes.Count + " mallas y " + reader.materials.Count + " materiales. " + prefabPath);
                return prefab;
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

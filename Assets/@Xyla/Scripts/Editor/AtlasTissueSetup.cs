using System;
using UnityEditor;
using UnityEngine;

namespace Xyla.Editor
{
    public static class AtlasTissueSetup
    {
        const string ResourceFolder = "Assets/@Xyla/Resources/";
        const string ShaderPath = "Assets/@Xyla/Data/TissueSurface.shader";

        static readonly (string name, AtlasSurfaceKind kind, float scale, float strength)[] Materials =
        {
            ("SkeletalMuscle", AtlasSurfaceKind.Muscle, 13f, .72f),
            ("Myocardium", AtlasSurfaceKind.Myocardium, 14f, .76f),
            ("LiverTissue", AtlasSurfaceKind.Liver, 10f, .85f),
            ("KidneyTissue", AtlasSurfaceKind.Kidney, 12f, .83f),
            ("SkinTissue", AtlasSurfaceKind.Skin, 20f, .68f)
        };

        [MenuItem("Atlas Humano Quest/Actualizar materiales de tejidos")]
        public static void Ensure()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (!shader) throw new InvalidOperationException("Falta TissueSurface.shader.");
            foreach (var spec in Materials)
            {
                string texturePath = ResourceFolder + spec.name + ".png";
                int maxSize = spec.kind == AtlasSurfaceKind.Muscle || spec.kind == AtlasSurfaceKind.Skin
                    ? 512 : 1024;
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer)
                {
                    bool dirty = importer.wrapMode != TextureWrapMode.Mirror || importer.maxTextureSize != maxSize
                        || !importer.mipmapEnabled || importer.isReadable || !importer.sRGBTexture;
                    if (dirty)
                    {
                        importer.textureType = TextureImporterType.Default;
                        importer.wrapMode = TextureWrapMode.Mirror;
                        importer.maxTextureSize = maxSize;
                        importer.mipmapEnabled = true;
                        importer.isReadable = false;
                        importer.sRGBTexture = true;
                        importer.SaveAndReimport();
                    }
                }
                Texture2D map = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (!map) throw new InvalidOperationException("Falta " + texturePath);
                string materialPath = ResourceFolder + spec.name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (!material)
                {
                    material = new Material(shader) { name = spec.name };
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                if (material.shader != shader) material.shader = shader;
                material.SetTexture("_TissueMap", map);
                material.SetFloat("_TissueScale", spec.scale);
                material.SetFloat("_TissueStrength", spec.strength);
                material.SetFloat("_Smoothness", AtlasTissueProfile.Smoothness(spec.kind));
                material.SetColor("_BaseColor", AtlasTissueProfile.Color(spec.kind, "muscular"));
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }
    }
}

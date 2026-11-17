using System;
using UnityEditor;
using UnityEngine;

namespace Xyla.Editor
{
    public static class AtlasBrainTissueSetup
    {
        const string TexturePath = "Assets/@Xyla/Resources/BrainTissueRealistic.png";
        const string ShaderPath = "Assets/@Xyla/Data/BrainTissue.shader";
        const string MaterialPath = "Assets/@Xyla/Resources/BrainTissue.mat";

        [MenuItem("Atlas Humano Quest/Actualizar material del cerebro")]
        public static void Ensure()
        {
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(TexturePath) is TextureImporter importer)
            {
                TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
                bool dirty = importer.textureType != TextureImporterType.Default
                    || importer.wrapMode != TextureWrapMode.Mirror || importer.maxTextureSize != 1024
                    || !importer.mipmapEnabled || importer.isReadable || !importer.sRGBTexture
                    || !android.overridden || android.maxTextureSize != 1024
                    || android.format != TextureImporterFormat.ASTC_6x6;
                if (dirty)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.wrapMode = TextureWrapMode.Mirror;
                    importer.maxTextureSize = 1024;
                    importer.mipmapEnabled = true;
                    importer.isReadable = false;
                    importer.sRGBTexture = true;
                    android.name = "Android";
                    android.overridden = true;
                    android.maxTextureSize = 1024;
                    android.format = TextureImporterFormat.ASTC_6x6;
                    android.compressionQuality = 50;
                    android.crunchedCompression = false;
                    importer.SetPlatformTextureSettings(android);
                    importer.SaveAndReimport();
                }
            }

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Texture2D map = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (!shader || !map) throw new InvalidOperationException("Faltan el shader o el albedo del cerebro.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material)
            {
                material = new Material(shader) { name = "BrainTissue" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            if (material.shader != shader) material.shader = shader;
            material.SetTexture("_BrainMap", map);
            material.SetFloat("_BrainScale", 13f);
            material.SetFloat("_BrainStrength", 1f);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }
    }
}

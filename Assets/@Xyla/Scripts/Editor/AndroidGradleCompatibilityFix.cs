using System.IO;
using UnityEditor.Android;
using UnityEngine;

public sealed class AndroidGradleCompatibilityFix : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 10000;

    public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
    {
        string gradleRoot = Directory.GetParent(unityLibraryPath).FullName;
        string propertiesPath = Path.Combine(gradleRoot, "gradle.properties");
        if (!File.Exists(propertiesPath))
            throw new FileNotFoundException("No se encontró gradle.properties.", propertiesPath);

        string properties = File.ReadAllText(propertiesPath);
        const string enabled = "android.enableJetifier=true";
        const string disabled = "android.enableJetifier=false";

        if (properties.Contains(enabled))
        {
            properties = properties.Replace(enabled, disabled);
            File.WriteAllText(propertiesPath, properties);
            Debug.Log("[Human Atlas Quest] Jetifier desactivado para Gradle 9 y Meta XR.");
        }
    }
}
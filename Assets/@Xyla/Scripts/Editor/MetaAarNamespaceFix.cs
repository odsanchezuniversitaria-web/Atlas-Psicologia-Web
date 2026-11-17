using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public sealed class MetaAarNamespaceFix : IPreprocessBuildWithReport
{
    const string OldNamespace = "com.oculus.Integration";

    static readonly (string path, string uniqueNamespace)[] Libraries =
    {
        ("Packages/com.meta.xr.sdk.core/Plugins/AndroidOpenXR/OVRPlugin.aar", "com.oculus.Integration.core"),
        ("Packages/com.meta.xr.sdk.interaction/Runtime/Plugins/Android/InteractionSdk.aar", "com.oculus.Integration.interaction"),
        ("Packages/com.meta.xr.sdk.voice/Lib/Telemetry/Plugins/SDKTelemetry.aar", "com.oculus.Integration.telemetry")
    };

    public int callbackOrder => -1000;

    static MetaAarNamespaceFix()
    {
        EditorApplication.delayCall += PatchAll;
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.Android)
            PatchAll();
    }

public static void PatchFromMenu()
    {
        PatchAll();
    }

static void PatchAll()
    {
        foreach ((string path, string uniqueNamespace) library in Libraries)
            PatchArchive(library.path, library.uniqueNamespace);

        Debug.Log("[Human Atlas Quest] Bibliotecas Meta XR verificadas para Unity 6.4.");
    }

static bool PatchArchive(string relativePath, string uniqueNamespace)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string fullPath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("No se encontró la biblioteca Meta XR.", fullPath);

        string manifest;
        using (FileStream file = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (ZipArchive archive = new ZipArchive(file, ZipArchiveMode.Read))
        {
            ZipArchiveEntry entry = archive.GetEntry("AndroidManifest.xml");
            if (entry == null)
                throw new InvalidDataException("La biblioteca no contiene AndroidManifest.xml: " + fullPath);

            using (StreamReader reader = new StreamReader(entry.Open()))
                manifest = reader.ReadToEnd();
        }

        string uniqueDeclaration = "package=\"" + uniqueNamespace + "\"";
        if (manifest.Contains(uniqueDeclaration))
            return false;

        if (manifest.Contains("package=\"" + OldNamespace + "\""))
            throw new BuildFailedException(
                "Meta XR necesita espacios de nombres únicos para Unity 6.4. " +
                "Restaura y repara las bibliotecas desde MetaAarBackups.");

        throw new InvalidDataException("El manifiesto tiene un espacio de nombres inesperado: " + fullPath);
    }
}
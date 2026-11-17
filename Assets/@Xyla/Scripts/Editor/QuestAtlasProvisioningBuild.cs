using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Temporary debuggable build lets ADB write pairing data as the app's own UID.
// Install the normal release APK immediately afterward; it keeps app data.
public static class QuestAtlasProvisioningBuild
{
    public static void Build()
    {
        const string landing = "Assets/@Xyla/Scenes/AtlasInicio.unity";
        const string atlas = "Assets/@Xyla/Scenes/HumanAtlasQuest.unity";
        var options = new BuildPlayerOptions
        {
            scenes = new[] { landing, atlas },
            locationPathName = "Builds/HumanAtlasQuest-provisioning.apk",
            target = BuildTarget.Android,
            options = BuildOptions.Development | BuildOptions.AllowDebugging
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("Provisioning build failed: " + report.summary.result);
        Debug.Log("[Atlas Provisioning] APK created: " + options.locationPathName);
    }
}

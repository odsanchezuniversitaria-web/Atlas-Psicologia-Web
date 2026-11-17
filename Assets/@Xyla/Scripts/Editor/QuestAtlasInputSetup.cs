using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using Oculus.Interaction;
using Oculus.Interaction.Input;

namespace Xyla.Editor
{
    public static class QuestAtlasInputSetup
    {
        public static void ConfigureProfiles()
        {
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null) throw new InvalidOperationException("Faltan los ajustes OpenXR de Android.");
            int enabled = 0;
            foreach (OpenXRFeature feature in settings.GetFeatures<OpenXRFeature>())
            {
                string type = feature.GetType().Name;
                if (type == "OculusTouchControllerProfile" ||
                    type == "MetaQuestTouchPlusControllerProfile" ||
                    type == "MetaQuestTouchProControllerProfile")
                {
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                    enabled++;
                }
            }
            if (enabled != 3) throw new InvalidOperationException("No se encontraron los tres perfiles Touch.");
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Atlas Humano Quest/Corregir mandos y rayos")]
        public static void ConfigureScene()
        {
            OVRCameraRig rig = UnityEngine.Object.FindFirstObjectByType<OVRCameraRig>();
            if (rig == null) throw new InvalidOperationException("Falta OVRCameraRig en la escena.");
            
            if (!rig.TryGetComponent<Xyla.QuestAtlasInputDiagnostics>(out var diagnostics))
                rig.gameObject.AddComponent<Xyla.QuestAtlasInputDiagnostics>();
            OVRManager manager = rig.GetComponent<OVRManager>();
            manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            manager.SimultaneousHandsAndControllersEnabled = true;
            manager.launchSimultaneousHandsControllersOnStartup = true;
            manager.controllerDrivenHandPosesType = OVRManager.ControllerDrivenHandPosesType.Natural;
            EditorUtility.SetDirty(manager);
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);

            int rigReferences = 0;
            // The comprehensive locomotion prefab assigns A/X to Jump by default.
            // These buttons belong exclusively to push-to-talk in Atlas.
            foreach (Transform child in rig.GetComponentsInChildren<Transform>(true))
                if (child.name == "Jump")
                {
                    child.gameObject.SetActive(false);
                    EditorUtility.SetDirty(child.gameObject);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
                }
            foreach (OVRCameraRigRef cameraRigRef in rig.GetComponentsInChildren<OVRCameraRigRef>(true))
            {
                cameraRigRef.InjectInteractionOVRCameraRig(rig);
                EditorUtility.SetDirty(cameraRigRef);
                PrefabUtility.RecordPrefabInstancePropertyModifications(cameraRigRef);
                rigReferences++;
            }
            if (rigReferences == 0)
                throw new InvalidOperationException("El Building Block de interacciones no contiene OVRCameraRigRef.");
            Debug.Log("[Atlas Input] Referencias de cámara Meta XR conectadas: " + rigReferences);

            foreach (RayInteractor ray in rig.GetComponentsInChildren<RayInteractor>(true))
            {
                ray.MaxRayLength = 3f;
                if (ray.name == "ControllerRayInteractor")
                {
                    var raySettings = new SerializedObject(ray);
                    UnityEngine.Object oldSelector = raySettings.FindProperty("_selector").objectReferenceValue;
                    if (oldSelector is ControllerSelector)
                    {
                        var selectorSettings = new SerializedObject(oldSelector);
                        UnityEngine.Object source = selectorSettings.FindProperty("_controller").objectReferenceValue;
                        if (!ray.TryGetComponent<Xyla.QuestAtlasRaySelector>(out var selector))
                            selector = ray.gameObject.AddComponent<Xyla.QuestAtlasRaySelector>();
                        selector.Configure(source, ray);
                        ray.InjectSelector(selector);
                        EditorUtility.SetDirty(selector);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(selector);
                    }
                }
                EditorUtility.SetDirty(ray);
                PrefabUtility.RecordPrefabInstancePropertyModifications(ray);
                foreach (RayInteractorRayVisual visual in ray.GetComponentsInChildren<RayInteractorRayVisual>(true))
                {
                    SerializedObject serialized = new SerializedObject(visual);
                    serialized.FindProperty("_hideWhenNoInteractable").boolValue = false;
                    serialized.FindProperty("_maxRayVisualLength").floatValue = 3f;
                    serialized.FindProperty("_minRayVisualEndOffset").floatValue = 0.005f;
                    serialized.FindProperty("_hoverColor").colorValue = new Color(0.1f, 0.85f, 1f, 1f);
                    serialized.FindProperty("_selectColor").colorValue = new Color(1f, 0.65f, 0.12f, 1f);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(visual);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
                }
            }
            ConfigureProfiles();
            EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            EditorSceneManager.SaveScene(rig.gameObject.scene);
        }
    }
}

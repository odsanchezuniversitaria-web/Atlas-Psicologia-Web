using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Xyla.Editor
{
    public static class QuestPatientOfficeSetup
    {
        public const string SourcePath = "Assets/@Xyla/Models/PatientOffice/ConsultorioHiggsfield.glb";
        public const string OutputFolder = "Assets/@Xyla/Models/PatientOffice/Imported";
        public const string PrefabPath = OutputFolder + "/ConsultorioHiggsfield.prefab";

        [MenuItem("Atlas Humano Quest/Consultorio/Importar GLB de Higgsfield")]
        public static void Import()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab) prefab = QuestPatientOfficeGlbImporter.Import(SourcePath, OutputFolder);
            Selection.activeObject = prefab;
            Debug.Log("[Atlas] Prefab del consultorio listo. Instálalo en HumanAtlasQuest desde Consultorio/Instalar en escena.");
        }

        [MenuItem("Atlas Humano Quest/Consultorio/Instalar en escena")]
        public static void Ensure()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Sal de Play Mode antes de instalar el consultorio.");
            var patient = Object.FindFirstObjectByType<QuestPatientSimulation>(FindObjectsInactive.Include);
            if (!patient) throw new InvalidOperationException("Abre la escena HumanAtlasQuest con la paciente antes de instalar el consultorio.");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab) prefab = QuestPatientOfficeGlbImporter.Import(SourcePath, OutputFolder);

            QuestPatientOffice office = patient.GetComponentInChildren<QuestPatientOffice>(true);
            if (!office)
            {
                var controller = new GameObject("04 · Consultorio de psicología · editable", typeof(QuestPatientOffice));
                controller.transform.SetParent(patient.transform, false);
                Undo.RegisterCreatedObjectUndo(controller, "Instalar consultorio");
                office = controller.GetComponent<QuestPatientOffice>();
            }
            if (!office.environmentRoot)
            {
                GameObject environment = (GameObject)PrefabUtility.InstantiatePrefab(prefab, office.transform);
                PrefabUtility.UnpackPrefabInstance(environment, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                environment.name = "Entorno · muebles, paredes y ventana passthrough";
                office.environmentRoot = environment;
            }
            if (!office.patientAnchor)
            {
                var anchor = new GameObject("Ancla paciente · posición y orientación del asiento");
                anchor.transform.SetParent(office.environmentRoot.transform, false);
                anchor.transform.localPosition = new Vector3(0, 0, 2.42f);
                anchor.transform.localRotation = Quaternion.Euler(0, 180, 0);
                office.patientAnchor = anchor.transform;
            }
            // Migrate only the original untouched seating anchor; preserve manual layout edits.
            if ((office.patientAnchor.localPosition - new Vector3(0, 0, 2.6f)).sqrMagnitude < .000001f)
            {
                office.patientAnchor.localPosition = new Vector3(0, 0, 2.42f);
                EditorUtility.SetDirty(office.patientAnchor);
            }
            if (!office.floorOrigin)
            {
                var rig = Object.FindFirstObjectByType<OVRCameraRig>();
                if (!rig) throw new InvalidOperationException("Falta el rig Meta para alinear el suelo del consultorio.");
                office.floorOrigin = rig.trackingSpace;
            }
            // Older scenes remain valid until the patient controller receives the optional hook.
            var serialized = new SerializedObject(patient);
            SerializedProperty reference = serialized.FindProperty("office");
            if (reference == null) throw new InvalidOperationException("QuestPatientSimulation debe incluir el campo office antes de instalar.");
            reference.objectReferenceValue = office;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            office.environmentRoot.SetActive(false);
            EditorUtility.SetDirty(office);
            EditorUtility.SetDirty(patient);
            EditorSceneManager.MarkSceneDirty(patient.gameObject.scene);
            Selection.activeGameObject = office.gameObject;
            Validate(office);
            Debug.Log("[Atlas] Consultorio instalado en la jerarquía. Se muestra sólo al entrevistar y conserva el passthrough fuera de la entrevista.");
        }

        [MenuItem("Atlas Humano Quest/Consultorio/Validar en escena")]
        public static void ValidateScene()
        {
            QuestPatientOffice office = Object.FindFirstObjectByType<QuestPatientOffice>(FindObjectsInactive.Include);
            if (!office) throw new InvalidOperationException("El consultorio no está instalado en esta escena.");
            Validate(office);
        }

        static void Validate(QuestPatientOffice office)
        {
            if (!office.environmentRoot || !office.patientAnchor)
                throw new InvalidOperationException("Falta el entorno o el ancla de la paciente.");
            MeshFilter[] filters = office.environmentRoot.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0) throw new InvalidOperationException("El consultorio no tiene geometría.");
            long triangles = filters.Sum(filter => filter.sharedMesh ? (long)filter.sharedMesh.triangles.Length / 3 : 0L);
            Renderer[] renderers = office.environmentRoot.GetComponentsInChildren<Renderer>(true);
            Material[] materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Distinct().ToArray();
            if (materials.Any(material => !material || !material.shader))
                throw new InvalidOperationException("Hay materiales ausentes en el consultorio.");
            long textureBytes = materials.Select(material => material.mainTexture).Where(texture => texture).Distinct()
                .Sum(texture => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture));
            Debug.Log("[Atlas] Consultorio validado: " + filters.Length + " objetos con malla, " + triangles +
                " triángulos visibles, " + materials.Length + " materiales, " +
                (textureBytes / 1048576f).ToString("0.0") + " MiB de texturas en editor. Sin luces ni sombras adicionales.");
        }
    }
}

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Xyla;

namespace Xyla.Editor
{
    public static class QuestPatientRealismSetup
    {
        const string ScenePath = "Assets/@Xyla/Scenes/HumanAtlasQuest.unity";
        const string ModelPath = "Assets/@Xyla/Models/Patient/MariaRealista.fbx";
        const string TexturePath = "Assets/@Xyla/Models/Patient/MariaAlbedo.png";
        const string MaterialPath = "Assets/@Xyla/Materials/Patient/MariaRealista.mat";

        [MenuItem("Atlas Humano Quest/Validar paciente realista y construir APK")]
        public static void ValidateAndBuild()
        {
            EditorSceneManager.OpenScene(ScenePath);
            QuestPatientSetup.Ensure();
            var patient = UnityEngine.Object.FindFirstObjectByType<QuestPatientSimulation>(FindObjectsInactive.Include);
            if (!patient || !patient.realisticDriver)
                throw new InvalidOperationException("María realista no está vinculada al caso de psicología.");
            var driver = patient.realisticDriver;
            UpgradeMotionProfileAndRig(driver);
            var body = driver.faceAndBody;
            if (!body || !driver.headBone || !driver.spineBone || !driver.leftUpperArmBone
                || !driver.rightUpperArmBone || !driver.leftForearmBone || !driver.rightForearmBone
                || !driver.speaker)
                throw new InvalidOperationException("Faltan referencias del rig o del audio de María.");
            if (!body.sharedMaterial || !body.sharedMaterial.GetTexture("_BaseMap"))
                throw new InvalidOperationException("Falta la textura optimizada de María.");
            foreach (string expression in new[] { "Hablar", "ParpadeoIzq", "ParpadeoDer" })
                if (body.sharedMesh.GetBlendShapeIndex(expression) < 0)
                    throw new InvalidOperationException("Falta la expresión " + expression + ".");
            bool wasActive = patient.visualRoot.activeSelf;
            patient.visualRoot.SetActive(true);
            float height = body.bounds.size.y;
            patient.visualRoot.SetActive(wasActive);
            if (height < 1.25f || height > 2.2f)
                throw new InvalidOperationException("La escala de María no es apta para Quest: " + height);
            Debug.Log("[Paciente IA] Validación: altura " + height.ToString("F2")
                + " m, " + body.sharedMesh.vertexCount + " vértices, textura 1K, huesos y expresiones conectados.");
            QuestAtlasSetup.BuildApk();
        }

        [MenuItem("Atlas Humano Quest/Instalar modelo realista de María")]
        public static void Install()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var patient = UnityEngine.Object.FindFirstObjectByType<QuestPatientSimulation>(FindObjectsInactive.Include);
            if (!patient) throw new InvalidOperationException("No se encontró el caso de María en la escena.");
            if (patient.realisticDriver)
            {
                var existingDriver = patient.realisticDriver;
                UpgradeMotionProfileAndRig(existingDriver);
                if (!existingDriver.leftForearmBone) existingDriver.leftForearmBone = Bone(existingDriver.gameObject, "LeftForeArm");
                if (!existingDriver.rightForearmBone) existingDriver.rightForearmBone = Bone(existingDriver.gameObject, "RightForeArm");
                EditorUtility.SetDirty(existingDriver);
                EditorSceneManager.MarkSceneDirty(patient.gameObject.scene);
                EditorSceneManager.SaveOpenScenes();
                Debug.Log("[Paciente IA] El modelo realista ya está instalado.");
                return;
            }

            ConfigureImports();
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!asset) throw new InvalidOperationException("No se pudo importar el FBX de María.");
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(asset, patient.visualRoot.transform);
            model.name = "03 · María realista · Higgsfield · huesos editables";
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            SkinnedMeshRenderer body = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (!body || !body.sharedMesh || body.sharedMesh.blendShapeCount < 3)
                throw new InvalidOperationException("El FBX perdió la piel o las expresiones faciales.");
            body.sharedMaterial = LoadMaterial();
            var driver = model.AddComponent<QuestPatientRealisticDriver>();
            driver.patientVisualRoot = patient.visualRoot.transform;
            driver.faceAndBody = body;
            driver.headBone = Bone(model, "Head");
            driver.spineBone = Bone(model, "Spine");
            driver.leftUpperArmBone = Bone(model, "LeftArm");
            driver.rightUpperArmBone = Bone(model, "RightArm");
            driver.leftForearmBone = Bone(model, "LeftForeArm");
            driver.rightForearmBone = Bone(model, "RightForeArm");
            UpgradeMotionProfileAndRig(driver);
            driver.speaker = driver.headBone.gameObject.AddComponent<AudioSource>();
            driver.speaker.playOnAwake = false;
            driver.speaker.spatialBlend = 1f;
            patient.realisticDriver = driver;
            patient.avatar.enabled = false;

            foreach (Renderer renderer in patient.visualRoot.GetComponentsInChildren<Renderer>(true))
                if (!renderer.transform.IsChildOf(model.transform)) renderer.enabled = false;

            EditorUtility.SetDirty(patient);
            EditorUtility.SetDirty(driver);
            EditorSceneManager.MarkSceneDirty(patient.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[Paciente IA] María realista instalada: " + body.sharedMesh.vertexCount
                + " vértices, " + body.sharedMesh.blendShapeCount + " expresiones faciales.");
        }

        public static void UpgradeMotionProfileAndRig(QuestPatientRealisticDriver driver)
        {
            if (!driver.hipsBone) driver.hipsBone = Bone(driver.gameObject, "Hips");
            if (!driver.leftThighBone) driver.leftThighBone = Bone(driver.gameObject, "LeftUpLeg");
            if (!driver.rightThighBone) driver.rightThighBone = Bone(driver.gameObject, "RightUpLeg");
            if (!driver.leftCalfBone) driver.leftCalfBone = Bone(driver.gameObject, "LeftLeg");
            if (!driver.rightCalfBone) driver.rightCalfBone = Bone(driver.gameObject, "RightLeg");
            if (!driver.leftFootBone) driver.leftFootBone = Bone(driver.gameObject, "LeftFoot");
            if (!driver.rightFootBone) driver.rightFootBone = Bone(driver.gameObject, "RightFoot");
            if (!driver.leftHandBone) driver.leftHandBone = Bone(driver.gameObject, "LeftHand");
            if (!driver.rightHandBone) driver.rightHandBone = Bone(driver.gameObject, "RightHand");
            if (driver.motionProfileVersion < 3)
            {
                driver.headYawDegrees=4f; driver.gestureDegrees=7f; driver.breathingDegrees=.6f;
                driver.idleGestureDegrees=.4f; driver.listeningNodDegrees=2f;
                driver.relaxedShoulderDegrees=driver.relaxedElbowDegrees=0f;
                driver.torsoShiftCentimeters=.4f; driver.mouthWeight=70f; driver.blinkWeight=100f;
                driver.secondsBetweenBlinks=4f; driver.animationFps=45f; driver.thinkingTiltDegrees=2.5f;
                driver.greetingWaveDegrees=7f; driver.speakingNodDegrees=1.3f;
                driver.seatedInConsultation=true; driver.seatSurfaceHeight=.45f;
                driver.pelvisAboveSeat=.18f; driver.feetForward=.40f; driver.ankleAboveFloor=.12f;
                driver.motionProfileVersion=3;
            }
            if (driver.motionProfileVersion < 4)
            {
                driver.mouthWeight=95f; driver.mouthGain=3.2f; driver.motionProfileVersion=4;
            }
            EditorUtility.SetDirty(driver);
        }

        [MenuItem("Atlas Humano Quest/Diagnosticar rig y expresiones de Maria")]
        public static void DiagnoseRigAndExpressions()
        {
            GameObject asset=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!asset) throw new InvalidOperationException("Falta el modelo de María.");
            GameObject preview=UnityEngine.Object.Instantiate(asset);
            preview.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                var driver=preview.AddComponent<QuestPatientRealisticDriver>();
                driver.patientVisualRoot=preview.transform;
                driver.faceAndBody=preview.GetComponentInChildren<SkinnedMeshRenderer>(true);
                driver.headBone=Bone(preview,"Head"); driver.spineBone=Bone(preview,"Spine");
                driver.leftUpperArmBone=Bone(preview,"LeftArm"); driver.rightUpperArmBone=Bone(preview,"RightArm");
                driver.leftForearmBone=Bone(preview,"LeftForeArm"); driver.rightForearmBone=Bone(preview,"RightForeArm");
                UpgradeMotionProfileAndRig(driver);
                var mesh=driver.faceAndBody.sharedMesh;
                string shapes="";
                for (int i=0;i<mesh.blendShapeCount;i++)
                    shapes+=mesh.GetBlendShapeName(i)+"("+mesh.GetBlendShapeFrameCount(i)+" frames) ";
                Debug.Log("[Maria rig] "+mesh.vertexCount+" vertices; readable="+mesh.isReadable+"; expressions: "+shapes
                    +"; renderBounds="+driver.faceAndBody.bounds.size.ToString("F4")+"; scale="+driver.faceAndBody.transform.lossyScale.ToString("F4"));
                var baked=new Mesh();
                try
                {
                    driver.faceAndBody.BakeMesh(baked);
                    Vector3[] neutral=baked.vertices;
                    Debug.Log("[Maria rig] bakedBounds="+baked.bounds.size.ToString("F4"));
                    foreach(string expression in new[] { "Hablar","ParpadeoIzq","ParpadeoDer" })
                    {
                        int index=mesh.GetBlendShapeIndex(expression);
                        if(index<0) throw new InvalidOperationException("Falta blendshape "+expression);
                        driver.faceAndBody.SetBlendShapeWeight(index,100f);
                        driver.faceAndBody.BakeMesh(baked);
                        Vector3[] posed=baked.vertices;
                        float maximum=0f; int affected=0;
                        for(int i=0;i<posed.Length;i++)
                        {
                            float movement=driver.faceAndBody.transform.TransformDirection(posed[i]-neutral[i]).magnitude;
                            if(movement>.00001f) affected++;
                            maximum=Mathf.Max(maximum,movement);
                        }
                        driver.faceAndBody.SetBlendShapeWeight(index,0f);
                        if(affected==0) throw new InvalidOperationException("La expresión no deforma vértices: "+expression);
                        Debug.Log("[Maria rig] "+expression+": "+affected+" vertices, max displacement "+(maximum*1000f).ToString("F2")+" mm");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(baked); }
                driver.SetSeatedPose(true,.45f);
                Vector3 hip=preview.transform.InverseTransformPoint(driver.hipsBone.position);
                Vector3 lf=preview.transform.InverseTransformPoint(driver.leftFootBone.position);
                Vector3 rf=preview.transform.InverseTransformPoint(driver.rightFootBone.position);
                Vector3 lk=preview.transform.InverseTransformPoint(driver.leftCalfBone.position);
                Vector3 rk=preview.transform.InverseTransformPoint(driver.rightCalfBone.position);
                if (Mathf.Abs(hip.y-driver.seatSurfaceHeight-driver.pelvisAboveSeat)>.005f || Mathf.Abs(lf.y-.12f)>.01f || Mathf.Abs(rf.y-.12f)>.01f)
                    throw new InvalidOperationException("La pose sentada no cumple altura de cadera/pies.");
                Debug.Log("[Maria rig] Seated verified: hips="+hip.ToString("F3")+" feet="+lf.ToString("F3")+" / "+rf.ToString("F3")
                    +" knees="+lk.ToString("F3")+" / "+rk.ToString("F3")+" hands="+driver.leftHandBone.position.ToString("F3")+" / "+driver.rightHandBone.position.ToString("F3"));
            }
            finally { UnityEngine.Object.DestroyImmediate(preview); }
        }

        [MenuItem("Atlas Humano Quest/Capturar postura y rostro de Maria")]
        public static void CaptureAnimationPreview()
        {
            var source=UnityEngine.Object.FindFirstObjectByType<QuestPatientRealisticDriver>(FindObjectsInactive.Include);
            if (!source) throw new InvalidOperationException("Abre primero HumanAtlasQuest.");
            string folder=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../Builds/PatientAnimationQA"));
            System.IO.Directory.CreateDirectory(folder);
            var utility=new PreviewRenderUtility();
            Mesh facialSnapshot=null;
            var copy=UnityEngine.Object.Instantiate(source.gameObject);
            var driver=copy.GetComponent<QuestPatientRealisticDriver>();
            copy.SetActive(true);
            copy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            copy.transform.localScale=Vector3.one;
            driver.patientVisualRoot=copy.transform;
            driver.enabled=false;
            driver.SetSeatedPose(true,source.seatSurfaceHeight);
            utility.AddSingleGO(copy);
            var chairMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            chairMaterial.color=new Color(.25f,.29f,.32f);
            var floorMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMaterial.color=new Color(.1f,.12f,.15f);
            GameObject seat=GameObject.CreatePrimitive(PrimitiveType.Cube);
            seat.transform.position=new Vector3(0,source.seatSurfaceHeight-.05f,-.04f);
            seat.transform.localScale=new Vector3(.66f,.1f,.60f);
            seat.GetComponent<Renderer>().sharedMaterial=chairMaterial;
            utility.AddSingleGO(seat);
            GameObject floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position=new Vector3(0,-.015f,0);
            floor.transform.localScale=new Vector3(2f,.03f,2f);
            floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
            utility.AddSingleGO(floor);
            try
            {
                utility.camera.clearFlags=CameraClearFlags.SolidColor;
                utility.camera.backgroundColor=new Color(.08f,.10f,.13f);
                utility.camera.nearClipPlane=.01f; utility.camera.farClipPlane=20f;
                utility.camera.fieldOfView=35f;
                utility.ambientColor=new Color(.45f,.45f,.45f);
                utility.lights[0].intensity=1.4f;
                utility.lights[0].transform.rotation=Quaternion.Euler(40,-30,0);
                utility.lights[1].intensity=.8f;
                utility.lights[1].transform.rotation=Quaternion.Euler(20,150,0);
                SavePreview(utility,folder,"Maria_sitting_front",new Vector3(0,.9f,2.65f),new Vector3(0,.62f,.1f));
                SavePreview(utility,folder,"Maria_sitting_side",new Vector3(2.4f,.9f,.4f),new Vector3(0,.62f,.1f));
                // Bake each expression explicitly: consecutive Editor renders otherwise reuse the skinned pose.
                facialSnapshot=new Mesh();
                var snapshot=new GameObject("Rostro horneado temporal");
                snapshot.AddComponent<MeshFilter>().sharedMesh=facialSnapshot;
                snapshot.AddComponent<MeshRenderer>().sharedMaterials=driver.faceAndBody.sharedMaterials;
                snapshot.transform.SetPositionAndRotation(driver.faceAndBody.transform.position,driver.faceAndBody.transform.rotation);
                utility.AddSingleGO(snapshot);
                driver.faceAndBody.enabled=false;
                driver.faceAndBody.BakeMesh(facialSnapshot);
                Vector3 face=driver.headBone.position+Vector3.up*.07f;
                SavePreview(utility,folder,"Maria_face_warmup",face+Vector3.forward*.58f,face);
                SavePreview(utility,folder,"Maria_face_neutral",face+Vector3.forward*.58f,face);
                int mouth=driver.faceAndBody.sharedMesh.GetBlendShapeIndex(driver.mouthShape);
                driver.faceAndBody.SetBlendShapeWeight(mouth,driver.mouthWeight);
                driver.faceAndBody.BakeMesh(facialSnapshot);
                SavePreview(utility,folder,"Maria_face_speaking",face+Vector3.forward*.58f,face);
                driver.faceAndBody.SetBlendShapeWeight(mouth,0f);
                driver.faceAndBody.BakeMesh(facialSnapshot);
                SavePreview(utility,folder,"Maria_face_neutral",face+Vector3.forward*.58f,face);
                driver.faceAndBody.SetBlendShapeWeight(driver.faceAndBody.sharedMesh.GetBlendShapeIndex(driver.leftBlinkShape),100f);
                driver.faceAndBody.SetBlendShapeWeight(driver.faceAndBody.sharedMesh.GetBlendShapeIndex(driver.rightBlinkShape),100f);
                driver.faceAndBody.BakeMesh(facialSnapshot);
                SavePreview(utility,folder,"Maria_face_blink",face+Vector3.forward*.58f,face);
                Debug.Log("[Maria rig] Capturas guardadas en "+folder);
            }
            finally
            {
                utility.Cleanup();
                if(facialSnapshot) UnityEngine.Object.DestroyImmediate(facialSnapshot);
                UnityEngine.Object.DestroyImmediate(chairMaterial);
                UnityEngine.Object.DestroyImmediate(floorMaterial);
            }
        }

        [MenuItem("Atlas Humano Quest/Capturar consultorio y paciente")]
        public static void CaptureOfficePreview()
        {
            var office=UnityEngine.Object.FindFirstObjectByType<QuestPatientOffice>(FindObjectsInactive.Include);
            var source=UnityEngine.Object.FindFirstObjectByType<QuestPatientRealisticDriver>(FindObjectsInactive.Include);
            if(!office || !office.environmentRoot || !office.patientAnchor || !source)
                throw new InvalidOperationException("Instala el consultorio antes de capturar.");
            string folder=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../Builds/PatientAnimationQA"));
            System.IO.Directory.CreateDirectory(folder);
            var utility=new PreviewRenderUtility();
            Material previousSky=RenderSettings.skybox;
            RenderSettings.skybox=office.consultationSkybox;
            try
            {
                GameObject room=UnityEngine.Object.Instantiate(office.environmentRoot);
                room.SetActive(true);
                room.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                utility.AddSingleGO(room);
                GameObject copy=UnityEngine.Object.Instantiate(source.gameObject);
                copy.SetActive(true);
                copy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                copy.transform.localScale=Vector3.one;
                var driver=copy.GetComponent<QuestPatientRealisticDriver>();
                driver.patientVisualRoot=copy.transform; driver.enabled=false;
                driver.SetSeatedPose(true,source.seatSurfaceHeight);
                Vector3 anchor=office.environmentRoot.transform.InverseTransformPoint(office.patientAnchor.position);
                Quaternion facing=Quaternion.Inverse(office.environmentRoot.transform.rotation)*office.patientAnchor.rotation;
                copy.transform.SetPositionAndRotation(anchor,facing);
                utility.AddSingleGO(copy);
                utility.camera.clearFlags=CameraClearFlags.Skybox;
                utility.camera.gameObject.AddComponent<Skybox>().material=office.consultationSkybox;
                utility.camera.backgroundColor=new Color(.18f,.25f,.3f);
                utility.camera.nearClipPlane=.02f; utility.camera.farClipPlane=30f;
                utility.camera.fieldOfView=65f;
                utility.ambientColor=new Color(.45f,.45f,.45f);
                utility.lights[0].intensity=1.3f;
                utility.lights[0].transform.rotation=Quaternion.Euler(40,-30,0);
                utility.lights[1].intensity=.7f;
                utility.lights[1].transform.rotation=Quaternion.Euler(20,150,0);
                SavePreview(utility,folder,"Office_therapist_view",new Vector3(0,1.25f,-.10f),anchor+Vector3.up*.9f);
                utility.camera.fieldOfView=45f;
                SavePreview(utility,folder,"Office_patient_seat",new Vector3(1.2f,1.2f,1.1f),anchor+Vector3.up*.6f);
                Debug.Log("[Maria rig] Consultorio y paciente capturados en "+folder);
            }
            finally { utility.Cleanup(); RenderSettings.skybox=previousSky; }
        }

        static void SavePreview(PreviewRenderUtility utility,string folder,string name,Vector3 position,Vector3 target)
        {
            utility.camera.transform.position=position;
            utility.camera.transform.LookAt(target,Vector3.up);
            utility.BeginStaticPreview(new Rect(0,0,1024,1024));
            utility.Render(true);
            Texture2D image=utility.EndStaticPreview();
            try { System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,name+".png"),image.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        static Transform Bone(GameObject root, string name)
        {
            Transform bone = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
            if (!bone) throw new InvalidOperationException("Falta el hueso " + name + " en el FBX.");
            return bone;
        }

        static void ConfigureImports()
        {
            var texture = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (!texture) throw new InvalidOperationException("Falta la textura de María.");
            texture.mipmapEnabled = true;
            texture.maxTextureSize = 1024;
            texture.textureCompression = TextureImporterCompression.Compressed;
            texture.isReadable = false;
            var android = texture.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = TextureImporterFormat.ASTC_6x6;
            texture.SetPlatformTextureSettings(android);
            texture.SaveAndReimport();

            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Falta el FBX de María.");
            importer.importBlendShapes = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        static Material LoadMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = "María · piel y ropa 1K" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .18f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}

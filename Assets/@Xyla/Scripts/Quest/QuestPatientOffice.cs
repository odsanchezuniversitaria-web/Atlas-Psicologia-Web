using System;
using UnityEngine;

namespace Xyla
{
    /// <summary>Positions an authored, editable office once when an interview opens.</summary>
    public sealed class QuestPatientOffice : MonoBehaviour
    {
        [Header("Consultorio editable en la jerarquía")]
        public GameObject environmentRoot;
        public Transform patientAnchor;
        [Tooltip("Origen FloorLevel del rig Meta; conserva el suelo también cuando el usuario se sienta.")]
        public Transform floorOrigin;
        [Tooltip("Alinea la paciente con el asiento del consultorio. Durante la entrevista se utiliza un exterior HDRI.")]
        public bool alignPatientToChair = true;
        [Tooltip("Asientos anteriores que se ocultan mientras se muestra el consultorio.")]
        public GameObject[] legacySeating = Array.Empty<GameObject>();
        [Header("Ambiente durante la entrevista")]
        public bool hideAtlasHolograms = true;
        [Tooltip("Se encuentra una sola vez al abrir si el laboratorio se creó en tiempo de ejecución.")]
        public GameObject atlasHolographicRoot;

        [Header("Consulta inmersiva · exterior HDRI")]
        public bool immersiveConsultation = true;
        public Material consultationSkybox;
        public Skybox cameraSkybox;
        bool previousSkyboxEnabled;
        Material previousCameraSky;
        Camera consultationCamera;
        Material previousSkybox;
        CameraClearFlags previousClearFlags;
        Color previousBackground;
        OVRPassthroughLayer[] passthroughLayers;
        bool[] passthroughWasHidden;
        bool environmentCaptured;

        bool[] seatingWasActive;
        bool isOpen;
        bool capturedHolograms, hologramsWereActive;
        public bool IsOpen => isOpen;

        void Awake()
        {
            if (environmentRoot) environmentRoot.SetActive(false);
        }

        public void Enter(Camera viewer, Transform patientVisual)
        {
            if (isOpen) return;
            if (!viewer || !patientVisual || !environmentRoot || !patientAnchor)
                throw new InvalidOperationException("El consultorio necesita cámara, paciente, entorno y ancla de asiento.");
            Vector3 forward = Vector3.ProjectOnPlane(viewer.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            // Use the tracked floor, independent of the viewer's height or seated posture.
            float floorY = floorOrigin ? floorOrigin.position.y : patientVisual.position.y;
            Vector3 origin = new Vector3(viewer.transform.position.x, floorY, viewer.transform.position.z);
            environmentRoot.transform.SetPositionAndRotation(origin, rotation);
            if (alignPatientToChair)
                patientVisual.SetPositionAndRotation(patientAnchor.position, patientAnchor.rotation);
            seatingWasActive = new bool[legacySeating.Length];
            for (int i = 0; i < legacySeating.Length; i++)
            {
                GameObject seat = legacySeating[i];
                if (!seat) continue;
                seatingWasActive[i] = seat.activeSelf;
                seat.SetActive(false);
            }
            isOpen = true;
            if (hideAtlasHolograms)
            {
                if (!atlasHolographicRoot)
                    atlasHolographicRoot = GameObject.Find("Laboratorio holográfico · sin colisiones");
                if (atlasHolographicRoot)
                {
                    hologramsWereActive = atlasHolographicRoot.activeSelf;
                    capturedHolograms = true;
                    atlasHolographicRoot.SetActive(false);
                }
            }
            environmentRoot.SetActive(true);
            EnterImmersiveEnvironment(viewer);
        }

        public void Exit()
        {
            if (environmentRoot) environmentRoot.SetActive(false);
            RestoreEnvironment();
            if (!isOpen) return;
            for (int i = 0; i < legacySeating.Length; i++)
                if (legacySeating[i] && seatingWasActive != null && i < seatingWasActive.Length)
                    legacySeating[i].SetActive(seatingWasActive[i]);
            seatingWasActive = null;
            if (capturedHolograms && atlasHolographicRoot) atlasHolographicRoot.SetActive(hologramsWereActive);
            capturedHolograms = false;
            isOpen = false;
        }

        void EnterImmersiveEnvironment(Camera viewer)
        {
            if (!immersiveConsultation) return;
            if (!consultationSkybox) throw new InvalidOperationException("Falta el material HDRI del consultorio.");
            consultationCamera=viewer;
            previousSkybox=RenderSettings.skybox;
            if (!cameraSkybox) cameraSkybox=viewer.GetComponent<Skybox>();
            if(cameraSkybox){previousSkyboxEnabled=cameraSkybox.enabled;previousCameraSky=cameraSkybox.material;cameraSkybox.material=consultationSkybox;cameraSkybox.enabled=true;}
            previousClearFlags=viewer.clearFlags; previousBackground=viewer.backgroundColor;
            passthroughLayers=FindObjectsByType<OVRPassthroughLayer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            passthroughWasHidden=new bool[passthroughLayers.Length];
            for(int i=0;i<passthroughLayers.Length;i++)
            {
                passthroughWasHidden[i]=passthroughLayers[i].hidden;
                passthroughLayers[i].hidden=true;
            }
            environmentCaptured=true;
            RenderSettings.skybox=consultationSkybox;
            viewer.clearFlags=CameraClearFlags.Skybox;
            viewer.backgroundColor=Color.black;
            Debug.Log("[Atlas Clinic] Consulta inmersiva: passthrough oculto, HDRI activo.");
        }

        void RestoreEnvironment()
        {
            if(!environmentCaptured)return;
            RenderSettings.skybox=previousSkybox;
            if(cameraSkybox){cameraSkybox.material=previousCameraSky;cameraSkybox.enabled=previousSkyboxEnabled;}
            if(consultationCamera)
            {
                consultationCamera.clearFlags=previousClearFlags;
                consultationCamera.backgroundColor=previousBackground;
            }
            for(int i=0;i<passthroughLayers.Length;i++)
                if(passthroughLayers[i])passthroughLayers[i].hidden=passthroughWasHidden[i];
            environmentCaptured=false;
            Debug.Log("[Atlas Clinic] Entorno anterior restaurado.");
        }

        void OnDisable() => Exit();

        [ContextMenu("Mostrar consultorio en el editor")]
        void ShowEditorPreview()
        {
            if (!Application.isPlaying && environmentRoot) environmentRoot.SetActive(true);
        }

        [ContextMenu("Ocultar consultorio en el editor")]
        void HideEditorPreview()
        {
            if (!Application.isPlaying && environmentRoot) environmentRoot.SetActive(false);
        }

        void OnDrawGizmosSelected()
        {
            if (!patientAnchor) return;
            Gizmos.color = new Color(.15f, .85f, 1f);
            Gizmos.DrawWireSphere(patientAnchor.position + Vector3.up * .45f, .16f);
            Gizmos.DrawRay(patientAnchor.position + Vector3.up * .45f, patientAnchor.forward * .4f);
        }
    }
}

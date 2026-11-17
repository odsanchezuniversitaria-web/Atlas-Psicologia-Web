using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.HandGrab;

namespace Xyla
{
    public sealed partial class QuestAtlasController : MonoBehaviour
    {
        [Header("Atlas")]
        public AtlasData data;
        public QuestAtlasSpokenGuide spokenGuide;
        public Text statusText;

        [Header("Navegación · menú START en la escena")]
        public QuestAtlasSceneMenu sceneMenu;
        [Header("Neuropsicología · panel editable en la escena")]
        public QuestAtlasPsychology psychologyPanel;
        [Header("Paciente simulado · psicología")]
        public QuestPatientSimulation patientSimulation;
        public QuestAtlasClinicalPresentation clinicalPresentation;

        [Header("Interacción")]
        [Range(1, 64)] public int piezasPorFrame = 24;
        [Range(0.05f, 1f)] public float escalaMinima = 0.2f;
        [Range(1f, 8f)] public float escalaMaxima = 4f;

        readonly Dictionary<string, Material> batchMaterials = new Dictionary<string, Material>();
        readonly Dictionary<string, Material> pieceMaterials = new Dictionary<string, Material>();
        readonly Dictionary<AtlasSurfaceKind, Material> tissueMaterials = new Dictionary<AtlasSurfaceKind, Material>();
        readonly Dictionary<string, List<MeshRenderer>> batchRenderers =
            new Dictionary<string, List<MeshRenderer>>();
        QuestAtlasPiece[] pieces;
        bool[] visibleSystems;
        QuestAtlasMenu menu;
        QuestAtlasInformationPanel informationPanel;
        QuestAtlasVoiceContext voiceContext;
        public QuestAtlasVoiceAgent Voice { get; private set; }
        QuestAtlasSelectionAudio selectionAudio;
        QuestAtlasPullProvider pullProvider;
        Material boneMaterial, brainMaterial;
        public bool BoneTextureEnabled { get; private set; } = true;
        Texture2D states, boneMask, brainMask, partColorMap;
        Color[] statePixels;
        int activeSystem;
        bool showAllVisuals;
        bool building;
        string selectedName = "Ninguna";
        bool pushToTalkHeld;
        OVRInput.Controller pushToTalkHand = OVRInput.Controller.RTouch;

        public string ActiveSystemId => AtlasSystems.Ids[activeSystem];
        public string ActiveSystemName => AtlasSystems.Names[activeSystem];

void Start()
        {
            if (data == null || data.parts == null || data.parts.Length == 0)
                throw new InvalidOperationException("Faltan los datos del Atlas Humano.");

            Application.targetFrameRate = AtlasWebPlatform.Active ? 60 : 72;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 2;
            QualitySettings.pixelLightCount = 1;
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.shadowDistance = 0f;
            Physics.queriesHitBackfaces = true;

            if (!AtlasWebPlatform.Active && OVRManager.instance != null)
            {
                OVRManager.instance.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
                OVRManager.fixedFoveatedRenderingLevel = OVRManager.FixedFoveatedRenderingLevel.High;
                OVRManager.useDynamicFixedFoveatedRendering = true;
            }

            pieces = new QuestAtlasPiece[data.parts.Length];
            visibleSystems = new bool[AtlasSystems.Ids.Length];
            bool startPsychology = PlayerPrefs.GetInt("Atlas.StartPsychology", 0) == 1;
            if (startPsychology)
            {
                activeSystem = Array.IndexOf(AtlasSystems.Ids, "nervous");
                if (activeSystem < 0) throw new InvalidOperationException("Falta el sistema nervioso para psicología.");
            }
            visibleSystems[activeSystem] = true;
            InitializeAppControls();
            states = new Texture2D(Mathf.NextPowerOfTwo(data.parts.Length), 1, TextureFormat.RGBAHalf, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Estado de piezas VR"
            };
            statePixels = new Color[states.width];

            selectionAudio = gameObject.AddComponent<QuestAtlasSelectionAudio>();
            pullProvider = gameObject.AddComponent<QuestAtlasPullProvider>();
            CreateMaterials();
            CreateSystemReturnCores();
            CreateBatchRenderers();
            ApplyBatchState();
            voiceContext = gameObject.AddComponent<QuestAtlasVoiceContext>();
            voiceContext.Initialize(this);
            Voice = GetComponent<QuestAtlasVoiceAgent>() ?? gameObject.AddComponent<QuestAtlasVoiceAgent>();
            Voice.Initialize(this, voiceContext);
            spokenGuide.Initialize(Voice);
            Voice.SetVoiceVolume(PlayerPrefs.GetFloat("Atlas.VoiceVolume", 1f));
            avatar = QuestAtlasAvatar.Create(this, Voice);
            Environment = gameObject.AddComponent<QuestAtlasEnvironment>();
            Environment.Initialize(this);
            informationPanel = QuestAtlasInformationPanel.Create();
            InitializeLearningTools();
            menu = QuestAtlasMenu.Create(this);
            if (!psychologyPanel) throw new InvalidOperationException("Falta el panel de psicología de la escena.");
            psychologyPanel.Initialize(this);
            if (!patientSimulation) throw new InvalidOperationException("Falta el paciente simulado en la escena.");
            patientSimulation.Initialize(this);
            if (!sceneMenu) throw new InvalidOperationException("Falta el menú START de la escena.");
            sceneMenu.Bind(this);
            statusText.GetComponentInParent<Canvas>().gameObject.SetActive(false);
            if (!startPsychology) BeginBuildSystem(ActiveSystemId);
            UpdateStatus();
            if (startPsychology)
            {
                PlayerPrefs.SetInt("Atlas.StartPsychology", 0);
                StartCoroutine(OpenPsychologyAfterFirstFrame());
            }
            else Voice.ScheduleWelcome();
        }

        IEnumerator OpenPsychologyAfterFirstFrame()
        {
            yield return null;
            Debug.Log("[Atlas Psychology] Abriendo la entrevista clínica con la paciente.");
            OpenPatientSimulation();
        }

void Update()
        {
            if (AtlasWebPlatform.Active) return;
            bool next = Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame;
            bool toggle = Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame;
            bool reset = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;

            // Hand pinches also map to Button.One. Read physical Touch controllers explicitly.
            bool rightTouch = OVRInput.IsControllerConnected(OVRInput.Controller.RTouch)
                && OVRInput.GetControllerPositionTracked(OVRInput.Controller.RTouch);
            bool leftTouch = OVRInput.IsControllerConnected(OVRInput.Controller.LTouch)
                && OVRInput.GetControllerPositionTracked(OVRInput.Controller.LTouch);
            if (next && !patientSimulation.IsOpen) NextSystem();
            // Push-to-talk: hold A (right) or X (left) to speak to Atlas, release to send.
            bool talkRight = rightTouch && OVRInput.Get(OVRInput.RawButton.A, OVRInput.Controller.RTouch);
            bool talkLeft = leftTouch && OVRInput.Get(OVRInput.RawButton.X, OVRInput.Controller.LTouch);
            bool talk = talkRight || talkLeft || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);
            if (leftTouch && !Voice.Speaking && !patientSimulation.voice.Speaking && OVRInput.GetDown(OVRInput.RawButton.Y, OVRInput.Controller.LTouch))
                _ = spokenGuide.PlayGuide(patientSimulation.IsOpen);
            if (spokenGuide.Playing && talk) spokenGuide.Cancel();
            if (talk != pushToTalkHeld)
            {
                pushToTalkHeld = talk;
                OVRInput.Controller hand = talk
                    ? (talkRight ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch)
                    : pushToTalkHand;
                pushToTalkHand = hand;
                if (hand == OVRInput.Controller.RTouch ? rightTouch : leftTouch)
                {
                    QuestAtlasHaptics.Play(talk ? AtlasHapticCue.Voice : AtlasHapticCue.Release, hand);
                }
            }
            if (patientSimulation.IsOpen)
            {
                Voice.SetPushToTalk(false);
                patientSimulation.voice.SetPushToTalk(talk);
            }
            else Voice.SetPushToTalk(talk);
            if (!patientSimulation.IsOpen && (toggle || (rightTouch && OVRInput.GetDown(OVRInput.RawButton.B, OVRInput.Controller.RTouch))))
                ToggleAllVisuals();
            if (reset && !patientSimulation.IsOpen) ResetAtlas();
            bool start = leftTouch && OVRInput.GetDown(OVRInput.RawButton.Start, OVRInput.Controller.LTouch);
            if (start || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
            {
                if (patientSimulation.IsOpen) { patientSimulation.Close(); return; }
                if (psychologyPanel.IsOpen) psychologyPanel.HideForAtlasNavigation();
                ExitClinicalPresentation();
                if (!sceneMenu.IsOpen)
                {
                    sceneMenu.RememberAtlasPanel(menu.gameObject.activeSelf);
                    menu.gameObject.SetActive(false);
                }
                sceneMenu.ToggleInAtlas();
            }
        }

        void CreateMaterials()
        {
            boneMaterial = new Material(Resources.Load<Material>("CorticalBone"));
            boneMaterial.name = "Huesos · superficie cortical";
            Material brainTemplate = Resources.Load<Material>("BrainTissue");
            if (!brainTemplate) throw new InvalidOperationException("Falta el material BrainTissue del cerebro.");
            brainMaterial = new Material(brainTemplate) { name = "Cerebro · tejido cortical realista" };
            boneMask = new Texture2D(states.width, 1, TextureFormat.R8, false, true)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            brainMask = new Texture2D(states.width, 1, TextureFormat.R8, false, true)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            partColorMap = new Texture2D(states.width, 1, TextureFormat.RGBA32, false, true)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var bonePixels = new Color[states.width];
            var brainPixels = new Color[states.width];
            var partColors = new Color[states.width];
            for (int i = 0; i < data.parts.Length; i++)
            {
                AtlasPart part = data.parts[i];
                AtlasSurfaceKind kind = AtlasTissueProfile.Kind(part);
                bonePixels[i] = IsBone(part) ? Color.white : Color.black;
                brainPixels[i] = IsBrainTissue(part) ? Color.white : Color.black;
                string texture = AtlasTissueProfile.TextureResource(kind);
                Color color = AtlasTissueProfile.Color(kind, part.system);
                color.a = part.system == "muscular" && texture == "Myocardium"
                    ? 1f : texture != null && texture == AtlasTissueProfile.BatchTextureResource(part.system)
                    ? .5f : 0f;
                partColors[i] = color;
            }
            boneMask.SetPixels(bonePixels); boneMask.Apply(false, true);
            brainMask.SetPixels(brainPixels); brainMask.Apply(false, true);
            partColorMap.SetPixels(partColors); partColorMap.Apply(false, true);
            Shader pieceShader = Shader.Find("Universal Render Pipeline/Lit");
            if (pieceShader == null) pieceShader = Shader.Find("Standard");
            if (pieceShader == null) pieceShader = Shader.Find("Unlit/Color");

            foreach (string id in AtlasSystems.Ids)
            {
                Color color = AtlasSystems.ColorFor(id);
                var batch = new Material(data.shader) { name = "Lote " + AtlasSystems.Name(id) };
                batch.SetColor("_Tint", color);
                batch.SetTexture("_PartData", states);
                batch.SetTexture("_BoneMask", boneMask);
                batch.SetTexture("_BoneMap", boneMaterial.GetTexture("_BoneMap"));
                batch.SetFloat("_TextureScale", boneMaterial.GetFloat("_TextureScale"));
                batch.SetFloat("_TextureStrength", 1f);
                if (id == "nervous")
                {
                    batch.SetTexture("_BrainMask", brainMask);
                    batch.SetTexture("_BrainMap", brainMaterial.GetTexture("_BrainMap"));
                    batch.SetFloat("_BrainScale", brainMaterial.GetFloat("_BrainScale"));
                    batch.SetFloat("_BrainStrength", brainMaterial.GetFloat("_BrainStrength"));
                    batch.EnableKeyword("ATLAS_BRAIN_SURFACE");
                }
                if (id == "skeletal") batch.EnableKeyword("ATLAS_BONE_SURFACE");
                if (id != "skeletal" && id != "nervous")
                {
                    batch.SetTexture("_PartColor", partColorMap);
                    string textureResource = AtlasTissueProfile.BatchTextureResource(id);
                    Material tissueTemplate = textureResource == null ? null : Resources.Load<Material>(textureResource);
                    if (textureResource != null && !tissueTemplate)
                        throw new InvalidOperationException("Falta el material " + textureResource + ".");
                    batch.SetTexture("_TissueMap", tissueTemplate ? tissueTemplate.GetTexture("_TissueMap") : Texture2D.whiteTexture);
                    batch.SetFloat("_TissueScale", tissueTemplate ? tissueTemplate.GetFloat("_TissueScale") : 13f);
                    batch.SetFloat("_TissueStrength", tissueTemplate ? tissueTemplate.GetFloat("_TissueStrength") : 0f);
                    if (id == "muscular")
                    {
                        Material secondary = Resources.Load<Material>("Myocardium");
                        if (!secondary) throw new InvalidOperationException("Falta el material Myocardium.");
                        batch.SetTexture("_TissueMap2", secondary.GetTexture("_TissueMap"));
                        batch.SetFloat("_TissueScale2", secondary.GetFloat("_TissueScale"));
                        batch.SetFloat("_TissueStrength2", secondary.GetFloat("_TissueStrength"));
                    }
                    batch.EnableKeyword("ATLAS_TISSUE_SURFACE");
                }
                batchMaterials[id] = batch;

                var piece = new Material(pieceShader) { name = "Pieza " + AtlasSystems.Name(id) };
                if (piece.HasProperty("_BaseColor")) piece.SetColor("_BaseColor", color);
                if (piece.HasProperty("_Color")) piece.SetColor("_Color", color);
                if (piece.HasProperty("_Smoothness")) piece.SetFloat("_Smoothness", .28f);
                pieceMaterials[id] = piece;
            }
        }

        Material MaterialForTissue(AtlasPart part)
        {
            AtlasSurfaceKind kind = AtlasTissueProfile.Kind(part);
            if (kind == AtlasSurfaceKind.Default) return pieceMaterials[part.system];
            if (tissueMaterials.TryGetValue(kind, out Material cached)) return cached;
            string resource = AtlasTissueProfile.TextureResource(kind);
            Material material;
            if (resource != null)
            {
                Material template = Resources.Load<Material>(resource);
                if (!template) throw new InvalidOperationException("Falta el material " + resource + ".");
                material = new Material(template);
            }
            else
            {
                material = new Material(pieceMaterials[part.system]);
                Color color = AtlasTissueProfile.Color(kind, part.system);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                if (material.HasProperty("_Smoothness"))
                    material.SetFloat("_Smoothness", AtlasTissueProfile.Smoothness(kind));
            }
            material.name = "Tejido · " + kind;
            tissueMaterials.Add(kind, material);
            return material;
        }

void CreateBatchRenderers()
        {
            foreach (AtlasBatch batch in data.batches)
            {
                var go = new GameObject(batch.system + " · lote Quest");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = batch.mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = batchMaterials[batch.system];
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                if (!batchRenderers.TryGetValue(batch.system, out List<MeshRenderer> renderers))
                {
                    renderers = new List<MeshRenderer>();
                    batchRenderers.Add(batch.system, renderers);
                }
                renderers.Add(renderer);
            }
        }

void BeginBuildSystem(string system)
        {
            StopAllCoroutines();
            building = false;
            StartCoroutine(BuildSystem(system));
        }


        IEnumerator BuildSystem(string system)
        {
            if (building) yield break;
            building = true;
            UpdateStatus("Preparando piezas de " + AtlasSystems.Name(system) + "...");

            int madeThisFrame = 0;
            for (int i = 0; i < data.parts.Length; i++)
            {
                if (data.parts[i].system != system || pieces[i] != null) continue;
                pieces[i] = CreatePiece(i);
                madeThisFrame++;
                if (madeThisFrame >= piezasPorFrame)
                {
                    madeThisFrame = 0;
                    yield return null;
                }
            }

            building = false;
            ApplyBatchState();
            UpdateAvailability();
            UpdateStatus();
        }

QuestAtlasPiece CreatePiece(int index)
        {
            if (AtlasWebPlatform.Active) return CreateWebPiece(index);
            AtlasPart part = data.parts[index];

            var root = new GameObject(part.id + " · " + SpanishName(part.name));
            root.transform.SetParent(transform, false);
            root.transform.localPosition = part.bounds.center;

            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.interpolation = RigidbodyInterpolation.None;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

            var transformer = root.AddComponent<GrabFreeTransformer>();
            var range = new TransformerUtils.FloatRange { Min = escalaMinima, Max = escalaMaxima };
            var axis = new TransformerUtils.ConstrainedAxis
            {
                ConstrainAxis = true,
                AxisRange = range
            };
            transformer.InjectOptionalScaleConstraints(new TransformerUtils.ScaleConstraints
            {
                ConstraintsAreRelative = true,
                XAxis = axis,
                YAxis = axis,
                ZAxis = axis
            });

            var grabbable = root.AddComponent<Grabbable>();
            grabbable.InjectOptionalRigidbody(rigidbody);
            grabbable.InjectOptionalOneGrabTransformer(transformer);
            grabbable.InjectOptionalTwoGrabTransformer(transformer);
            grabbable.MaxGrabPoints = 2;
            grabbable.InjectOptionalThrowWhenUnselected(false);

            var interaction = new GameObject("ISDK_HandGrabInteraction");
            interaction.transform.SetParent(root.transform, false);

            var visual = new GameObject("Malla");
            visual.transform.SetParent(interaction.transform, false);
            visual.transform.localPosition = -part.bounds.center;

            visual.AddComponent<MeshFilter>().sharedMesh = part.mesh;
            var renderer = visual.AddComponent<MeshRenderer>();
            Material material = IsBone(part) ? boneMaterial
                : IsBrainTissue(part) ? brainMaterial : MaterialForTissue(part);
            var materials = new Material[Mathf.Max(1, part.mesh.subMeshCount)];
            for (int m = 0; m < materials.Length; m++) materials[m] = material;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;

            var collider = root.AddComponent<BoxCollider>();
            Vector3 size = part.bounds.size;
            collider.center = Vector3.zero;
            collider.size = new Vector3(
                Mathf.Max(0.006f, size.x),
                Mathf.Max(0.006f, size.y),
                Mathf.Max(0.006f, size.z));

            var controllerGrab = interaction.AddComponent<GrabInteractable>();
            controllerGrab.InjectAllGrabInteractable(rigidbody);
            controllerGrab.InjectOptionalPointableElement(grabbable);

            var handGrab = interaction.AddComponent<HandGrabInteractable>();
            handGrab.InjectRigidbody(rigidbody);
            handGrab.InjectSupportedGrabTypes(GrabTypeFlags.All);
            handGrab.InjectOptionalPointableElement(grabbable);

            var piece = root.AddComponent<QuestAtlasPiece>();
            
            var surface = interaction.AddComponent<Oculus.Interaction.Surfaces.ColliderSurface>();
            surface.InjectCollider(collider);
            var rayTarget = interaction.AddComponent<RayInteractable>();
            rayTarget.InjectSurface(surface);
            rayTarget.InjectOptionalMovementProvider(pullProvider);
            rayTarget.InjectOptionalPointableElement(grabbable);
            piece.InitializeRay(rayTarget);
piece.Initialize(this, index, renderer, collider, controllerGrab, handGrab, grabbable);
            voiceContext.Register(piece, rayTarget);
            piece.SetStandaloneVisual(false);
            piece.SetInteractionAvailable(part.system == ActiveSystemId && IsSystemVisible(activeSystem));
            return piece;
        }

        public void Detach(QuestAtlasPiece piece)
        {
            if (psychologyPanel && psychologyPanel.HandlePieceSelected(piece.Index)) return;
            if (Learning!=null && Learning.HandlePieceSelected(piece.Index)) return;
            RecordUndo("mover "+SpanishName(data.parts[piece.Index].name));
            if (!piece.Detached)
            {
                piece.Detach();
                ApplyBatchState();
            }
            selectedName = SpanishName(data.parts[piece.Index].name);
            informationPanel.ShowPart(data.parts[piece.Index], piece.transform);
            UpdateStatus();
        }

public void CompletePieceReturn(QuestAtlasPiece piece)
        {
            if (piece == null) return;

            string returnedId = data.parts[piece.Index].id;
            string returnedName = SpanishName(data.parts[piece.Index].name);
            if (informationPanel != null && informationPanel.SelectedPartId == returnedId)
            {
                selectedName = "Ninguna";
                informationPanel.ClearSelection();
            }

            ApplyBatchState();
            UpdateAvailability();
            PlaySelectionSound();
            UpdateStatus(returnedName + " volvió a su sistema");
        }


public void NextSystem()
        {
            QuestAtlasTransitions.Current.Pulse(true);
            RecordUndo("cambiar de sistema");
            Array.Clear(excludedParts, 0, excludedParts.Length);
            activeSystem = (activeSystem + 1) % AtlasSystems.Ids.Length;
            showAllVisuals = false;
            Array.Clear(visibleSystems, 0, visibleSystems.Length);
            visibleSystems[activeSystem] = true;
            ApplyBatchState();
            UpdateAvailability();
            BeginBuildSystem(ActiveSystemId);
            UpdateStatus();
        }

public bool IsSystemVisible(int index)
        {
            return showAllVisuals || visibleSystems[index];
        }

        public bool IsActiveSystem(int index)
        {
            return index == activeSystem && IsSystemVisible(index);
        }

        public void ToggleSystem(int index)
        {
            QuestAtlasTransitions.Current.Pulse(true);
            RecordUndo("cambiar visibilidad de sistema");
            if (showAllVisuals)
                for (int i = 0; i < visibleSystems.Length; i++) visibleSystems[i] = true;
            showAllVisuals = false;
            visibleSystems[index] = !visibleSystems[index];

            if (visibleSystems[index])
            {
                ClearPartMask(AtlasSystems.Ids[index]);
                activeSystem = index;
                BeginBuildSystem(ActiveSystemId);
            }
            else if (activeSystem == index)
            {
                for (int i = 0; i < visibleSystems.Length; i++)
                {
                    if (!visibleSystems[i]) continue;
                    activeSystem = i;
                    BeginBuildSystem(ActiveSystemId);
                    break;
                }
            }

            ApplyBatchState();
            UpdateAvailability();
            UpdateStatus();
        }

        public void ShowOnlyActiveSystem()
        {
            QuestAtlasTransitions.Current.Pulse(true);
            RecordUndo("mostrar solo el sistema activo");
            Array.Clear(excludedParts, 0, excludedParts.Length);
            showAllVisuals = false;
            Array.Clear(visibleSystems, 0, visibleSystems.Length);
            visibleSystems[activeSystem] = true;
            ApplyBatchState();
            UpdateAvailability();
            BeginBuildSystem(ActiveSystemId);
            UpdateStatus();
        }

        public void ShowAllSystems()
        {
            QuestAtlasTransitions.Current.Pulse(true);
            RecordUndo("mostrar todos los sistemas");
            Array.Clear(excludedParts, 0, excludedParts.Length);
            showAllVisuals = true;
            ApplyBatchState();
            UpdateAvailability();
            UpdateStatus();
        }

        public void HideAllSystems()
        {
            QuestAtlasTransitions.Current.Pulse(true);
            RecordUndo("ocultar todos los sistemas");
            showAllVisuals = false;
            Array.Clear(visibleSystems, 0, visibleSystems.Length);
            ApplyBatchState();
            UpdateAvailability();
            UpdateStatus();
        }


public void ToggleAllVisuals()
        {
            if (showAllVisuals)
                ShowOnlyActiveSystem();
            else
                ShowAllSystems();
        }

public void ResetAtlas()
        {
            QuestAtlasTransitions.Current.Pulse(true);
            RecordUndo("restablecer el atlas");
            Array.Clear(excludedParts, 0, excludedParts.Length);
            transform.SetPositionAndRotation(initialModelPosition,initialModelRotation); transform.localScale=initialModelScale;
            StopAllCoroutines();
            building = false;

            if (pieces != null)
                foreach (QuestAtlasPiece piece in pieces)
                    if (piece != null) piece.ResetPiece();

            showAllVisuals = false;
            Array.Clear(visibleSystems, 0, visibleSystems.Length);
            visibleSystems[activeSystem] = true;
            selectedName = "Ninguna";
            informationPanel.ClearSelection();
            ApplyBatchState();
            UpdateAvailability();
            BeginBuildSystem(ActiveSystemId);
            UpdateStatus("Atlas restablecido");
        }

void UpdateAvailability()
        {
            if (pieces == null) return;
            bool activeVisible = IsSystemVisible(activeSystem);
            for (int i = 0; i < pieces.Length; i++)
            {
                QuestAtlasPiece piece = pieces[i];
                if (piece == null) continue;
                bool available = IsPartVisible(i) &&
                    (piece.Detached || explicitInteractiveParts.Contains(i) || (activeVisible && data.parts[i].system == ActiveSystemId));
                piece.SetAppVisible(IsPartVisible(i));
                piece.SetInteractionAvailable(available);
            }
        }

void ApplyBatchState()
        {
            if (states == null) return;

            foreach (KeyValuePair<string, List<MeshRenderer>> group in batchRenderers)
            {
                int systemIndex = Array.IndexOf(AtlasSystems.Ids, group.Key);
                bool enabled = systemIndex >= 0 && IsSystemVisible(systemIndex);
                foreach (MeshRenderer renderer in group.Value)
                    renderer.enabled = enabled;
            }

            for (int i = 0; i < data.parts.Length; i++)
            {
                bool detached = pieces != null && pieces[i] != null && pieces[i].Detached;
                statePixels[i] = new Color(0f, 0f, 0f, detached || !IsPartVisible(i) ? 0f : (pieces[i] != null && pieces[i].IsHighlighted ? 2f : 1f));
            }
            states.SetPixels(statePixels);
            states.Apply(false, false);
            if (menu != null) menu.Refresh();
        }

        string SpanishName(string source)
        {
            try { return AtlasSpanish.Name(source); }
            catch { return source; }
        }

void UpdateStatus(string transient = null)
        {
            if (statusText == null) return;
            int ready = 0;
            int visibleCount = 0;
            for (int i = 0; i < visibleSystems.Length; i++)
                if (IsSystemVisible(i)) visibleCount++;

            if (pieces != null)
                for (int i = 0; i < pieces.Length; i++)
                    if (pieces[i] != null && data.parts[i].system == ActiveSystemId) ready++;

            statusText.text =
                "ATLAS HUMANO VR\n\n" +
                (string.IsNullOrEmpty(transient) ? "" : transient + "\n") +
                "Listo para agarrar: " + ActiveSystemName + "\n" +
                "Sistemas visibles: " + visibleCount + "\n" +
                "Piezas preparadas: " + ready + "\n" +
                "Pieza seleccionada: " + selectedName + "\n\n" +
                "PALANCA IZQUIERDA: caminar\n" +
                "PALANCA DERECHA: girar\n" +
                "GATILLO: pulsar el panel\n" +
                "GRAB LATERAL: atraer y sujetar\n" +
                "ALCANCE DEL RAYO: 3 metros\n" +
                "Suelta los mandos: usar manos\n" +
                "A o X (mantener): hablar con Atlas\n" +
                "B: todos / solo activo\n" +
                "Menú: restablecer piezas\n\n" +
                "Dos manos permiten escalar.";
            if (menu != null) menu.Refresh();
        }

        public void SetSystemsFromVoice(string[] ids, bool visible)
        {
            FocusAnatomy(ids,Array.Empty<string>(),visible?"show":"hide");
        }

        public QuestAtlasPiece PreparePieceFromVoice(string id)
        {
            int index = Array.FindIndex(data.parts, part => part.id == id);
            if (index < 0) throw new ArgumentException("Pieza desconocida.");
            EnsurePartVisible(index);
            if (pieces[index] == null) pieces[index] = CreatePiece(index);
            pieces[index].SetAppVisible(true);
            if (!pieces[index].IsHeld) Detach(pieces[index]);
            return pieces[index];
        }

        public static bool IsBone(AtlasPart part)
        {
            return part.system == "skeletal" && System.Text.RegularExpressions.Regex.IsMatch(part.name.ToLowerInvariant(),
                @"\b(bone|vertebra|phalanx|rib|clavicle|calcaneus|capitate|femur|fibula|hamate|humerus|lunate|maxilla|patella|pisiform|radius|scaphoid|scapula|talus|temporal|tibia|trapezium|trapezoid|triquetral|ulna|mandible|manubrium|sacrum|ethmoid|vomer|atlas|axis|sternum|xiphoid)\b")
                && !System.Text.RegularExpressions.Regex.IsMatch(part.name.ToLowerInvariant(), @"\b(cartilage|disk|tooth|gingiva)\b");
        }

        public static bool IsBrainTissue(AtlasPart part)
        {
            if (part == null || part.system != "nervous" || string.IsNullOrEmpty(part.name)) return false;
            string name = part.name.ToLowerInvariant();
            if (System.Text.RegularExpressions.Regex.IsMatch(name,
                @"\b(white matter|ventricle|choroid plexus|subcortex|nucleus|commissure|nerve|tract)\b")) return false;
            return System.Text.RegularExpressions.Regex.IsMatch(name,
                @"\b(gyrus|gyri|cortex|lobe|cerebellum|brainstem|brain|pons|medulla oblongata|midbrain|insula|lobule|vermis|cerebrum|hemisphere)\b");
        }

        public void ToggleBoneTexture()
        {
            RecordUndo("cambiar acabado óseo");
            BoneTextureEnabled = !BoneTextureEnabled;
            boneMaterial.SetFloat("_TextureStrength", BoneTextureEnabled ? 1f : 0f);
            batchMaterials["skeletal"].SetFloat("_TextureStrength", BoneTextureEnabled ? 1f : 0f);
            menu.Refresh();
        }

        public void PlaySelectionSound() { selectionAudio.PlaySelection(); }

        void OnDestroy()
        {
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            if (states != null) Destroy(states);
            if (boneMask != null) Destroy(boneMask);
            if (brainMask != null) Destroy(brainMask);
            if (partColorMap != null) Destroy(partColorMap);
            if (boneMaterial != null) Destroy(boneMaterial);
            if (brainMaterial != null) Destroy(brainMaterial);
            foreach (Material material in batchMaterials.Values) Destroy(material);
            foreach (Material material in pieceMaterials.Values) Destroy(material);
            foreach (Material material in tissueMaterials.Values) Destroy(material);
        }
    

public void SetPieceHighlight(QuestAtlasPiece piece)
        {
            statePixels[piece.Index] = new Color(0f, 0f, 0f,
                piece.Detached || !IsPartVisible(piece.Index) ? 0f : (piece.IsHighlighted ? 2f : 1f));
            states.SetPixel(piece.Index, 0, statePixels[piece.Index]);
            states.Apply(false, false);
        }


void CreateSystemReturnCores()
        {
            const float CoreRadius = 0.12f;
            for (int systemIndex = 0; systemIndex < AtlasSystems.Ids.Length; systemIndex++)
            {
                string systemId = AtlasSystems.Ids[systemIndex];
                bool found = false;
                Bounds systemBounds = default;
                for (int partIndex = 0; partIndex < data.parts.Length; partIndex++)
                {
                    AtlasPart part = data.parts[partIndex];
                    if (part.system != systemId) continue;

                    if (!found)
                    {
                        systemBounds = part.bounds;
                        found = true;
                    }
                    else
                    {
                        systemBounds.Encapsulate(part.bounds);
                    }
                }

                if (!found) continue;

                var coreObject = new GameObject("Núcleo de retorno · " + AtlasSystems.Name(systemId));
                coreObject.transform.SetParent(transform, false);
                coreObject.transform.localPosition = systemBounds.center;
                var trigger = coreObject.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = CoreRadius;
                coreObject.AddComponent<QuestAtlasSystemReturnCore>().Initialize(systemId);
            }
        }
}

    public sealed class QuestAtlasPiece : MonoBehaviour
    {
        QuestAtlasController controller;
        MeshRenderer meshRenderer;
        Collider meshCollider;
        GrabInteractable controllerGrab;
        HandGrabInteractable handGrab;
        Grabbable grabbable;
        Vector3 initialLocalPosition;
        Quaternion initialLocalRotation;
        Vector3 initialLocalScale;

        public int Index { get; private set; }
        
        RayInteractable rayTarget;
        readonly HashSet<int> hoveringPointers = new HashSet<int>();
        readonly HashSet<int> selectingPointers = new HashSet<int>();
        MaterialPropertyBlock highlight;
        bool narrationHighlighted;
        public bool IsHighlighted => hoveringPointers.Count > 0 || selectingPointers.Count > 0 || narrationHighlighted;
        
        public string SystemId => controller == null ? "" : controller.data.parts[Index].system;
        bool returnRequested;
        bool returning;
public bool IsHeld => selectingPointers.Count > 0;

        public void InitializeWeb(QuestAtlasController owner,int index,MeshRenderer renderer,Collider collider){
            controller=owner;Index=index;meshRenderer=renderer;meshCollider=collider;highlight=new MaterialPropertyBlock();
            initialLocalPosition=transform.localPosition;initialLocalRotation=transform.localRotation;initialLocalScale=transform.localScale;
        }
        public void WebHover(bool value){if(value)hoveringPointers.Add(-42);else hoveringPointers.Remove(-42);RefreshHighlight();}
        public void WebHold(bool value){
            if(value){selectingPointers.Add(-42);controller.PlaySelectionSound();controller.Detach(this);}
            else {selectingPointers.Remove(-42);if(returnRequested&&selectingPointers.Count==0)StartReturnToOrigin();}
            RefreshHighlight();
        }
        public void InitializeRay(RayInteractable target)
        {
            rayTarget = target;
        }

        public void SetNarrationHighlight(bool enabled)
        {
            narrationHighlighted=enabled;RefreshHighlight();
        }

        void RefreshHighlight()
        {
            highlight.Clear();
            if (IsHighlighted)
            {
                Color color = selectingPointers.Count > 0 ? new Color(1f, 0.65f, 0.12f)
                    : narrationHighlighted ? new Color(.2f,1f,.55f) : new Color(0.1f, 0.9f, 0.95f);
                highlight.SetColor("_BaseColor", color);
                highlight.SetColor("_Color", color);
            }
            meshRenderer.SetPropertyBlock(highlight);
            controller.SetPieceHighlight(this);
        }
public bool Detached { get; private set; }

public void Initialize(
            QuestAtlasController owner,
            int index,
            MeshRenderer renderer,
            Collider collider,
            GrabInteractable controllerInteractable,
            HandGrabInteractable handInteractable,
            Grabbable target)
        {
            controller = owner;
            highlight = new MaterialPropertyBlock();
            Index = index;
            meshRenderer = renderer;
            meshCollider = collider;
            controllerGrab = controllerInteractable;
            handGrab = handInteractable;
            grabbable = target;
            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;
            initialLocalScale = transform.localScale;
            grabbable.WhenPointerEventRaised += HandlePointerEvent;
        }

void HandlePointerEvent(PointerEvent evt)
        {
            bool startReturn = false;
            switch (evt.Type)
            {
                case PointerEventType.Hover:
                    hoveringPointers.Add(evt.Identifier);
                    break;
                case PointerEventType.Unhover:
                    hoveringPointers.Remove(evt.Identifier);
                    break;
                case PointerEventType.Select:
                    if (selectingPointers.Count == 0) { controller.PlaySelectionSound(); QuestAtlasHaptics.Play(AtlasHapticCue.Grab); }
                    selectingPointers.Add(evt.Identifier);
                    controller.Detach(this);
                    break;
                case PointerEventType.Unselect:
                    QuestAtlasHaptics.Play(AtlasHapticCue.Release);
                    selectingPointers.Remove(evt.Identifier);
                    startReturn = returnRequested && selectingPointers.Count == 0;
                    break;
                case PointerEventType.Cancel:
                    hoveringPointers.Remove(evt.Identifier);
                    selectingPointers.Remove(evt.Identifier);
                    startReturn = returnRequested && selectingPointers.Count == 0;
                    break;
                default:
                    return;
            }

            RefreshHighlight();
            if (startReturn) StartReturnToOrigin();
        }

        bool standaloneVisible;
        bool appVisible = true;
        public void SetAppVisible(bool visible) { appVisible = visible; meshRenderer.enabled = visible && (Detached || standaloneVisible); }
        public void SetStandaloneVisual(bool visible)
        {
            standaloneVisible = visible;
            meshRenderer.enabled = appVisible && (Detached || standaloneVisible);
        }

        public void Detach()
        {
            Detached = true;
            meshRenderer.enabled = true;
            SetInteractionAvailable(true);
        }

public void RequestReturnToOrigin()
        {
            if (!Detached || returning) return;

            returnRequested = true;
            if (!IsHeld) StartReturnToOrigin();
        }

        void StartReturnToOrigin()
        {
            if (!Detached || returning || IsHeld) return;

            returning = true;
            returnRequested = false;
            StartCoroutine(ReturnToOriginRoutine());
        }

        IEnumerator ReturnToOriginRoutine()
        {
            SetInteractionAvailable(false);
            Vector3 startPosition = transform.localPosition;
            Quaternion startRotation = transform.localRotation;
            Vector3 startScale = transform.localScale;
            const float duration = 0.38f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                transform.localPosition = Vector3.LerpUnclamped(startPosition, initialLocalPosition, t);
                transform.localRotation = Quaternion.SlerpUnclamped(startRotation, initialLocalRotation, t);
                transform.localScale = Vector3.LerpUnclamped(startScale, initialLocalScale, t);
                yield return null;
            }

            ApplyInitialPose();
            controller.CompletePieceReturn(this);
        }

        void ApplyInitialPose()
        {
            Detached = false;
            returning = false;
            returnRequested = false;
            hoveringPointers.Clear();
            selectingPointers.Clear();
            transform.localPosition = initialLocalPosition;
            transform.localRotation = initialLocalRotation;
            transform.localScale = initialLocalScale;
            if (meshRenderer != null) meshRenderer.enabled = appVisible && standaloneVisible;
            RefreshHighlight();
        }


public void SetInteractionAvailable(bool available)
        {
            meshCollider.enabled = available;
            if (!AtlasWebPlatform.Active) { controllerGrab.enabled = available; handGrab.enabled = available; }
            if (rayTarget != null) rayTarget.enabled = available;
            if (!available && IsHighlighted)
            {
                hoveringPointers.Clear();
                selectingPointers.Clear();
                RefreshHighlight();
            }
        }

        public void RestorePose(Vector3 position,Quaternion rotation,Vector3 scale,bool detached)
        {
            Detached=detached;transform.SetPositionAndRotation(position,rotation);transform.localScale=scale;
            meshRenderer.enabled=appVisible&&(Detached||standaloneVisible);SetInteractionAvailable(appVisible);
        }

public void ResetPiece()
        {
            StopAllCoroutines();
            ApplyInitialPose();
        }

        void OnDestroy()
        {
            if (grabbable != null) grabbable.WhenPointerEventRaised -= HandlePointerEvent;
        }
    }
}

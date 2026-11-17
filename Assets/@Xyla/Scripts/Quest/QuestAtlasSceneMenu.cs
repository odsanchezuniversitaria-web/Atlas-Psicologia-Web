using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Xyla
{
    // The pages and buttons are authored in the two Unity scenes, not generated at runtime.
    public sealed class QuestAtlasSceneMenu : MonoBehaviour
    {
        public const string LandingSceneName = "AtlasWebInicio";
        public const string AtlasSceneName = "AtlasWebLab";

        [Header("Ubicación")]
        public bool isLandingScene;
        [Header("Páginas editables en la jerarquía")]
        public GameObject mainPage;
        public GameObject settingsPage;
        public GameObject creditsPage;
        [Header("Textos de ajustes")]
        public Text passthroughLabel;
        public Text effectsLabel;
        public Text soundsLabel;
        public Text voiceVolumeLabel;
        public Text loadingLabel;
        public Text hapticsLabel;

        QuestAtlasController atlas;
        AudioSource clickAudio;
        AudioClip confirmClip;
        bool loading;
        bool restoreAtlasPanel;

        public bool IsOpen => gameObject.activeSelf;

        void Awake()
        {
            clickAudio = GetComponent<AudioSource>();
            confirmClip = Resources.Load<AudioClip>("AtlasLabAudio/Confirm");
            if (isLandingScene) ApplyLandingPassthrough(PlayerPrefs.GetInt("Atlas.Passthrough", 1) == 1);
            RefreshSettings();
        }

        public void Bind(QuestAtlasController owner) { atlas = owner; }
        public void RememberAtlasPanel(bool visible) { restoreAtlasPanel = visible; }

        public void ToggleInAtlas()
        {
            if (isLandingScene) return;
            if (IsOpen) Hide(); else OpenMain();
        }

        public void OpenMain()
        {
            gameObject.SetActive(true);
            if (!isLandingScene) MoveInFrontOfViewer();
            ShowPage(mainPage);
            RefreshSettings();
            PlayClick();
        }

        public void OpenSettings() { ShowPage(settingsPage); RefreshSettings(); PlayClick(); }
        public void OpenCredits() { ShowPage(creditsPage); PlayClick(); }
        public void Hide()
        {
            if (isLandingScene) return;
            gameObject.SetActive(false);
            if (restoreAtlasPanel) atlas.RestoreAtlasPanel();
            restoreAtlasPanel = false;
        }

        public void OpenAtlasPanel()
        {
            restoreAtlasPanel = false;
            Hide();
            atlas.OpenMenuPage(0);
        }

        public void EnterAtlas()
        {
            if (loading) return;
            PlayerPrefs.SetInt("Atlas.StartPsychology", 0);
            StartCoroutine(LoadAtlas());
        }

        public void EnterPsychology()
        {
            if (loading) return;
            PlayerPrefs.SetInt("Atlas.StartPsychology", 1);
            StartCoroutine(LoadAtlas());
        }

        public void OpenPsychology()
        {
            restoreAtlasPanel = false;
            Hide();
            atlas.OpenPatientSimulation();
        }

        IEnumerator LoadAtlas()
        {
            loading = true;
            bool psychology = PlayerPrefs.GetInt("Atlas.StartPsychology", 0) == 1;
            PlayerPrefs.Save();
            string destination = psychology ? "PSICOLOGÍA" : "ATLAS ANATÓMICO";
            loadingLabel.text = "ABRIENDO " + destination + "…";
            Debug.Log("[Atlas Menu] Iniciando carga de " + destination);
            PlayClick();
            yield return QuestAtlasTransitions.Current.Cover("ABRIENDO " + destination);
            AsyncOperation operation = SceneManager.LoadSceneAsync(AtlasSceneName);
            if (operation == null)
            {
                loadingLabel.text = "NO SE PUDO ABRIR " + destination + ". INTÉNTALO DE NUEVO.";
                loading = false;
                QuestAtlasTransitions.Current.CancelCover();
                yield break;
            }
            int lastPercent = -1;
            while (!operation.isDone)
            {
                int percent = Mathf.Clamp(Mathf.RoundToInt(operation.progress / .9f * 100f), 0, 100);
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    QuestAtlasTransitions.Current.SetProgress("CARGANDO " + destination + " · " + percent + "%");
                    loadingLabel.text = percent < 100
                        ? "ABRIENDO " + destination + " · " + percent + "%"
                        : "PREPARANDO " + destination + "…";
                }
                yield return null;
            }
            Debug.Log("[Atlas Menu] Escena anatómica cargada para " + destination);
        }

        public void ReturnToStart()
        {
            if (loading) return;
            loading = true; PlayClick();
            StartCoroutine(ReturnToStartRoutine());
        }
        IEnumerator ReturnToStartRoutine()
        {
            yield return QuestAtlasTransitions.Current.Cover("VOLVIENDO AL MENÚ");
            AsyncOperation operation = SceneManager.LoadSceneAsync(LandingSceneName);
            while (!operation.isDone) yield return null;
        }

        public void TogglePassthrough()
        {
            bool enabled = PlayerPrefs.GetInt("Atlas.Passthrough", 1) != 1;
            PlayerPrefs.SetInt("Atlas.Passthrough", enabled ? 1 : 0);
            if (isLandingScene) ApplyLandingPassthrough(enabled);
            else atlas.Environment.SetPassthrough(enabled);
            RefreshSettings(); PlayClick();
        }

        public void ToggleEffects()
        {
            bool enabled = PlayerPrefs.GetInt("Atlas.Effects", 1) != 1;
            PlayerPrefs.SetInt("Atlas.Effects", enabled ? 1 : 0);
            if (!isLandingScene) atlas.Environment.SetEffects(enabled);
            RefreshSettings(); PlayClick();
        }

        public void ToggleSounds()
        {
            bool enabled = PlayerPrefs.GetInt("Atlas.Sounds", 1) != 1;
            PlayerPrefs.SetInt("Atlas.Sounds", enabled ? 1 : 0);
            if (!isLandingScene) atlas.Environment.SetSounds(enabled);
            RefreshSettings(); PlayClick();
        }

        public void CycleHaptics()
        {
            float gain=PlayerPrefs.GetFloat("Atlas.Haptics",1f);
            PlayerPrefs.SetFloat("Atlas.Haptics",gain>.75f?0f:gain<.25f?.5f:1f);
            PlayerPrefs.Save(); RefreshSettings();
            QuestAtlasHaptics.Play(AtlasHapticCue.System);
        }
        public void IncreaseVoiceVolume() { ChangeVoiceVolume(0.1f); }
        public void DecreaseVoiceVolume() { ChangeVoiceVolume(-0.1f); }

        void ChangeVoiceVolume(float change)
        {
            float volume = Mathf.Clamp01(PlayerPrefs.GetFloat("Atlas.VoiceVolume", 1f) + change);
            PlayerPrefs.SetFloat("Atlas.VoiceVolume", volume);
            if (!isLandingScene) atlas.Voice.SetVoiceVolume(volume);
            RefreshSettings(); PlayClick();
        }

        void RefreshSettings()
        {
            if(hapticsLabel) hapticsLabel.text="VIBRACIÓN · "+(PlayerPrefs.GetFloat("Atlas.Haptics",1f)<.25f?"DESACTIVADA":PlayerPrefs.GetFloat("Atlas.Haptics",1f)<.75f?"SUAVE":"NORMAL");
            passthroughLabel.text = PlayerPrefs.GetInt("Atlas.Passthrough", 1) == 1
                ? "PASSTHROUGH AZULADO  ·  ACTIVADO" : "PASSTHROUGH AZULADO  ·  DESACTIVADO";
            effectsLabel.text = PlayerPrefs.GetInt("Atlas.Effects", 1) == 1
                ? "PARTÍCULAS Y EFECTOS  ·  ACTIVADOS" : "PARTÍCULAS Y EFECTOS  ·  DESACTIVADOS";
            soundsLabel.text = PlayerPrefs.GetInt("Atlas.Sounds", 1) == 1
                ? "SONIDOS DEL ENTORNO  ·  ACTIVADOS" : "SONIDOS DEL ENTORNO  ·  DESACTIVADOS";
            voiceVolumeLabel.text = "VOZ DE ATLAS  ·  " + Mathf.RoundToInt(PlayerPrefs.GetFloat("Atlas.VoiceVolume", 1f) * 100f) + "%";
        }

        void ShowPage(GameObject page)
        {
            mainPage.SetActive(page == mainPage);
            settingsPage.SetActive(page == settingsPage);
            creditsPage.SetActive(page == creditsPage);
        }

        void MoveInFrontOfViewer()
        {
            Camera viewer = Camera.main;
            if (!viewer) return;
            Vector3 forward = Vector3.ProjectOnPlane(viewer.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
            transform.SetPositionAndRotation(viewer.transform.position + forward * 1.2f - Vector3.up * 0.06f,
                Quaternion.LookRotation(forward, Vector3.up));
            GetComponent<Canvas>().worldCamera = viewer;
        }

        void ApplyLandingPassthrough(bool enabled)
        {
            if(AtlasWebPlatform.Active)return;
            OVRCameraRig rig = FindFirstObjectByType<OVRCameraRig>();
            OVRManager manager = rig.GetComponent<OVRManager>();
            OVRPassthroughLayer layer = rig.GetComponent<OVRPassthroughLayer>();
            if (!layer) layer = rig.gameObject.AddComponent<OVRPassthroughLayer>();
            manager.isInsightPassthroughEnabled = enabled;
            layer.hidden = !enabled;
            layer.overridePerLayerColorScaleAndOffset = true;
            layer.colorScale = new Vector4(.84f, .95f, 1f, 1f);
            layer.colorOffset = new Vector4(0f, .008f, .035f, 0f);
            foreach (Camera camera in rig.GetComponentsInChildren<Camera>(true))
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = enabled ? new Color(0f, 0f, 0f, 0f) : new Color(.004f, .012f, .026f, 1f);
            }
        }

        void PlayClick()
        {
            if (PlayerPrefs.GetInt("Atlas.Sounds", 1) == 1 && confirmClip)
                clickAudio.PlayOneShot(confirmClip, .55f);
        }
    }
}

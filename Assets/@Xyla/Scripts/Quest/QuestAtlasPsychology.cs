using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Xyla
{
    // An educational neuropsychology activity. All wording and targets are serialized
    // so that instructors can revise them in the Unity Inspector.
    public sealed class QuestAtlasPsychology : MonoBehaviour
    {
        [Serializable]
        public sealed class Mission
        {
            public string title;
            [TextArea(2, 4)] public string question;
            public string[] targetEnglishNames;
            [TextArea(3, 6)] public string correctFeedback;
            [TextArea(2, 4)] public string hint;
            public string source;
        }

        [Header("Misiones editables")]
        public Mission[] missions;
        [Header("Tutorial editable")]
        [TextArea(2, 5)] public string[] tutorialSteps;
        [Header("Páginas de la jerarquía")]
        public GameObject homePage;
        public GameObject missionPage;
        public GameObject tutorialPage;
        [Header("Textos de las misiones")]
        public Text[] missionLabels;
        public Text missionTitle;
        public Text missionQuestion;
        public Text feedbackHeading;
        public Text feedbackBody;
        public Text progressLabel;
        public Text sourceLabel;
        [Header("Textos del tutorial")]
        public Text tutorialStepLabel;
        public Text tutorialBody;

        QuestAtlasController owner;
        readonly List<int> candidates = new List<int>();
        bool[] completed;
        int activeMission = -1;
        int tutorialStep;
        bool missionAnswered;

        public bool IsOpen => gameObject.activeSelf;

        public void Initialize(QuestAtlasController atlas)
        {
            owner = atlas;
            if (missions == null || missions.Length == 0)
                throw new InvalidOperationException("Faltan misiones de psicología en la escena.");
            if (missionLabels == null || missionLabels.Length != missions.Length || tutorialSteps.Length == 0)
                throw new InvalidOperationException("Completa las tarjetas y pasos del tutorial en la escena.");
            completed = new bool[missions.Length];
            for (int i = 0; i < missions.Length; i++)
                completed[i] = AtlasWebProgress.GetInt("Atlas.Psychology.Mission." + i, 0) == 1;
            RefreshCards();
        }

        public void Open()
        {
            gameObject.SetActive(true);
            MoveBesideViewer(false);
            owner.Learning.PauseForPsychology();
            FocusMissionStructures();
            OpenHome();
        }

        public void OpenHome()
        {
            activeMission = -1;
            homePage.SetActive(true);
            missionPage.SetActive(false);
            tutorialPage.SetActive(false);
            MoveBesideViewer(false);
        }

        public void OpenMissions()
        {
            homePage.SetActive(false);
            MoveBesideViewer(true);
            if (AtlasWebProgress.GetInt("Atlas.Psychology.TutorialSeen", 0) == 0) OpenTutorial();
            else ShowMissions();
        }

        public void Close()
        {
            HideForAtlasNavigation();
            owner.OpenMenuPage(3);
        }

        public void OpenPatient() { owner.OpenPatientSimulation(); }

        public void HideForAtlasNavigation()
        {
            activeMission = -1;
            gameObject.SetActive(false);
        }

        public void OpenTutorial()
        {
            tutorialStep = 0;
            homePage.SetActive(false);
            missionPage.SetActive(false);
            tutorialPage.SetActive(true);
            RefreshTutorial();
        }

        public void NextTutorial()
        {
            tutorialStep++;
            if (tutorialStep < tutorialSteps.Length) { RefreshTutorial(); return; }
            AtlasWebProgress.SetInt("Atlas.Psychology.TutorialSeen", 1);
            AtlasWebProgress.Save();
            ShowMissions();
        }

        public void SkipTutorial()
        {
            AtlasWebProgress.SetInt("Atlas.Psychology.TutorialSeen", 1);
            AtlasWebProgress.Save();
            ShowMissions();
        }

        public void ShowMissions()
        {
            homePage.SetActive(false);
            tutorialPage.SetActive(false);
            missionPage.SetActive(true);
            RefreshCards();
            if (activeMission < 0)
            {
                missionTitle.text = "ELIGE UNA MISIÓN";
                missionQuestion.text = "Relaciona las estructuras visibles con memoria, emoción, percepción, atención y lenguaje.";
                feedbackHeading.text = "RETROALIMENTACIÓN";
                feedbackHeading.color = new Color(.20f, .88f, .95f);
                feedbackBody.text = "Selecciona una misión. Luego apunta a una estructura o tócala para recibir una explicación inmediata.";
                sourceLabel.text = "Basado en OpenStax · Anatomía y fisiología 13.2 y 14.2";
            }
        }

        public void StartMission0() { StartMission(0); }
        public void StartMission1() { StartMission(1); }
        public void StartMission2() { StartMission(2); }
        public void StartMission3() { StartMission(3); }
        public void StartMission4() { StartMission(4); }
        public void StartMission5() { StartMission(5); }

        public void StartMission(int index)
        {
            if (index < 0 || index >= missions.Length) throw new ArgumentOutOfRangeException(nameof(index));
            activeMission = index;
            missionAnswered = false;
            ShowMissions();
            Mission mission = missions[index];
            missionTitle.text = mission.title.ToUpperInvariant();
            missionQuestion.text = mission.question;
            feedbackHeading.text = "BUSCA LA ESTRUCTURA";
            feedbackHeading.color = new Color(.20f, .88f, .95f);
            feedbackBody.text = "Apunta a una de las piezas cerebrales visibles y selecciónala. Puedes pedir una pista si la necesitas.";
            sourceLabel.text = mission.source;
            RefreshCards();
        }

        public void ShowHint()
        {
            if (activeMission < 0) { feedbackBody.text = "Selecciona primero una misión."; return; }
            feedbackHeading.text = "PISTA";
            feedbackHeading.color = new Color(1f, .73f, .36f);
            feedbackBody.text = missions[activeMission].hint;
        }

        public void NextMission()
        {
            int start = activeMission < 0 ? 0 : (activeMission + 1) % missions.Length;
            for (int offset = 0; offset < missions.Length; offset++)
            {
                int index = (start + offset) % missions.Length;
                if (!completed[index]) { StartMission(index); return; }
            }
            activeMission = -1;
            ShowMissions();
            feedbackHeading.text = "TODAS LAS MISIONES COMPLETADAS";
            feedbackHeading.color = new Color(.45f, 1f, .72f);
            feedbackBody.text = "Has relacionado las seis estructuras con sus funciones. Puedes repetir cualquier misión para repasar.";
        }

        public bool HandlePieceSelected(int index)
        {
            if (!IsOpen || activeMission < 0) return false;
            if (missionAnswered) return true;
            AtlasPart part = owner.data.parts[index];
            Mission mission = missions[activeMission];
            bool correct = Array.Exists(mission.targetEnglishNames,
                name => string.Equals(name, part.name, StringComparison.OrdinalIgnoreCase));
            owner.ShowLearningPart(index);
            if (correct)
            {
                missionAnswered = true;
                completed[activeMission] = true;
                AtlasWebProgress.SetInt("Atlas.Psychology.Mission." + activeMission, 1);
                AtlasWebProgress.Complete("neuro."+activeMission);
                AtlasWebProgress.Save();
                feedbackHeading.text = "CORRECTO · " + AtlasSpanish.Name(part.name).ToUpperInvariant();
                feedbackHeading.color = new Color(.45f, 1f, .72f);
                feedbackBody.text = mission.correctFeedback;
                owner.Learning.ShowNarration(new[] { part.id }, "Correcto: " + AtlasSpanish.Name(part.name), 5f);
            }
            else
            {
                feedbackHeading.text = "REVISA TU RESPUESTA";
                feedbackHeading.color = new Color(1f, .73f, .36f);
                feedbackBody.text = "Seleccionaste " + AtlasSpanish.Name(part.name) + ". " + mission.hint;
            }
            RefreshCards();
            return true;
        }

        void FocusMissionStructures()
        {
            candidates.Clear();
            var ids = new List<string>();
            foreach (Mission mission in missions)
                foreach (string target in mission.targetEnglishNames)
                {
                    int index = Array.FindIndex(owner.data.parts, part =>
                        part.system == "nervous" && string.Equals(part.name, target, StringComparison.OrdinalIgnoreCase));
                    if (index < 0) throw new InvalidOperationException("Falta la pieza de misión: " + target);
                    if (candidates.Contains(index)) continue;
                    candidates.Add(index);
                    ids.Add(owner.data.parts[index].id);
                }
            owner.FocusAnatomy(Array.Empty<string>(), ids.ToArray(), "only");
            owner.SetLearningInteractive(candidates);
        }

        void RefreshCards()
        {
            int done = 0;
            for (int i = 0; i < missions.Length; i++)
            {
                if (completed[i]) done++;
                missionLabels[i].text = (completed[i] ? "✓ " : activeMission == i ? "◆ " : "○ ") + missions[i].title;
                missionLabels[i].color = completed[i] ? new Color(.52f, 1f, .75f) : Color.white;
            }
            progressLabel.text = "PROGRESO  " + done + " / " + missions.Length + " MISIONES";
        }

        void RefreshTutorial()
        {
            tutorialStepLabel.text = "PASO " + (tutorialStep + 1) + " DE " + tutorialSteps.Length;
            tutorialBody.text = tutorialSteps[tutorialStep];
        }

        void MoveBesideViewer(bool besideAnatomy)
        {
            Camera viewer = Camera.main;
            if (!viewer) return;
            Vector3 forward = Vector3.ProjectOnPlane(viewer.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            transform.SetPositionAndRotation(viewer.transform.position + forward * 1.4f + right * (besideAnatomy ? .58f : 0f) - Vector3.up * .08f,
                Quaternion.LookRotation(forward, Vector3.up));
            GetComponent<Canvas>().worldCamera = viewer;
        }
    }
}

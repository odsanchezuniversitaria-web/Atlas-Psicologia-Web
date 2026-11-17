using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Xyla
{
    public sealed class QuestPatientSimulation : MonoBehaviour
    {
        [Header("Caso educativo · datos visibles editables")]
        public string patientName = "María";
        public string caseTitle = "Entrevista inicial · ansiedad generalizada";
        [TextArea(2, 5)] public string openingLine = "Hola. Últimamente me preocupa casi todo y no estoy durmiendo bien.";
        [TextArea(2, 5)] public string caseBrief = "Caso educativo: María vive con trastorno de ansiedad generalizada. Explora su experiencia, el sueño y el impacto en su vida; escucha antes de proponer conclusiones.";
        public string clinicalSource = "NICE CG113 · Evaluación de ansiedad generalizada";
        [Header("Referencias de la jerarquía")]
        public GameObject visualRoot;
        public GameObject panelRoot;
        public QuestPatientAvatar avatar;
        public QuestPatientRealisticDriver realisticDriver;
        public QuestPatientVoice voice;
        public QuestPatientTeaching teaching;
        public QuestPatientOffice office;
        public QuestInterviewResults resultsDashboard;
        float interviewStarted, interviewSeconds;
        public Text heading;
        public Text briefLabel;
        public Text statusLabel;
        public Text conversationSymbol;
        public Text transcriptLabel;
        public Text patientLabel;
        public Text feedbackLabel;
        public GameObject interviewPage;
        public GameObject feedbackPage;

        QuestAtlasController atlas;
        readonly List<string> questions = new List<string>();
        readonly JArray dialogue = new JArray();
        CancellationTokenSource lifetime;
        [Header("Evaluación anticipada · editable")]
        [Range(.2f,3f)] public float reviewDebounceSeconds = .75f;
        CancellationTokenSource reviewLifetime;
        Task<JObject> preparedReview;
        float nextIndicatorUpdate;
        int interviewGeneration;
        public bool IsOpen { get; private set; }

        public void Initialize(QuestAtlasController owner)
        {
            atlas = owner;
            voice.Initialize(owner.Voice, realisticDriver
                ? realisticDriver.speaker
                : visualRoot.GetComponentInChildren<AudioSource>(true));
            avatar.enabled = !realisticDriver;
            if (realisticDriver)
            {
                realisticDriver.Initialize(voice);
            }
            else avatar.Initialize(voice);
            atlas.spokenGuide.CanPlayPatientGuide = () => !voice.IsOpen ||
                (voice.Ready && !voice.Speaking && !voice.WaitingForReply);
            teaching.Initialize(this);
            voice.Changed += RefreshStatus;
            voice.QuestionHeard += OnQuestion;
            voice.PatientAnswered += OnAnswer;
            visualRoot.SetActive(false);
            panelRoot.SetActive(false);
        }

        public void Open()
        {
            if (IsOpen) return;
            teaching.PrepareCase();
            IsOpen = true;
            atlas.EnterClinicalPresentation();
            atlas.Voice.SuspendForPatient();
            atlas.spokenGuide.Cancel();
            lifetime = new CancellationTokenSource();
            int ticket = ++interviewGeneration;
            questions.Clear(); dialogue.Clear();
            interviewStarted = Time.realtimeSinceStartup;
            atlas.Learning.PauseForPsychology();
            atlas.FocusAnatomy(Array.Empty<string>(), Array.Empty<string>(), "only");
            atlas.ShowAvatar(false);
            visualRoot.SetActive(true);
            avatar.PlaceForViewer();
            if (realisticDriver) realisticDriver.PlaceForViewer();
            if (office) office.Enter(Camera.main, visualRoot.transform);
            teaching.emotionGauge.ShowFor(visualRoot.transform,Camera.main);
            panelRoot.SetActive(true);
            MovePanel();
            interviewPage.SetActive(true);
            feedbackPage.SetActive(false);
            heading.text = patientName.ToUpperInvariant() + " · ENTREVISTA SIMULADA";
            briefLabel.text = caseTitle + "\n" + caseBrief;
            transcriptLabel.text = "TÚ: Mantén A o X para hablar. Suelta el botón al terminar.";
            patientLabel.text = patientName.ToUpperInvariant() + ": " + openingLine;
            _ = OpenGuidedAsync(ticket, lifetime.Token);
            RefreshStatus();
        }

        async Task OpenGuidedAsync(int ticket, CancellationToken cancellation)
        {
            try
            {
                while (atlas.spokenGuide.Playing) await AtlasWebClock.Delay(50, cancellation);
                cancellation.ThrowIfCancellationRequested();
                if (!IsOpen || ticket != interviewGeneration) return;
                await atlas.spokenGuide.PlayGuide(true);
                cancellation.ThrowIfCancellationRequested();
                if (IsOpen && ticket == interviewGeneration && interviewPage.activeSelf)
                {
                    // The guide has relinquished its speaker before Maria can greet.
                    atlas.spokenGuide.Cancel();
                    voice.Open();
                }
            }
            catch (OperationCanceledException) { }
        }

        void OnQuestion(string question)
        {
            if (!IsOpen || string.IsNullOrWhiteSpace(question)) return;
            CancelPreparedReview();
            questions.Add(question);
            dialogue.Add(new JObject { ["user"] = question, ["assistant"] = "" });
            teaching.ObserveQuestion(question);
            transcriptLabel.text = "TÚ: " + question;
        }

        void OnAnswer(string answer)
        {
            if (!IsOpen) return;
            // A server response takes priority over a manually repeated instruction.
            atlas.spokenGuide.Cancel();
            if (dialogue.Count > 0) dialogue[dialogue.Count-1]["assistant"] = answer;
            patientLabel.text = patientName.ToUpperInvariant() + ": " + answer;
            if (questions.Count > 0) QueueReview();
        }

        void CancelPreparedReview()
        {
            reviewLifetime?.Cancel(); reviewLifetime?.Dispose(); reviewLifetime = null;
            preparedReview = null;
        }
        void QueueReview()
        {
            CancelPreparedReview();
            reviewLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            preparedReview = PrepareReviewAsync(questions.ToArray(), (JArray)dialogue.DeepClone(), voice.CaseId, reviewLifetime.Token);
        }
        async Task<JObject> PrepareReviewAsync(string[] asked, JArray turns, string caseId, CancellationToken cancellation)
        {
            try
            {
                await AtlasWebClock.Delay(TimeSpan.FromSeconds(reviewDebounceSeconds), cancellation);
                return await voice.FeedbackAsync(asked, turns, cancellation, caseId);
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception) { return null; } // FinishInterview retries; background errors never interrupt speech.
        }

        void RefreshStatus()
        {
            if (!IsOpen) return;
            if (voice.IsOpen && atlas.spokenGuide.Playing &&
                (voice.WaitingForReply || voice.Speaking || voice.UserSpeaking))
                atlas.spokenGuide.Cancel();
            RefreshConversationIndicator();
        }

        void Update()
        {
            if (!IsOpen || Time.unscaledTime < nextIndicatorUpdate) return;
            nextIndicatorUpdate = Time.unscaledTime + .08f;
            RefreshConversationIndicator();
        }

        void RefreshConversationIndicator()
        {
            Color color;
            if (voice.UserSpeaking)
            {
                statusLabel.text = "TÚ ESTÁS HABLANDO · SUELTA A / X AL TERMINAR";
                color = new Color(.15f, 1f, .72f);
            }
            else if (voice.Speaking)
            {
                statusLabel.text = "MARÍA ESTÁ HABLANDO";
                color = new Color(.98f, .62f, 1f);
            }
            else if (voice.WaitingForReply)
            {
                statusLabel.text = "MARÍA ESTÁ PENSANDO…";
                color = new Color(1f, .78f, .38f);
            }
            else if (voice.Ready)
            {
                statusLabel.text = "TU TURNO · MANTÉN A / X Y HABLA";
                color = new Color(.22f, .86f, .98f);
            }
            else
            {
                statusLabel.text = voice.Status.ToUpperInvariant();
                color = new Color(1f, .68f, .36f);
            }
            statusLabel.color = color;
            float pulse = .65f + .35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
            conversationSymbol.color = new Color(color.r, color.g, color.b, pulse);
        }

        public void FinishInterview() { _ = FinishInterviewAsync(); }

        async Task FinishInterviewAsync()
        {
            if (!IsOpen || feedbackPage.activeSelf) return;
            int ticket = ++interviewGeneration;
            atlas.spokenGuide.Cancel();
            interviewSeconds = Time.realtimeSinceStartup - interviewStarted;
            voice.Close();
            teaching.emotionGauge.Hide();
            interviewPage.SetActive(false);
            feedbackPage.SetActive(true);
            QuestAtlasTransitions.Current.Pulse();
            resultsDashboard.ShowPending(interviewSeconds, questions.Count);
            feedbackLabel.text = "Revisando la entrevista…";
            try
            {
                JObject result = preparedReview == null ? null : await preparedReview;
                if (!IsOpen || ticket != interviewGeneration) return;
                if (result == null)
                    result = await voice.FeedbackAsync(questions.ToArray(), (JArray)dialogue.DeepClone(), lifetime.Token);
                if (!IsOpen || ticket != interviewGeneration) return;
                ShowFeedback(result);
            }
            catch (Exception)
            {
                if (IsOpen && ticket == interviewGeneration) ShowFeedback(LocalFeedback());
            }
        }

        void ShowFeedback(JObject result)
        {
            if(AtlasWebPlatform.Active && questions.Count>0)AtlasWebProgress.Complete("case."+voice.CaseId,result);
            resultsDashboard.Display(result, interviewSeconds, questions.Count);
            var lines = new StringBuilder();
            lines.AppendLine("RETROALIMENTACIÓN DE PRÁCTICA");
            lines.AppendLine("Observado: " + result.Value<int>("observed") + " de " + result.Value<int>("total") + " habilidades · " + questions.Count + " intervenciones");
            lines.AppendLine();
            foreach (JObject criterion in (JArray)result["criteria"])
            {
                bool observed = criterion.Value<bool>("observed");
                lines.AppendLine((observed ? "✓ " : "○ ") + criterion.Value<string>("name"));
                if (observed && !string.IsNullOrEmpty(criterion.Value<string>("evidence")))
                    lines.AppendLine("   «" + criterion.Value<string>("evidence") + "»");
                if (!observed) lines.AppendLine("   " + criterion.Value<string>("suggestion"));
            }
            lines.AppendLine();
            lines.AppendLine("Caso educativo ficticio · " + clinicalSource + ". Esta revisión no equivale a una evaluación clínica.");
            feedbackLabel.text = lines.ToString();
        }

        public void RestartInterview()
        {
            if (!IsOpen) return;
            CancelPreparedReview();
            QuestAtlasTransitions.Current.Pulse();
            interviewGeneration++;
            atlas.spokenGuide.Cancel();
            questions.Clear(); dialogue.Clear();
            teaching.ResetProgress();
            teaching.emotionGauge.ShowFor(visualRoot.transform,Camera.main);
            interviewStarted = Time.realtimeSinceStartup;
            transcriptLabel.text = "TÚ: Mantén A o X para hablar. Suelta el botón al terminar.";
            patientLabel.text = patientName.ToUpperInvariant() + ": " + openingLine;
            feedbackPage.SetActive(false);
            interviewPage.SetActive(true);
            voice.Close();
            voice.Open();
        }

        JObject LocalFeedback()
        {
            // Matches the server rubric when internet is lost after an interview.
            string all = string.Join(" ", questions).ToLowerInvariant();
            string[] names = { "Inicio y permiso", "Pregunta abierta", "Preocupación y control", "Duración e impacto", "Validación", "Cierre colaborativo" };
            string[][] words = {
                new[] { "hola", "buenos", "buenas", "te parece" },
                new[] { "cuéntame", "como te has", "cómo te has", "qué te trae", "que te preocupa" },
                new[] { "preocup", "ansied", "nervios", "controlar" },
                new[] { "cuánto tiempo", "cuanto tiempo", "desde cuándo", "desde cuando", "afecta", "dormir", "sueño" },
                new[] { "entiendo", "difícil", "dificil", "gracias por contar", "te escucho" },
                new[] { "resum", "siguiente paso", "te parece si", "qué te gustaría" }
            };
            string[] suggestions = {
                "Preséntate y pide permiso para conversar.", "Invita a la paciente a contar su experiencia con una pregunta abierta.",
                "Explora qué le preocupa y si puede detener la preocupación.", "Pregunta por duración e impacto en su vida.",
                "Reconoce su experiencia sin adelantar un diagnóstico.", "Resume lo escuchado y acuerda el siguiente paso."
            };
            if (teaching.selectedCase > 0) {
                names[2] = "Exploración específica del caso";
                string[][] terms = {
                    new[] { "exponer", "clase", "evit", "personas", "evaluad" },
                    new[] { "abuela", "pérdida", "perdida", "apoy", "hermana", "rutina" },
                    new[] { "ánimo", "animo", "interés", "interes", "disfrut", "segur", "apoy" }
                };
                words[2] = terms[teaching.selectedCase-1];
                suggestions[2] = caseBrief;
            }
            var criteria = new JArray(); int observedCount = 0;
            for (int i = 0; i < names.Length; i++)
            {
                bool observed = Array.Exists(words[i], word => all.Contains(word));
                if (observed) observedCount++;
                criteria.Add(new JObject { ["name"] = names[i], ["observed"] = observed, ["suggestion"] = suggestions[i] });
            }
            return new JObject { ["evaluation_method"] = "local_fallback", ["summary"] = "Sin evaluación contextual: análisis local limitado.", ["criteria"] = criteria, ["observed"] = observedCount, ["total"] = names.Length };
        }

        public void Close()
        {
            if (!IsOpen) return;
            CancelPreparedReview();
            QuestAtlasTransitions.Current.Pulse();
            IsOpen = false;
            interviewGeneration++;
            atlas.spokenGuide.Cancel();
            lifetime.Cancel(); lifetime.Dispose(); lifetime = null;
            voice.Close();
            teaching.emotionGauge.Hide();
            if (office) office.Exit();
            visualRoot.SetActive(false);
            panelRoot.SetActive(false);
            atlas.ShowAvatar(true);
            atlas.Voice.ResumeFromPatient();
            atlas.OpenPsychology();
        }

        void MovePanel()
        {
            Camera viewer = Camera.main;
            Vector3 forward = Vector3.ProjectOnPlane(viewer.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            panelRoot.transform.SetPositionAndRotation(viewer.transform.position + forward * 1.27f + right * .82f - Vector3.up * .08f,
                Quaternion.LookRotation(forward * 1.27f + right * .82f - Vector3.up * .08f, Vector3.up));
            panelRoot.GetComponent<Canvas>().worldCamera = viewer;
        }

        void OnDestroy()
        {
            CancelPreparedReview();
            interviewGeneration++;
            if (lifetime != null) { lifetime.Cancel(); lifetime.Dispose(); }
        }
    }
}

using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Xyla
{
    public sealed class QuestInterviewResults : MonoBehaviour
    {
        public Text scoreLabel, durationLabel, detailLabel, basisLabel;
        public Text[] criterionLabels;
        public Image[] criterionBars;
        public Image[] ringSegments;
        public Button[] criterionButtons;
        JObject lastResult;
        public JObject CurrentReport=>lastResult;

        public void ShowPending(float seconds, int turns)
        {
            lastResult = null;
            scoreLabel.text = "…\n<size=18>REVISANDO</size>";
            durationLabel.text = "DURACIÓN " + TimeSpan.FromSeconds(Mathf.Max(0, seconds)).ToString(@"mm\:ss") + "   ·   " + turns + " INTERVENCIONES";
            basisLabel.text = "Revisando la conversación y sus evidencias";
            detailLabel.text = "Estamos contrastando tus intervenciones con las respuestas de María. Las tarjetas se completarán al terminar.";
            Color color = new Color(.28f,.54f,.64f);
            for (int i=0;i<criterionLabels.Length;i++)
            {
                string title = criterionLabels[i].text.Split('\n')[0];
                criterionLabels[i].text = title + "\n<size=18>EN REVISIÓN</size>";
                criterionBars[i].color = color;
                criterionBars[i].rectTransform.localScale = new Vector3(.02f,1,1);
                ringSegments[i].color = color;
                criterionButtons[i].interactable = false;
            }
        }

        public void Display(JObject result, float seconds, int turns)
        {
            lastResult = result;
            int total = result.Value<int>("total"), observed = result.Value<int>("observed");
            float score = result.Value<float?>("score_percent") ?? (total > 0 ? 100f * observed / total : 0);
            scoreLabel.text = Mathf.RoundToInt(score) + "%\n<size=18>DESEMPEÑO</size>";
            durationLabel.text = "DURACIÓN " + TimeSpan.FromSeconds(Mathf.Max(0, seconds)).ToString(@"mm\:ss") + "   ·   " + turns + " INTERVENCIONES";
            basisLabel.text = result.Value<string>("evaluation_method") == "semantic_dialogue" ? "Evaluación contextual · diálogo y evidencias reales" : "Análisis básico limitado · requiere revisión";
            var criteria = (JArray)result["criteria"];
            for (int i = 0; i < criterionLabels.Length; i++)
            {
                var item = (JObject)criteria[i];
                float percent = item.Value<float?>("score_percent") ?? (item.Value<bool>("observed") ? 100f : 0f);
                bool found = percent >= 67f;
                Color color = found ? new Color(.18f, .95f, .68f) : new Color(1f, .73f, .2f);
                criterionLabels[i].text = item.Value<string>("name").ToUpperInvariant() + "\n<size=18>" + Mathf.RoundToInt(percent) + "% · " + (percent >= 100 ? "COMPLETO" : percent > 0 ? "PARCIAL" : "POR EXPLORAR") + "</size>";
                criterionBars[i].color = color;
                criterionBars[i].rectTransform.localScale = new Vector3(Mathf.Max(.02f, percent / 100f), 1, 1);
                ringSegments[i].color = color;
                int index = i;
                criterionButtons[i].interactable = true;
                criterionButtons[i].onClick.RemoveAllListeners();
                criterionButtons[i].onClick.AddListener(() => SelectCriterion(index));
            }
            if (result.Value<string>("evaluation_method") == "semantic_dialogue")
            {
                detailLabel.text = "RETROALIMENTACIÓN\n" + Clip(result.Value<string>("summary"), 300)
                    + "\nFortaleza: " + Clip(result.Value<string>("strength"), 180)
                    + "\nPrioridad: " + Clip(result.Value<string>("priority"), 200)
                    + "\nToca una tarjeta para ver lo que dijiste y cómo respondió María.";
                return;
            }
            int lowest = 0, highest = 0;
            for (int i = 1; i < criteria.Count; i++)
            {
                if (Percent((JObject)criteria[i]) < Percent((JObject)criteria[lowest])) lowest = i;
                if (Percent((JObject)criteria[i]) > Percent((JObject)criteria[highest])) highest = i;
            }
            var best = (JObject)criteria[highest]; var next = (JObject)criteria[lowest];
            detailLabel.text = "RETROALIMENTACIÓN\n" + (Percent(best) > 0 ? "Fortaleza: " + best.Value<string>("name") + "." : "Aún no hay evidencia suficiente en la entrevista.") +
                "\nA mejorar: " + next.Value<string>("name") + ". " + next.Value<string>("suggestion") +
                "\nToca una tarjeta para ver sus indicadores. No equivale a una calificación clínica.";
        }

        static string Clip(string value, int limit) => string.IsNullOrEmpty(value) ? "" : value.Length <= limit ? value : value.Substring(0, limit) + "…";

        static float Percent(JObject item) => item.Value<float?>("score_percent") ?? (item.Value<bool>("observed") ? 100f : 0f);

        public void SelectCriterion(int index)
        {
            if (lastResult == null) return;
            var item = (JObject)((JArray)lastResult["criteria"])[index];
            if (item["evidence_turns"] is JArray turns && turns.Count > 0)
            {
                var turn = (JObject)turns[0];
                detailLabel.text = item.Value<string>("name").ToUpperInvariant() + " · " + Percent(item) + "%"
                    + "\n" + Clip(item.Value<string>("rationale"), 210)
                    + "\nIntervención " + turn.Value<int>("turn") + " · Tú: «" + Clip(turn.Value<string>("student"), 170) + "»"
                    + "\nMaría: «" + Clip(turn.Value<string>("patient"), 170) + "»"
                    + "\nPróximo paso: " + Clip(item.Value<string>("suggestion"), 180);
                return;
            }
            string indicators = "";
            if (item["indicators"] is JArray list)
                foreach (JObject indicator in list) indicators += (indicator.Value<bool>("observed") ? "✓ " : "○ ") + indicator.Value<string>("name") + "   ";
            detailLabel.text = indicators + "\n" + item.Value<string>("name").ToUpperInvariant() + "\n" +
                (item.Value<bool>("observed") ? "Evidencia: «" + item.Value<string>("evidence") + "»" : "No se detectó evidencia en la transcripción.") +
                "\nPróximo paso: " + item.Value<string>("suggestion");
        }
    }
}

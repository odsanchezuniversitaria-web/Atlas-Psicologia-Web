using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Xyla
{
    public sealed partial class QuestAtlasVoiceAgent
    {
        [Header("Voz por LiveKit · editable")]
        public QuestLiveKitVoice liveKit;
        void InitializeLiveKit()
        {
            if (!liveKit) liveKit = GetComponent<QuestLiveKitVoice>() ?? gameObject.AddComponent<QuestLiveKitVoice>();
            liveKit.Message += LiveKitMessage;
            liveKit.Failed += message => { Busy = false; HasError = true; Listening = false; SetStatus(message); };
        }
        public Task<JObject> LiveKitConnectionAsync(string mode, string caseId, CancellationToken cancellation, string sessionId = null)
        {
            LoadConnection(); ValidateConnection(connection);
            return RequestAsync("/livekit/session", new JObject { ["session_id"] = sessionId ?? session,
                ["mode"] = mode, ["case_id"] = caseId, ["protocol"] = 1 }, cancellation);
        }
        async Task<bool> OpenLiveKitAsync(CancellationToken cancellation)
        {
            if (!liveKit.preferLiveKit) return false;
            try
            {
                var details = await LiveKitConnectionAsync("atlas", "maria_anxiety_v1", cancellation);
                return await liveKit.OpenAsync(details, "atlas", speaker,
                    () => { if (capturedContext == null || DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (capturedContext.Value<long?>("captured_at") ?? 0) > 30000) CaptureContext(); return capturedContext; },
                    async (batch, token) => {
                        PerformingAction = true; SetStatus("Aplicando la orden en el atlas…");
                        try
                        {
                            var results = await actions.ExecuteAsync(batch, token, () => liveKit.Connected && Listening && !patientMode);
                            foreach (var receipt in results) if (receipt.Value<string>("status") == "applied") LastActionAt = Time.unscaledTime;
                            return results;
                        }
                        finally { PerformingAction = false; }
                    }, cancellation);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { liveKit.Close(); Debug.LogWarning("[Atlas LiveKit] Respaldo de voz anterior: " + ex.Message); return false; }
        }
        void LiveKitMessage(JObject message)
        {
            if (patientMode) return;
            string type = message.Value<string>("type");
            if (type == "user_state" && message.Value<string>("state") == "speaking") { CaptureContext(); }
            else if (type == "transcript_partial") { Transcript = message.Value<string>("text") ?? ""; Changed?.Invoke(); }
            else if (type == "transcript_final") { Transcript = message.Value<string>("text") ?? ""; Busy = true; SetStatus("Atlas está pensando…"); }
            else if (type == "result")
            {
                var response = (JObject)message["payload"];
                Transcript = response.Value<string>("transcript") ?? Transcript; Answer = response.Value<string>("answer") ?? "";
                var ids = response["part_ids"] as JArray;
                if (ids != null) owner.Learning.ShowNarration(ids.ToObject<string[]>(), Answer, 12f);
                if (Answer.TrimEnd().EndsWith("?")) replyWindow.Open(Time.unscaledTime, 12f);
                Changed?.Invoke();
            }
            else if (type == "agent_state")
            {
                string state = message.Value<string>("state"); Busy = state == "thinking";
                SetStatus(state == "speaking" ? "Atlas responde" : state == "thinking" ? "Atlas está pensando…" : IdleHint);
            }
            else if (type == "error") { Busy = false; SetStatus(message.Value<string>("message") ?? "Error de voz."); }
        }
    }
}

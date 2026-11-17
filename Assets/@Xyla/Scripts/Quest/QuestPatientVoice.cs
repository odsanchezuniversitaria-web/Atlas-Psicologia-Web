using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Xyla
{
    // Reuses the paired Atlas service, but owns a separate microphone turn and patient socket.
    public sealed class QuestPatientVoice : MonoBehaviour
    {
        public QuestLiveKitVoice liveKit;
        public string CaseId = "maria_anxiety_v1";
        public event Action<JObject> InteractionReceived;
        public event Action Changed;
        public event Action<string> QuestionHeard;
        public event Action<string> PatientAnswered;
        public string Status { get; private set; } = "Paciente en espera";
        public string Transcript { get; private set; } = "";
        public string Answer { get; private set; } = "";
        public string Mood { get; private set; } = "worried";
        public bool IsOpen { get; private set; }
        public bool Ready { get; private set; }
        public bool Speaking => liveKit && liveKit.Connected ? liveKit.Speaking : speaker.isPlaying;
        public bool UserSpeaking => capturing;
        public bool TalkButtonHeld => held;
        public bool WaitingForReply => waiting;
        public float SpeechLevel { get; private set; }

        QuestAtlasVoiceAgent atlasVoice;
        AudioSource speaker;
        AudioClip microphone, replyClip;
        ClientWebSocket socket;
        CancellationTokenSource lifetime;
        readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        readonly float[] frame = new float[320], meter = new float[128];
        readonly List<float> recorded = new List<float>(16000 * 15);
        PcmRing ring;
        MemoryStream receivedPcm;
        bool playbackStarted;
        int microphonePosition;
        bool held, capturing, waiting, resumeLiveKit;
        string session;
        string lastServerError;

        sealed class PcmRing
        {
            readonly object gate = new object();
            readonly float[] samples = new float[24000 * 90];
            int read, write, count;
            public bool Ended { get; private set; }
            public int Buffered { get { lock (gate) return count; } }
            public void Append(byte[] bytes)
            {
                lock (gate)
                {
                    for (int i = 0; i + 1 < bytes.Length; i += 2)
                    {
                        if (count == samples.Length) { read = (read + 1) % samples.Length; count--; }
                        samples[write] = (short)(bytes[i] | bytes[i + 1] << 8) / 32768f;
                        write = (write + 1) % samples.Length;
                        count++;
                    }
                }
            }
            public void Finish() { lock (gate) Ended = true; }
            public void Read(float[] output)
            {
                lock (gate)
                {
                    int available = Math.Min(output.Length, count);
                    for (int i = 0; i < available; i++)
                    { output[i] = samples[read]; read = (read + 1) % samples.Length; }
                    count -= available;
                    for (int i = available; i < output.Length; i++) output[i] = 0f;
                }
            }
        }

        public void Initialize(QuestAtlasVoiceAgent voice, AudioSource patientSpeaker)
        {
            atlasVoice = voice;
            speaker = patientSpeaker;
            speaker.playOnAwake = false;
            // Dialogue stays audible regardless of the avatar scale or viewer distance.
            speaker.spatialBlend = 0f;
            speaker.spatialize = false;
            speaker.dopplerLevel = 0f;
            speaker.outputAudioMixerGroup = null;
            speaker.mute = false;
            speaker.volume = 1f;
            speaker.priority = 0;
            if (!liveKit) liveKit = GetComponent<QuestLiveKitVoice>() ?? gameObject.AddComponent<QuestLiveKitVoice>();
            liveKit.Message += LiveKitMessage;
            liveKit.Failed += message => { Ready = false; waiting = false; SetStatus(message); };
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            Ready = false; held = false; capturing = false; waiting = true;
            session = Guid.NewGuid().ToString("N");
            lastServerError = null;
            Mood = "worried"; Transcript = ""; Answer = "";
            atlasVoice.SuspendForPatient();
            lifetime = new CancellationTokenSource();
            SetStatus("Conectando con la paciente…");
            _ = ConnectAsync(lifetime.Token);
        }

        async Task ConnectAsync(CancellationToken cancellation)
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
                {
                    UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                    float until = Time.realtimeSinceStartup + 20f;
                    while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone)
                        && Time.realtimeSinceStartup < until)
                        await AtlasWebClock.Delay(100, cancellation);
                    if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
                        throw new InvalidOperationException("Permite el micrófono para entrevistar a la paciente.");
                }
#endif
                if (liveKit.preferLiveKit)
                {
                    try
                    {
                        var details = await atlasVoice.LiveKitConnectionAsync("patient", CaseId, cancellation, session);
                        await liveKit.OpenAsync(details, "patient", speaker, () => new JObject(),
                            (batch, token) => Task.FromException<JArray>(new InvalidOperationException("La paciente no controla piezas.")), cancellation);
                        cancellation.ThrowIfCancellationRequested();
                        Ready = true; return;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception error)
                    {
                        liveKit.Close();
                        Debug.LogWarning("[Atlas Patient LiveKit] Respaldo anterior: " + error.Message);
                    }
                }
                if (Microphone.devices.Length == 0) throw new InvalidOperationException("No se encontró el micrófono de las gafas.");
                microphone = Microphone.Start(null, true, 10, 16000);
                float deadline = Time.realtimeSinceStartup + 5f;
                while (Microphone.GetPosition(null) <= 0 && Time.realtimeSinceStartup < deadline)
                    await AtlasWebClock.Delay(40, cancellation);
                if (Microphone.GetPosition(null) <= 0 || microphone.channels != 1 || microphone.frequency != 16000)
                    throw new InvalidOperationException("El micrófono no inició a 16 kHz.");
                microphonePosition = Microphone.GetPosition(null);
                string bearer = await atlasVoice.PatientAccessTokenAsync(cancellation);
                cancellation.ThrowIfCancellationRequested();
                var source = new Uri(atlasVoice.PatientServerUrl + "/realtime?session_id=" + session);
                var wsUri = new UriBuilder(source) { Scheme = source.Scheme == "https" ? "wss" : "ws" }.Uri;
                var connection = new ClientWebSocket();
                socket = connection;
                connection.Options.SetRequestHeader("Authorization", "Bearer " + bearer);
                connection.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                using (var connectionTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                {
                    connectionTimeout.CancelAfter(TimeSpan.FromSeconds(12));
                    try { await connection.ConnectAsync(wsUri, connectionTimeout.Token); }
                    catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
                    { throw new TimeoutException("El servidor de la paciente no respondió a tiempo."); }
                }
                cancellation.ThrowIfCancellationRequested();
                await SendJsonAsync(new JObject { ["type"] = "start", ["session_id"] = session,
                    ["mode"] = "patient", ["case_id"] = CaseId, ["reset_patient"] = true }, cancellation);
                _ = ReceiveAsync(connection, cancellation);
                float readyDeadline = Time.realtimeSinceStartup + 12f;
                while (!Ready && IsOpen && Time.realtimeSinceStartup < readyDeadline)
                    await AtlasWebClock.Delay(100, cancellation);
                if (!Ready && IsOpen)
                    throw new TimeoutException("La paciente no confirmó la conexión.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (cancellation.IsCancellationRequested) return;
                Ready = false; waiting = false;
                socket?.Abort();
                if (IsOpen) SetStatus("Paciente sin conexión · " + (lastServerError ?? ex.Message));
            }
        }

        public void SetPushToTalk(bool pressed)
        {
            held = pressed;
            if (liveKit && liveKit.Connected)
            {
                capturing = pressed; liveKit.SetPushToTalk(pressed);
                SetStatus(pressed ? "Te escucho · suelta A o X al terminar" : "María está pensando…");
            }
        }

        void LiveKitMessage(JObject message)
        {
            if (!IsOpen) return;
            string type = message.Value<string>("type");
            if (type == "agent_state")
            {
                string state = message.Value<string>("state");
                waiting = state == "thinking";
                SetStatus(state == "speaking" ? "María responde…" : state == "thinking" ? "María está pensando…" : "Tu turno · mantén A o X para hablar");
            }
            else if (type == "patient_interaction") InteractionReceived?.Invoke((JObject)message["interaction"]);
            else ReceiveMessage(message);
        }

        void Update()
        {
            if (!IsOpen) return;
            if (liveKit && liveKit.Connected) { SpeechLevel = liveKit.SpeechLevel; return; }
            if (speaker.isPlaying)
            {
                speaker.GetOutputData(meter, 0);
                float energy = 0f;
                for (int i = 0; i < meter.Length; i++) energy += meter[i] * meter[i];
                SpeechLevel = Mathf.Clamp01(Mathf.Sqrt(energy / meter.Length) * 8f);
            }
            else SpeechLevel = 0f;
            if (playbackStarted && !speaker.isPlaying)
            { playbackStarted = false; SetStatus("Tu turno · mantén A o X para hablar"); }
            if (!Ready || !microphone) return;
            int position = Microphone.GetPosition(null);
            if (position < 0) { SetStatus("Se perdió el micrófono."); return; }
            if (held && !capturing && !waiting)
            {
                speaker.Stop(); ReleaseClip();
                recorded.Clear(); capturing = true; microphonePosition = position;
                SetStatus("Te escucho · suelta A o X al terminar");
            }
            if (capturing)
            {
                int available = (position - microphonePosition + microphone.samples) % microphone.samples;
                while (available >= frame.Length && recorded.Count < 16000 * 15)
                {
                    microphone.GetData(frame, microphonePosition);
                    microphonePosition = (microphonePosition + frame.Length) % microphone.samples;
                    available -= frame.Length;
                    recorded.AddRange(frame);
                }
                if (!held)
                {
                    capturing = false;
                    float[] speech = AtlasVoiceDetector.CompactPauses(recorded.ToArray());
                    recorded.Clear();
                    if (speech.Length < 4800) SetStatus("No te escuché. Mantén A o X mientras hablas.");
                    else { waiting = true; SetStatus("María está pensando…"); _ = SendTurnAsync(speech, lifetime.Token); }
                }
            }
            else microphonePosition = position;
        }

        async Task SendTurnAsync(float[] speech, CancellationToken cancellation)
        {
            try
            {
                await SendJsonAsync(new JObject { ["type"] = "context", ["session_id"] = session,
                    ["mode"] = "patient", ["case_id"] = CaseId, ["manual"] = true }, cancellation);
                byte[] pcm = AtlasVoiceDetector.Pcm16(speech);
                for (int offset = 0; offset < pcm.Length; offset += 32000)
                {
                    int count = Math.Min(32000, pcm.Length - offset);
                    var chunk = new byte[count];
                    Buffer.BlockCopy(pcm, offset, chunk, 0, count);
                    await SendBytesAsync(chunk, WebSocketMessageType.Binary, cancellation);
                }
                await SendBytesAsync(new byte[32000], WebSocketMessageType.Binary, cancellation);
                await SendJsonAsync(new JObject { ["type"] = "stop_audio" }, cancellation);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!cancellation.IsCancellationRequested)
                { waiting = false; SetStatus("No se pudo enviar la pregunta · " + ex.Message); }
            }
        }

        async Task SendJsonAsync(JObject value, CancellationToken cancellation)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value.ToString(Newtonsoft.Json.Formatting.None));
            await SendBytesAsync(bytes, WebSocketMessageType.Text, cancellation);
        }

        async Task SendBytesAsync(byte[] bytes, WebSocketMessageType kind, CancellationToken cancellation)
        {
            await sendLock.WaitAsync(cancellation);
            try
            {
                cancellation.ThrowIfCancellationRequested();
                if (socket == null || socket.State != WebSocketState.Open)
                    throw new InvalidOperationException("Se perdió la conexión con la paciente.");
                await socket.SendAsync(new ArraySegment<byte>(bytes), kind, true, cancellation);
            }
            finally { sendLock.Release(); }
        }

        async Task ReceiveAsync(ClientWebSocket connection, CancellationToken cancellation)
        {
            byte[] chunk = new byte[65536];
            try
            {
                while (!cancellation.IsCancellationRequested && connection.State == WebSocketState.Open)
                {
                    using (var message = new MemoryStream())
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await connection.ReceiveAsync(new ArraySegment<byte>(chunk), cancellation);
                            if (result.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("Conexión cerrada.");
                            message.Write(chunk, 0, result.Count);
                            if (message.Length > 4000000) throw new InvalidOperationException("Respuesta demasiado grande.");
                        } while (!result.EndOfMessage);
                        cancellation.ThrowIfCancellationRequested();
                        if (socket != connection || !IsOpen) return;
                        if (result.MessageType == WebSocketMessageType.Binary) ReceiveAudio(message.ToArray());
                        else ReceiveMessage(JObject.Parse(Encoding.UTF8.GetString(message.ToArray())));
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!cancellation.IsCancellationRequested && socket == connection)
                { Ready = false; waiting = false; if (IsOpen) SetStatus("Paciente sin conexión · " + (lastServerError ?? ex.Message)); }
            }
        }

        void ReceiveMessage(JObject message)
        {
            string kind = message.Value<string>("type");
            if (kind == "ready") { SetStatus("Esperando el saludo del caso…"); }
            else if (kind == "patient_greeting")
            {
                Ready = true; waiting = true;
                Answer = message.Value<string>("text") ?? "";
                if (!string.IsNullOrEmpty(Answer)) PatientAnswered?.Invoke(Answer);
                SetStatus("María se presenta…");
            }
            else if (kind == "transcript_partial")
            { Transcript = message.Value<string>("text") ?? ""; Changed?.Invoke(); }
            else if (kind == "transcript_final")
            { Transcript = message.Value<string>("text") ?? ""; QuestionHeard?.Invoke(Transcript); SetStatus("María está pensando…"); }
            else if (kind == "result")
            {
                JObject payload = message["payload"] as JObject;
                if (payload != null && payload.Value<string>("state") == "patient_answer")
                {
                    Answer = payload.Value<string>("answer") ?? "";
                    Mood = payload.Value<string>("mood") ?? "worried";
                    if (payload["interaction"] is JObject interaction) InteractionReceived?.Invoke(interaction);
                    // Text arrives before its audio; keep this turn reserved until audio_end.
                    waiting = true;
                    PatientAnswered?.Invoke(Answer);
                    SetStatus("María responde…");
                }
                else
                {
                    waiting = false;
                    SetStatus(payload?.Value<string>("message") ?? "No se recibió una respuesta de María. Intenta preguntar de nuevo.");
                }
            }
            else if (kind == "audio_start")
            {
                waiting = true;
                speaker.Stop(); ReleaseClip();
                receivedPcm = new MemoryStream();
            }
            else if (kind == "audio_end")
            {
                byte[] pcm = receivedPcm?.ToArray(); receivedPcm?.Dispose(); receivedPcm = null;
                waiting = false;
                if (pcm != null && pcm.Length > 0 && message.Value<bool?>("interrupted") != true)
                {
                    replyClip = QuestDialogueAudio.Play(speaker, pcm, "Voz de María");
                    playbackStarted = true; SetStatus("María responde…");
                }
                else SetStatus("No se recibió voz para esta respuesta. Vuelve a preguntar.");
                Changed?.Invoke();
            }
            else if (kind == "audio_error" || kind == "error")
            {
                ring?.Finish(); waiting = false;
                string error = message.Value<string>("message") ?? "Audio no disponible.";
                if (kind == "error") lastServerError = error;
                SetStatus(error);
            }
            else if (kind == "state")
            { waiting = false; SetStatus("Te escucho · mantén A o X para hablar"); }
        }

        void ReceiveAudio(byte[] pcm)
        {
            if (receivedPcm == null) return;
            if (receivedPcm.Length + pcm.Length > 4000000) throw new InvalidOperationException("Audio demasiado largo.");
            receivedPcm.Write(pcm, 0, pcm.Length);
        }

        public async Task<JObject> FeedbackAsync(string[] questions, JArray dialogue, CancellationToken cancellation, string caseId = null)
        {
            if(AtlasWebPlatform.Active)return await AtlasWebPlatform.RequestAsync("/patient/feedback",new JObject{["case_id"]=caseId??CaseId,["questions"]=new JArray(questions),["turns"]=dialogue},cancellation);
            string bearer = await atlasVoice.PatientAccessTokenAsync(cancellation);
            byte[] body = Encoding.UTF8.GetBytes(new JObject {
                ["case_id"] = caseId ?? CaseId, ["questions"] = new JArray(questions), ["turns"] = dialogue
            }.ToString(Newtonsoft.Json.Formatting.None));
            using (var request = new UnityWebRequest(atlasVoice.PatientServerUrl + "/patient/feedback", "POST"))
            {
                request.timeout = 24;
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization", "Bearer " + bearer);
                request.SetRequestHeader("Content-Type", "application/json");
                var operation = request.SendWebRequest();
                while (!operation.isDone) { cancellation.ThrowIfCancellationRequested(); await AtlasWebClock.Delay(25, cancellation); }
                if (request.result != UnityWebRequest.Result.Success)
                    throw new InvalidOperationException("No se pudo obtener la retroalimentación de la entrevista.");
                return JObject.Parse(request.downloadHandler.text);
            }
        }

        public void Close()
        {
            if (liveKit) liveKit.Close();
            IsOpen = false; Ready = false; held = false; capturing = false; waiting = false;
            lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
            if (microphone) { Microphone.End(null); Destroy(microphone); microphone = null; }
            speaker.Stop(); ReleaseClip();
            socket?.Abort(); socket?.Dispose(); socket = null;
            ring = null;
            SetStatus("Paciente en espera");
        }

        void ReleaseClip()
        {
            speaker.clip = null;
            if (replyClip) Destroy(replyClip);
            replyClip = null; ring = null; playbackStarted = false;
            receivedPcm?.Dispose(); receivedPcm = null;
        }

        void SetStatus(string value) { Status = value; Debug.Log("[Atlas Patient] " + value); Changed?.Invoke(); }
        void OnApplicationPause(bool paused)
        {
            if (paused && IsOpen && liveKit && liveKit.Connected)
            {
                resumeLiveKit = true; Ready = false; waiting = false;
                liveKit.Close(); SetStatus("Entrevista pausada · vuelve a ponerte las gafas");
            }
            else if (!paused && resumeLiveKit && IsOpen)
            {
                resumeLiveKit = false; _ = ConnectAsync(lifetime.Token);
            }
        }
        void OnDestroy() { if (IsOpen) Close(); sendLock.Dispose(); }
    }
}

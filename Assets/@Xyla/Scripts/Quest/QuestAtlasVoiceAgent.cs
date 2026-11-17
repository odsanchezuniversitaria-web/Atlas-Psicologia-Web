using System;
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
    [Serializable]
    public sealed class AtlasVoiceConnection
    {
        public string serverUrl = QuestAtlasVoiceAgent.DefaultServerUrl;
        public string appId = "";
        public string appSecret = "";
        public string deviceToken = "";
        public bool allowLocalHttp = false;
    }

    public sealed partial class QuestAtlasVoiceAgent : MonoBehaviour
    {
        public const string DefaultServerUrl = "https://api.oscar-vr.iudcdesarrollo.tech/api/v1/atlas";
        public string Status { get; private set; } = "Asistente sin configurar";
        public string Transcript { get; private set; } = "";
        public const string IdleHint = "Mantén Hablar o espacio; en VR, A";
        public string Answer { get; private set; } = "Mantén pulsado A o X mientras hablas y suelta para enviar. Pregunta por anatomía o pide: «activa el sistema muscular», «acércame el hueso del muslo» o «acércame lo que señalo».";
        public string ContextLabel { get; private set; } = "Pregunta general";
        public bool Listening { get; private set; }
        public bool Busy { get; private set; }
        public bool Speaking => liveKit && liveKit.Connected ? liveKit.Speaking : speaker && speaker.isPlaying;
        public bool PushToTalkActive { get; private set; }
        public bool HearingSpeech => Listening && (PushToTalkActive || detector.Recording || (liveKit && liveKit.UserSpeaking));
        public bool AwaitingReply => Listening && replyWindow.IsOpen(Time.unscaledTime);
        public bool HasError { get; private set; }
        public bool PerformingAction { get; private set; }
        public bool Reconnecting { get; private set; }
        public float LastActionAt { get; private set; } = -100f;
        readonly float[] speechMeter = new float[128];

        public float GetSpeechLevel()
        {
            if (liveKit && liveKit.Connected) return liveKit.SpeechLevel;
            if (!Speaking || !replyClip || replyClip.samples < speechMeter.Length) return 0f;
            int offset = Mathf.Clamp(speaker.timeSamples, 0, replyClip.samples - speechMeter.Length);
            replyClip.GetData(speechMeter, offset);
            float energy = 0;
            for (int i = 0; i < speechMeter.Length; i++) energy += speechMeter[i] * speechMeter[i];
            return Mathf.Clamp01(Mathf.Sqrt(energy / speechMeter.Length) * 6f);
        }
        public const string WelcomeText = "Hola, soy Atlas, tu asistente de anatomía. Puedes explorar las piezas, estudiar los sistemas y practicar entrevistas con nuestra paciente. Para hablar, mantén pulsado el botón Hablar, la barra espaciadora en PC o el botón A en realidad virtual. Suelta al terminar. Señala una pieza y pregunta qué es o para qué sirve. También puedes pedirme que muestre un sistema o acerque una pieza. Comencemos.";
        public bool IsWelcoming { get; private set; }
        bool welcomeStarted, enabling, requestingPermission, resumeAfterPause, wantsListening, patientMode;
        byte[] lastAnswerPcm;
        public void ScheduleWelcome() { _ = WelcomeWhenReadyAsync(); }
        async Task WelcomeWhenReadyAsync()
        {
            try
            {
                await AtlasWebClock.Delay(1200,lifetime.Token);
#if UNITY_ANDROID && !UNITY_EDITOR
                if(!Application.isFocused || (!AtlasWebPlatform.Active && (!OVRManager.instance || !OVRManager.instance.isUserPresent)))
                    SetStatus("Atlas espera a que abras la app y te pongas las gafas");
                while(!Application.isFocused || (!AtlasWebPlatform.Active && (!OVRManager.instance || !OVRManager.instance.isUserPresent))) await AtlasWebClock.Delay(300,lifetime.Token);
#endif
                if(!patientMode&&!welcomeStarted&&!Listening&&!Busy) await PlayWelcomeAsync();
            }
            catch(OperationCanceledException) { }
        }
        public void ToggleWelcome()
        {
            if(patientMode)return;
            if(IsWelcoming) {SkipWelcome();return;}
            if(!Busy) { owner.spokenGuide.Cancel(); _ = PlayWelcomeAsync(); }
        }
        public void SkipWelcome(bool activateVoice = true)
        {
            if(!IsWelcoming)return;
            generation++;speaker.Stop();ReleaseReply();IsWelcoming=false;Busy=false;
            suppressCaptureUntil=Time.unscaledTime+.45f;
            SetStatus(Listening?IdleHint:"Preparando la escucha…");
            if(activateVoice&&!Listening) _ = EnableAsync();
        }
        async Task PlayWelcomeAsync()
        {
            if(patientMode)return;
            owner.spokenGuide.Cancel();
            int ticket=++generation;
            welcomeStarted=true;IsWelcoming=true;Busy=true;HasError=false;
            try
            {
                speaker.Stop();ReleaseReply();
                byte[] welcomePcm=await QuestAtlasSpeechCache.GetAsync(this,WelcomeText,lifetime.Token);
                if(ticket!=generation || patientMode)return;
                replyClip=QuestAtlasSpeechCache.Clip(welcomePcm,"Bienvenida de Atlas");
                ownsReplyClip=true;
                speaker.clip=replyClip;Transcript="";Answer=WelcomeText;
                SetStatus("Bienvenida de Atlas · voz generada por IA");
                speaker.Play();
                nextInterruptAt = Time.unscaledTime + 1.1f;
                float startDeadline=Time.realtimeSinceStartup+1f;
                while(!speaker.isPlaying&&ticket==generation&&Time.realtimeSinceStartup<startDeadline)await AtlasWebClock.Delay(25,lifetime.Token);
                while(speaker.isPlaying&&ticket==generation)await AtlasWebClock.Delay(50,lifetime.Token);
                if(ticket==generation)
                {
                    IsWelcoming=false;Busy=false;suppressCaptureUntil=Time.unscaledTime+.45f;
                    ReleaseReply();
                    await owner.spokenGuide.PlayGuide(false);
                    if(ticket!=generation || patientMode)return;
                    if(!Listening) { Debug.Log("[Atlas Voice] Welcome finished; starting listening."); await EnableAsync(); }
                    else SetStatus(IdleHint);
                }
            }
            catch(OperationCanceledException) { }
            catch(Exception ex)
            {
                if(ticket==generation){
                    IsWelcoming=false;Busy=false;HasError=true;Answer=WelcomeText;SetStatus(ex.Message);
                    if(!patientMode&&!Listening)await EnableAsync();
                }
            }
        }
        public bool MuteAfterReply { get; set; }
        public void SetVoiceVolume(float value) { speaker.volume = Mathf.Clamp01(value); }
        public float VoiceVolume => speaker.volume;
        public event Action Changed;
        static string privateConnectionPath;
        static string LegacyConnectionPath => Path.Combine(Application.persistentDataPath, "atlas-voice-connection.json");
        public static string ConnectionPath
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (!string.IsNullOrEmpty(privateConnectionPath)) return privateConnectionPath;
                try
                {
                    using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var files = activity.Call<AndroidJavaObject>("getFilesDir"))
                        return privateConnectionPath = Path.Combine(files.Call<string>("getAbsolutePath"), "atlas-voice-connection.json");
                }
                catch (Exception)
                {
                    return Path.Combine("/data/user/0", Application.identifier, "files", "atlas-voice-connection.json");
                }
#else
                return LegacyConnectionPath;
#endif
            }
        }
        public string PatientServerUrl { get { LoadConnection(); ValidateConnection(connection); return connection.serverUrl.TrimEnd('/'); } }
        public async Task<string> PatientAccessTokenAsync(CancellationToken cancellation)
        {
            LoadConnection();
            ValidateConnection(connection);
            return await EnsureAccessTokenAsync(cancellation);
        }

        QuestAtlasController owner;
        QuestAtlasVoiceContext context;
        QuestAtlasVoiceActions actions;
        AtlasVoiceConnection connection;
        readonly AtlasVoiceDetector detector = new AtlasVoiceDetector();
        readonly AtlasVoiceDetector interruptionDetector = new AtlasVoiceDetector();
        readonly float[] frame = new float[320];
        readonly string session = Guid.NewGuid().ToString("N");
        AudioSource speaker;
        AudioClip microphone, replyClip;
        bool ownsReplyClip, interruptRequestInFlight;
        float nextInterruptAt;
        int microphonePosition, generation;
        readonly AtlasReplyWindow replyWindow = new AtlasReplyWindow();
        int countdown = -1;
        float lastFrameAt, suppressCaptureUntil;
        JObject capturedContext;
        CancellationTokenSource lifetime;
        UnityWebRequest activeRequest;
        bool cloudAvailable, assistantReady, probingCloud;
        float nextCloudProbeAt;
        string accessToken = "";
        string authenticatedAppId = "";
        long accessTokenExpiresAt;
        Task<string> tokenRefresh;
        ClientWebSocket realtimeSocket;
        CancellationTokenSource realtimeCancellation;
        readonly SemaphoreSlim realtimeSendLock = new SemaphoreSlim(1, 1);
        bool realtimeConnected, realtimeAudio;
        bool realtimeServerReady;
        int diagnosticFrames, diagnosticPartials;
        float diagnosticPeak, nextVoiceDiagnosticAt;
        RealtimePcmBuffer realtimeBuffer;
        MemoryStream realtimeAnswerPcm;
        // Push-to-talk: audio leaves the headset only while A or X is held.
        const int PushToTalkMaxSamples = 16000 * 15, PushToTalkPreRoll = 3200;
        readonly System.Collections.Generic.List<float> pushToTalkSamples = new System.Collections.Generic.List<float>(PushToTalkMaxSamples);
        bool pushToTalkHeld, pushToTalkCapturing, pushToTalkLimitShown, awaitingPushToTalk;
        bool connectingRealtime, localCommandTurn;
        float pushToTalkReleasedAt, nextKeepAliveAt, nextRealtimeRetryAt;
        JObject pendingPushToTalk;

        sealed class RealtimePcmBuffer
        {
            readonly object sync = new object();
            readonly float[] ring = new float[24000 * 90];
            int read, write, count;
            public bool Ended { get; private set; }
            public int Buffered { get { lock(sync) return count; } }
            public void Append(byte[] pcm)
            {
                lock(sync)
                {
                    for(int i=0;i+1<pcm.Length;i+=2)
                    {
                        short sample=(short)(pcm[i]|(pcm[i+1]<<8));
                        if(count==ring.Length){read=(read+1)%ring.Length;count--;}
                        ring[write]=sample/32768f;write=(write+1)%ring.Length;count++;
                    }
                }
            }
            public void Finish(){lock(sync)Ended=true;}
            public void Read(float[] output)
            {
                lock(sync)
                {
                    int available=Mathf.Min(output.Length,count);
                    for(int i=0;i<available;i++){output[i]=ring[read];read=(read+1)%ring.Length;}
                    count-=available;
                    for(int i=available;i<output.Length;i++)output[i]=0f;
                }
            }
        }

        public void Initialize(QuestAtlasController atlas, QuestAtlasVoiceContext sceneContext)
        {
            owner = atlas; context = sceneContext;
            actions = gameObject.AddComponent<QuestAtlasVoiceActions>();
            actions.Initialize(atlas);
            lifetime = new CancellationTokenSource();
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false; speaker.spatialBlend = 0; speaker.volume = .8f;
            detector.Started += CaptureContext;
            detector.Completed += AudioReady;
            interruptionDetector.Completed += InterruptionReady;
            InitializeLiveKit();
            LoadConnection();
        }

        void LoadConnection()
        {
            if(AtlasWebPlatform.Active){connection=new AtlasVoiceConnection{serverUrl=AtlasWebPlatform.ApiBaseUrl,deviceToken=AtlasWebPlatform.AccessToken};Status="Servicio de voz web";return;}
            connection = new AtlasVoiceConnection();
            try
            {
                string path = ConnectionPath;
                if (File.Exists(path))
                    connection = JsonUtility.FromJson<AtlasVoiceConnection>(File.ReadAllText(path));
                else if (!string.Equals(path, LegacyConnectionPath, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(LegacyConnectionPath))
                {
                    connection = JsonUtility.FromJson<AtlasVoiceConnection>(File.ReadAllText(LegacyConnectionPath));
                    if (connection != null)
                    {
                        File.WriteAllText(path, JsonUtility.ToJson(connection, true));
                        try { File.Delete(LegacyConnectionPath); }
                        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                        { Debug.LogWarning("[Atlas Voice] No se pudo retirar la vinculación antigua: " + error.GetType().Name); }
                    }
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                // A configuration copied by ADB can be owned by the shell user on Quest.
                // Voice may need pairing, but the atlas and psychology must still open.
                Debug.LogWarning("[Atlas Voice] No se pudo leer la vinculación local: " + error.GetType().Name);
            }
            if(connection == null) connection = new AtlasVoiceConnection();
            if(string.IsNullOrWhiteSpace(connection.serverUrl))
                connection.serverUrl = DefaultServerUrl;
            bool expiredTunnel = IsExpiredTemporaryConnection(connection.serverUrl);
            bool retiredRailway = IsRetiredRailwayConnection(connection.serverUrl);
            if(expiredTunnel || retiredRailway)
            {
                // The persisted address wins over defaults unless a known retired endpoint is migrated.
                connection.serverUrl = DefaultServerUrl;
                if(expiredTunnel) connection.deviceToken = "";
                try { File.WriteAllText(ConnectionPath, JsonUtility.ToJson(connection, true)); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                { Debug.LogWarning("[Atlas Voice] No se pudo actualizar la vinculación local: " + error.GetType().Name); }
                Debug.LogWarning("[Atlas Voice] Dirección anterior de Atlas reemplazada por el dominio actual.");
            }
            Status = string.IsNullOrWhiteSpace(connection.appId) && string.IsNullOrWhiteSpace(connection.deviceToken)
                ? "Vincula estas gafas con Atlas en Railway" : "Micrófono apagado";
        }

        static bool IsExpiredTemporaryConnection(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (uri.Host.EndsWith(".trycloudflare.com", StringComparison.OrdinalIgnoreCase) ||
                 uri.Host.EndsWith(".loca.lt", StringComparison.OrdinalIgnoreCase));
        }

        static bool IsRetiredRailwayConnection(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                string.Equals(uri.Host, "game-auth-backend-production.up.railway.app", StringComparison.OrdinalIgnoreCase);
        }

        public void ToggleListening()
        {
            if(patientMode)return;
            if(IsWelcoming) SkipWelcome(false);
            if(Listening || Busy) { StopListening(); return; }
            wantsListening=true; _ = EnableAsync();
        }

async Task EnableAsync(bool retry=false)
        {
            if(patientMode)return;
            if(enabling||Listening)return;
            wantsListening=true;
            int ticket = ++generation;
            HasError = false; Busy = true; enabling = true; SetStatus("Preparando asistente…");
            try {
                string cloudError = "";
                bool liveKitAvailable = false;
                cloudAvailable = false;
                try
                {
                    LoadConnection();
                    ValidateConnection(connection);
                    var health = await RequestAsync("/health", null, lifetime.Token);
                    if(ticket != generation) return;
                    liveKitAvailable = health["livekit"]?.Value<bool?>("enabled") == true;
                    assistantReady = health.Value<bool?>("ready") == true;
                    cloudAvailable = assistantReady || health.Value<bool?>("realtime") == true;
                    if(!assistantReady) cloudError = health.Value<string>("message") ?? "Servicio de IA no disponible.";
                }
                catch(Exception ex)
                {
                    cloudError = ex.Message;
                    Debug.LogWarning("[Atlas Voice] Cloud unavailable; local repository enabled: " + ex.Message);
                }
                // Aborted health checks must not start Atlas's mic after entering the interview.
                if(ticket != generation || patientMode)return;
#if UNITY_ANDROID && !UNITY_EDITOR
                if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone)) {
                    requestingPermission = true;
                    UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                    float until = Time.realtimeSinceStartup + 20f;
                    while(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone)
                        && Time.realtimeSinceStartup < until) {
                        await AtlasWebClock.Delay(100, lifetime.Token);
                        if(ticket != generation) return;
                    }
                    if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
                        throw new InvalidOperationException("Permite el acceso al micrófono en los ajustes de la aplicación.");
                }
#endif
                if (liveKitAvailable && await OpenLiveKitAsync(lifetime.Token))
                {
                    if (ticket != generation || patientMode) { liveKit.Close(); return; }
                    Listening = true; cloudAvailable = true; assistantReady = true;
                    SetStatus(IdleHint + " · LiveKit");
                    if (pushToTalkHeld) { PushToTalkActive = true; liveKit.SetPushToTalk(true); }
                    return;
                }
                if(AtlasWebPlatform.Active)throw new InvalidOperationException("LiveKit web no está disponible. Comprueba el servicio de voz.");
                if(Microphone.devices.Length == 0) throw new InvalidOperationException("No se detecta un micrófono.");
                microphone = Microphone.Start(null, true, 10, 16000);
                float deadline = Time.realtimeSinceStartup + 5;
                while(Microphone.GetPosition(null) <= 0 && Time.realtimeSinceStartup < deadline) {
                    await Task.Yield(); lifetime.Token.ThrowIfCancellationRequested();
                    if(ticket != generation) return;
                }
                if(Microphone.GetPosition(null) <= 0 || !microphone || microphone.channels != 1 || microphone.frequency != 16000)
                    throw new InvalidOperationException("No se pudo iniciar el micrófono. Vuelve a intentarlo.");
                microphonePosition = Microphone.GetPosition(null);
                detector.Reset(); Listening = true; lastFrameAt = Time.unscaledTime;
                nextCloudProbeAt = Time.unscaledTime + 8f;
                if(cloudAvailable)
                {
                    try { await ConnectRealtimeAsync(ticket); }
                    catch(Exception ex) { realtimeConnected=false; Debug.LogWarning("[Atlas Realtime] WebSocket fallback HTTP: "+ex.Message); }
                }
                if(ticket != generation || patientMode)return;
                if(!assistantReady && !realtimeConnected) cloudAvailable=false;
                Debug.Log("[Atlas Voice] Microphone active; cloud=" + cloudAvailable + "; ready=" + assistantReady + "; realtime="+realtimeConnected+"; rate=" + microphone.frequency);
                SetStatus(cloudAvailable ? IdleHint :
                    (!string.IsNullOrEmpty(cloudError) ? cloudError : "Voz sin conexión · revisa Railway"));
                if(!cloudAvailable && !string.IsNullOrEmpty(cloudError))
                    Debug.Log("[Atlas Voice] Local mode reason: " + cloudError);
            } catch(OperationCanceledException) { }
              catch(Exception ex) {
                if(ticket == generation) {
                    EndMicrophone(); Listening=false; HasError=true;
                    Debug.LogWarning("[Atlas Voice] Listening could not start: "+ex.Message);
                    SetStatus(ex.Message);
                    if(wantsListening&&!retry)_=ReconnectAsync();
                }
            } finally { requestingPermission = false; if(ticket == generation) { enabling = false; Busy = false; Changed?.Invoke(); } }
        }

        async Task ReconnectAsync()
        {
            if(Reconnecting)return;Reconnecting=true;Changed?.Invoke();
            int[] waits={2,4,8,15,30};int attempt=0;
            try
            {
                while(wantsListening&&!Listening)
                {
                    int seconds=waits[Mathf.Min(attempt++,waits.Length-1)];
                    SetStatus("Sin conexión · reintento en "+seconds+" s");
                    await AtlasWebClock.Delay(seconds*1000,lifetime.Token);
                    if(!wantsListening)return;
                    await EnableAsync(true);
                }
            }
            catch(OperationCanceledException){}
            finally{Reconnecting=false;Changed?.Invoke();}
        }

async Task ProbeCloudAsync()
        {
            if(probingCloud || cloudAvailable || !Listening) return;
            probingCloud = true;
            try
            {
                LoadConnection();
                ValidateConnection(connection);
                var health = await RequestAsync("/health", null, lifetime.Token);
                assistantReady = health.Value<bool?>("ready") == true;
                cloudAvailable = assistantReady || health.Value<bool?>("realtime") == true;
                if(cloudAvailable && !realtimeConnected)
                {
                    try { await ConnectRealtimeAsync(generation); }
                    catch(Exception ex) { realtimeConnected=false; Debug.LogWarning("[Atlas Realtime] Reconexión: "+ex.Message); }
                }
                if(!assistantReady && !realtimeConnected) cloudAvailable=false;
                if(cloudAvailable) SetStatus("Conexión recuperada · " + IdleHint);
            }
            catch(Exception) { cloudAvailable = false; }
            finally
            {
                probingCloud = false;
                nextCloudProbeAt = Time.unscaledTime + 12f;
                Changed?.Invoke();
            }
        }


        public static void ValidateConnection(AtlasVoiceConnection value)
        {
            if(AtlasWebPlatform.Active)return;
            if(value == null || !Uri.TryCreate(value.serverUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != "https" && uri.Scheme != "http"))
                throw new InvalidOperationException("Falta configurar el servicio de voz de Atlas.");
            #if !UNITY_EDITOR
            if(uri.Scheme != "https") throw new InvalidOperationException("Las gafas necesitan una conexión HTTPS para el asistente.");
#endif
            if(uri.Scheme == "http" && !(value.allowLocalHttp && IsPrivateHost(uri.Host)))
                throw new InvalidOperationException("El asistente necesita una conexión HTTPS.");
            if(!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new InvalidOperationException("La dirección del asistente no es válida.");
            bool hasAppId=!string.IsNullOrWhiteSpace(value.appId);
            bool hasAppSecret=!string.IsNullOrWhiteSpace(value.appSecret);
            if(hasAppId != hasAppSecret)
                throw new InvalidOperationException("Completa el ID y el secreto de la aplicación.");
            if(!hasAppId && (string.IsNullOrWhiteSpace(value.deviceToken) || value.deviceToken.Length < 32))
                throw new InvalidOperationException("Vincula estas gafas con Atlas en Railway.");
        }

        static bool IsPrivateHost(string host)
        {
            if(host == "localhost") return true;
            if(!System.Net.IPAddress.TryParse(host, out var address)) return false;
            if(System.Net.IPAddress.IsLoopback(address)) return true;
            var bytes = address.GetAddressBytes();
            return bytes.Length == 4 && (bytes[0] == 10 || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31));
        }

        public void AskNow()
        {
            if (liveKit && liveKit.Connected) { liveKit.Send(new JObject { ["type"] = "ask" }); SetStatus("Te escucho."); return; }
            if(!Listening) { SetStatus("Primero activa la voz."); return; }
            if(!cloudAvailable) { SetStatus("Voz sin conexión · revisa la vinculación de Atlas"); return; }
            if(Busy) return;
            speaker.Stop(); ReleaseReply();
            detector.Reset(); replyWindow.Open(Time.unscaledTime, 25f); countdown = -1;
            microphonePosition = Microphone.GetPosition(null);
            SetStatus("Te escucho. Haz tu pregunta.");
        }

        // Called every frame by the controller input. Recording starts on the next Update while
        // the microphone is ready, so a press during the welcome or the first start is not lost.
        public void SetPushToTalk(bool held)
        {
            if(patientMode)return;
            if(held == pushToTalkHeld) return;
            pushToTalkHeld = held;
            if (liveKit && liveKit.Connected)
            {
                PushToTalkActive = held; if (held) CaptureContext(); liveKit.SetPushToTalk(held);
                SetStatus(held ? "Te escucho · suelta A o X al terminar" : "Atlas está pensando…"); return;
            }
            if(!held) return;
            if(IsWelcoming) SkipWelcome();
            else if(!Listening && !enabling) { wantsListening = true; _ = EnableAsync(); }
            if(!Listening) SetStatus("Preparando el micrófono · mantén pulsado");
        }

        void BeginPushToTalk()
        {
            PushToTalkActive = true; awaitingPushToTalk = false; pushToTalkLimitShown = false;
            bool wasSpeaking = speaker.isPlaying;
            if(wasSpeaking) { speaker.Stop(); ReleaseReply(); }
            realtimeAudio = false; realtimeBuffer = null; realtimeAnswerPcm?.Dispose(); realtimeAnswerPcm = null;
            replyWindow.Cancel(); countdown = -1; detector.Reset(); interruptionDetector.Reset();
            pushToTalkSamples.Clear();
            capturedContext = context.Capture(); UpdateContextLabel(capturedContext);
            if(!cloudAvailable) { pushToTalkCapturing = false; SetStatus("Sin conexión de voz · vincula estas gafas con Railway"); return; }
            pushToTalkCapturing = true; localCommandTurn = false; pendingPushToTalk = null;
            int position = Microphone.GetPosition(null);
            int preRoll = wasSpeaking ? 0 : PushToTalkPreRoll;
            microphonePosition = (position - preRoll + microphone.samples) % microphone.samples;
            // The clip is sent on release; reconnect now so the socket is ready by then.
            if(!realtimeConnected) _ = EnsureRealtimeAsync();
            Transcript = "";
            Debug.Log("[Atlas PTT] Start; realtime=" + realtimeConnected + "; ready=" + assistantReady);
            SetStatus("Te escucho · suelta A o X para enviar");
        }

        void CapturePushToTalk(int position)
        {
            int available = (position - microphonePosition + microphone.samples) % microphone.samples;
            int count = 0;
            while(available >= frame.Length && count++ < 50)
            {
                microphone.GetData(frame, microphonePosition);
                microphonePosition = (microphonePosition + frame.Length) % microphone.samples; available -= frame.Length;
                RecordInputFrame(frame);
                if(pushToTalkSamples.Count >= PushToTalkMaxSamples) continue;
                pushToTalkSamples.AddRange(frame);
            }
            if(pushToTalkSamples.Count >= PushToTalkMaxSamples && !pushToTalkLimitShown)
            { pushToTalkLimitShown = true; SetStatus("Máximo 15 s · suelta A o X para enviar"); }
        }

        void EndPushToTalk()
        {
            PushToTalkActive = false;
            if(!pushToTalkCapturing) return;
            pushToTalkCapturing = false;
            CapturePushToTalk(Microphone.GetPosition(null));
            float[] clip = pushToTalkSamples.ToArray();
            pushToTalkSamples.Clear();
            float[] speech = AtlasVoiceDetector.CompactPauses(clip);
            Debug.Log("[Atlas PTT] Release; seconds=" + (clip.Length / 16000f).ToString("F2")
                + "; speech=" + (speech.Length / 16000f).ToString("F2") + "; peak=" + AtlasVoiceDetector.Peak(clip).ToString("F3")
                + "; realtime=" + realtimeConnected);
            if(speech.Length < 16000 * 3 / 10) { SetStatus("No te escuché · mantén A o X mientras hablas"); return; }
            var payload = new JObject {
                ["session_id"] = session,
                ["pcm"] = Convert.ToBase64String(AtlasVoiceDetector.Pcm16(clip)),
                ["sample_rate"] = 16000, ["manual"] = true, ["action_protocol"] = 2,
                ["local_repository"] = true, ["context"] = capturedContext ?? context.Capture()
            };
            SetStatus("Atlas está pensando…");
            _ = SendPushToTalkAsync(speech, payload);
        }

        // Deepgram (realtime) is the production speech path. The clip goes in one burst with
        // its pauses shortened, so the server's 250 ms endpointing cannot split the order.
        // HTTP is the fallback when the socket is missing or no transcript arrives.
        async Task SendPushToTalkAsync(float[] speech, JObject payload)
        {
            int ticket = generation;
            try
            {
                float deadline = Time.realtimeSinceStartup + 2.5f;
                while(!realtimeConnected && connectingRealtime && ticket == generation && Time.realtimeSinceStartup < deadline)
                    await AtlasWebClock.Delay(50, lifetime.Token);
                if(ticket != generation || !Listening) return;
                if(realtimeConnected)
                {
                    await SendRealtimeJsonAsync(new JObject{
                        ["type"]="context",["session_id"]=session,["context"]=payload["context"],
                        ["manual"]=true,["action_protocol"]=2,["local_repository"]=true});
                    byte[] pcm = AtlasVoiceDetector.Pcm16(speech);
                    for(int offset = 0; offset < pcm.Length; offset += 32000)
                    {
                        int length = Mathf.Min(32000, pcm.Length - offset);
                        var chunk = new byte[length]; Buffer.BlockCopy(pcm, offset, chunk, 0, length);
                        await SendRealtimeBinaryAsync(chunk);
                    }
                    // One second of silence closes the utterance right away.
                    await SendRealtimeBinaryAsync(new byte[32000]);
                    await SendRealtimeJsonAsync(new JObject{["type"]="stop_audio"});
                    if(realtimeConnected)
                    {
                        pendingPushToTalk = payload; awaitingPushToTalk = true; pushToTalkReleasedAt = Time.unscaledTime;
                        Debug.Log("[Atlas PTT] Sent over realtime; bytes=" + pcm.Length);
                        return;
                    }
                }
                Debug.Log("[Atlas PTT] Sending over HTTP");
                await SubmitWhenIdleAsync(payload);
            }
            catch(OperationCanceledException) { }
            catch(Exception ex)
            {
                Debug.LogWarning("[Atlas PTT] Realtime unavailable: " + ex.Message);
                if(ticket == generation && Listening)
                {
                    if(assistantReady) await SubmitWhenIdleAsync(payload);
                    else SetStatus("No se pudo enviar la voz · comprueba la red y la vinculación");
                }
            }
        }

        void FallBackToHttp(string reason)
        {
            awaitingPushToTalk = false;
            var payload = pendingPushToTalk; pendingPushToTalk = null;
            if(payload == null || !assistantReady)
            { Debug.Log("[Atlas PTT] No answer: " + reason); SetStatus("No te escuché bien · mantén A o X mientras hablas"); return; }
            Debug.Log("[Atlas PTT] HTTP fallback: " + reason);
            _ = SubmitWhenIdleAsync(payload);
        }

        async Task EnsureRealtimeAsync()
        {
            if(connectingRealtime || realtimeConnected || !cloudAvailable || !Listening) return;
            connectingRealtime = true;
            try { await ConnectRealtimeAsync(generation); Debug.Log("[Atlas Realtime] Reconnected"); }
            catch(Exception ex) { realtimeConnected = false; Debug.LogWarning("[Atlas Realtime] Reconnect failed: " + ex.Message); }
            finally { connectingRealtime = false; nextRealtimeRetryAt = Time.unscaledTime + 10f; }
        }

        async Task SubmitWhenIdleAsync(JObject payload)
        {
            int ticket = generation;
            try
            {
                float deadline = Time.realtimeSinceStartup + 5f;
                while(Busy && ticket == generation && Time.realtimeSinceStartup < deadline) await AtlasWebClock.Delay(50, lifetime.Token);
                if(ticket != generation || !Listening) return;
                if(Busy) { SetStatus("Atlas sigue respondiendo · vuelve a intentarlo"); return; }
                await SubmitAsync(payload);
            }
            catch(OperationCanceledException) { }
        }

        void Update()
        {
            if (liveKit && liveKit.Connected) return;
            if(realtimeAudio && realtimeBuffer != null && realtimeBuffer.Ended && realtimeBuffer.Buffered == 0 && speaker.isPlaying)
            {
                speaker.Stop(); realtimeAudio=false; suppressCaptureUntil=Time.unscaledTime+.45f;
                if(realtimeAnswerPcm!=null){lastAnswerPcm=realtimeAnswerPcm.ToArray();realtimeAnswerPcm.Dispose();realtimeAnswerPcm=null;}
                SetStatus(Listening?IdleHint:"Micrófono apagado");
            }
            if(awaitingPushToTalk && !Busy && Time.unscaledTime > pushToTalkReleasedAt + 5f) FallBackToHttp("sin transcripción en tiempo real");
            if(!Listening || !microphone) return;
            // Without background audio Deepgram closes an idle socket after ~10 s:
            // send 20 ms of digital silence every 4 s (never microphone audio).
            if(realtimeConnected && !PushToTalkActive && Time.unscaledTime >= nextKeepAliveAt)
            { nextKeepAliveAt = Time.unscaledTime + 4f; _ = SendRealtimeBinaryAsync(new byte[640]); }
            if(cloudAvailable && !realtimeConnected && !connectingRealtime && Time.unscaledTime >= nextRealtimeRetryAt)
                _ = EnsureRealtimeAsync();
            if(pushToTalkHeld && !PushToTalkActive) BeginPushToTalk();
            else if(!pushToTalkHeld && PushToTalkActive) EndPushToTalk();
            if(Time.unscaledTime >= nextVoiceDiagnosticAt)
            {
                Debug.Log("[Atlas Voice Diagnostic] frames="+diagnosticFrames+
                    "; peak="+diagnosticPeak.ToString("F4")+
                    "; partials="+diagnosticPartials+
                    "; ws="+realtimeConnected+
                    "; serverReady="+realtimeServerReady+
                    "; busy="+Busy+
                    "; focused="+Application.isFocused);
                diagnosticFrames=0;diagnosticPartials=0;diagnosticPeak=0;
                nextVoiceDiagnosticAt=Time.unscaledTime+8f;
            }
            if(!cloudAvailable && !probingCloud && Time.unscaledTime >= nextCloudProbeAt)
                _ = ProbeCloudAsync();
            int position = Microphone.GetPosition(null);
            if(position < 0) { StopListening(); HasError = true; SetStatus("Se perdió el micrófono. Activa la voz otra vez."); return; }
            if(PushToTalkActive)
            {
                if(pushToTalkCapturing) CapturePushToTalk(position); else microphonePosition=position;
                lastFrameAt=Time.unscaledTime; return;
            }
            // Without STT, amplitude detection cannot distinguish speech from noise.
            // Keep the microphone alive for recovery, but never infer a question from sound alone.
            if(!cloudAvailable)
            {
                microphonePosition=position; detector.Reset(); lastFrameAt=Time.unscaledTime;
                return;
            }
            // Outside push-to-talk the microphone is ignored, except for a «Preguntar ahora» window.
            if(speaker.isPlaying || (!replyWindow.IsOpen(Time.unscaledTime) && !detector.Recording))
            {
                if(replyWindow.Deadline>0f && !replyWindow.IsOpen(Time.unscaledTime) && !speaker.isPlaying)
                {replyWindow.Cancel();countdown=-1;SetStatus(IdleHint);}
                microphonePosition=position; detector.Reset(); lastFrameAt=Time.unscaledTime; return;
            }
            int available = (position - microphonePosition + microphone.samples) % microphone.samples;
            interruptionDetector.Reset();
            if(Busy || Time.unscaledTime < suppressCaptureUntil || Time.unscaledTime-lastFrameAt>2f)
            {microphonePosition=position;detector.Reset();lastFrameAt=Time.unscaledTime;return;}
            lastFrameAt=Time.unscaledTime;
            if(!detector.Recording&&replyWindow.Deadline>0f)
            {
                int seconds=Mathf.CeilToInt(replyWindow.Deadline-Time.unscaledTime);
                if(seconds<=0){replyWindow.Cancel();SetStatus(IdleHint);}
                else if(seconds!=countdown){countdown=seconds;SetStatus("Te escucho · habla ahora · "+seconds+" s");}
            }
            int count=0;
            while(available>=frame.Length&&count++<50&&!Busy)
            {
                microphone.GetData(frame,microphonePosition);
                microphonePosition=(microphonePosition+frame.Length)%microphone.samples;available-=frame.Length;detector.Feed(frame);
                RecordInputFrame(frame);
                if(realtimeConnected) QueueRealtimeFrame(frame);
            }
        }

        void RecordInputFrame(float[] samples)
        {
            diagnosticFrames++;
            float peak=0f;
            for(int i=0;i<samples.Length;i++) peak=Mathf.Max(peak,Mathf.Abs(samples[i]));
            diagnosticPeak=Mathf.Max(diagnosticPeak,peak);
        }

        void InterruptionReady(float[] samples)
        {
            if(interruptRequestInFlight || !Speaking || Time.unscaledTime < nextInterruptAt) return;
            _=SubmitInterruptionAsync(samples);
        }
        async Task SubmitInterruptionAsync(float[] samples)
        {
            if(patientMode || !Listening)return;
            int ticket=generation;
            interruptRequestInFlight=true;
            try
            {
                var response=await RequestAsync("/interrupt",new JObject{
                    ["session_id"]=session,["pcm"]=Convert.ToBase64String(AtlasVoiceDetector.Pcm16(samples)),
                    ["sample_rate"]=16000,["context"]=context.Capture()},lifetime.Token);
                if(ticket!=generation || patientMode || !Listening)return;
                string state=response.Value<string>("state");
                if(state=="interrupt_stop"){Transcript=response.Value<string>("transcript")??"Atlas, espera";StopSpeaking();return;}
                if(state!="answer")return;
                owner.spokenGuide.Cancel();
                speaker.Stop();ReleaseReply();Transcript=response.Value<string>("transcript")??"";Answer=response.Value<string>("answer")??"";
                var referenced=response["part_ids"] as JArray;if(referenced!=null)owner.Learning.ShowNarration(referenced.ToObject<string[]>(),Answer,12f);
                string audio=response.Value<string>("audio_pcm");if(string.IsNullOrEmpty(audio)){Changed?.Invoke();_=SpeakTextAsync(Answer,false);return;}
                byte[] bytes=Convert.FromBase64String(audio);lastAnswerPcm=(byte[])bytes.Clone();
                float[] decoded=AtlasVoiceDetector.DecodePcm16(bytes);replyClip=AudioClip.Create("Interrupción de Atlas",decoded.Length,1,24000,false);
                ownsReplyClip=true;replyClip.SetData(decoded,0);speaker.clip=replyClip;speaker.Play();nextInterruptAt=Time.unscaledTime+1.1f;SetStatus("Atlas ajustó la explicación");
            }
            catch(OperationCanceledException){}
            catch(Exception ex){Debug.LogWarning("[Atlas Voice] Interrupción no disponible: "+ex.Message);}
            finally{if(ticket==generation){nextInterruptAt=Time.unscaledTime+1.5f;interruptRequestInFlight=false;interruptionDetector.Reset();}}
        }

        void CaptureContext()
        {
            replyWindow.Capture(Time.unscaledTime);
            capturedContext = context.Capture();
            UpdateContextLabel(capturedContext);
            if(realtimeConnected) _=SendRealtimeJsonAsync(new JObject{
                ["type"]="context",["session_id"]=session,["context"]=capturedContext,
                ["manual"]=replyWindow.ReplyStarted,["action_protocol"]=2,["local_repository"]=true});
        }

        static bool HasTarget(JObject value) => value != null && (((JArray)value["held"]).Count > 0 || ((JArray)value["pointed"]).Count > 0);

        void LateUpdate()
        {
            if(!Listening || (Busy && !(liveKit && liveKit.UserSpeaking))) return;
            if((detector.Recording || (liveKit && liveKit.UserSpeaking)) && !HasTarget(capturedContext) && context.HasCurrentTarget())
            {
                var candidate = context.Capture();
                if(HasTarget(candidate)) { capturedContext = candidate; UpdateContextLabel(candidate); }
            }
        }

        void UpdateContextLabel(JObject snapshot)
        {
            var held = (JArray)snapshot["held"];
            var pointed = (JArray)snapshot["pointed"];
            var targets = held.Count > 0 ? held : pointed;
            ContextLabel = held.Count>0 && pointed.Count>0
                ? "En mano: " + held[0].Value<string>("name") + " · Rayo: " + pointed[0].Value<string>("name")
                : targets.Count == 0 ? "Sin pieza detectada · puedes preguntar en general"
                : targets.Count == 1 ? (held.Count>0?"En mano: ":"Rayo: ")+targets[0].Value<string>("name") : "Varias piezas: puedo pedirte una aclaración";
            Changed?.Invoke();
        }

void AudioReady(float[] samples)
        {
            bool manual = replyWindow.Consume(); countdown = -1;
            if(!HasTarget(capturedContext))
            {
                var finalContext = context.Capture();
                if(HasTarget(finalContext)) capturedContext = finalContext;
            }
            var snapshot = capturedContext ?? context.Capture();
            UpdateContextLabel(snapshot);
            var payload = new JObject {
                ["session_id"] = session,
                ["pcm"] = Convert.ToBase64String(AtlasVoiceDetector.Pcm16(samples)),
                ["sample_rate"] = 16000, ["manual"] = manual, ["action_protocol"] = 2,
                ["local_repository"] = true, ["context"] = snapshot
            };
            if(realtimeConnected)
            {
                _=SendRealtimeJsonAsync(new JObject{["type"]="stop_audio"});
                return;
            }
            if(!cloudAvailable)
            {
                SetStatus("Voz sin conexión · revisa la vinculación de Atlas");
                return;
            }
            _ = SubmitAsync(payload);
        }

        // Server answers that did not become actions still get a deterministic system order.
        bool TryLocalCommand(string question)
        {
            if(!AtlasSystemCommands.TryParse(question, out var command)) return false;
            try { owner.FocusAnatomy(command.SystemIds, Array.Empty<string>(), command.Mode); }
            catch(Exception ex) { Debug.LogWarning("[Atlas PTT] Local command failed: " + ex.Message); return false; }
            Debug.Log("[Atlas PTT] Local command; mode=" + command.Mode + "; systems=" + string.Join(",", command.SystemIds));
            localCommandTurn = true;
            speaker.Stop(); ReleaseReply(); realtimeAudio = false; realtimeBuffer = null;
            owner.Environment.PlayConfirm(); LastActionAt = Time.unscaledTime;
            Transcript = question; Answer = command.Description;
            SetStatus("Listo · " + IdleHint);
            return true;
        }

bool TryShowLocalAnswer(JObject response, JObject snapshot, string question, bool offline)
        {
            AtlasPart part = null;
            var referenced = response == null ? null : response["part_ids"] as JArray;
            if(referenced != null && referenced.Count == 1)
                context.TryResolvePart(referenced[0].Value<string>(), out part);
            if(part == null) context.TryResolvePrimaryTarget(snapshot, out part);
            if(part == null) return false;

            string localAnswer;
            AtlasLocalQuestionIntent intent = QuestAtlasLocalKnowledge.DetectIntent(question);
            if(!QuestAtlasLocalKnowledge.TryAnswer(part, question, out localAnswer))
            {
                intent = AtlasLocalQuestionIntent.IdentificationAndFunction;
                localAnswer = QuestAtlasLocalKnowledge.BuildOfflineAnswer(part);
            }

            speaker.Stop();
            ReleaseReply();
            lastAnswerPcm = null;
            Transcript = string.IsNullOrWhiteSpace(question)
                ? "Consulta local sobre " + AtlasSpanish.Name(part.name)
                : question;
            Answer = localAnswer;
            owner.PlaySelectionSound();
            owner.Learning.ShowNarration(new [] { part.id }, Answer, 14f);
            suppressCaptureUntil = Time.unscaledTime + .35f;
            Changed?.Invoke();

            if(!offline)
            {
                SetStatus("Respuesta local inmediata · preparando voz");
                _ = SpeakTextAsync(localAnswer, true);
            }
            else
            {
                byte[] cached=QuestAtlasSpeechCache.TryRead(this,localAnswer);
                if(cached!=null) {
                    AudioClip offlineClip=QuestAtlasSpeechCache.Clip(cached,"Atlas · voz guardada");
                    _ = PlayCachedClipAsync(offlineClip);
                }
                else SetStatus("Respuesta local visible · esta voz aún no está guardada");
            }
            return true;
        }

async Task PlayCachedClipAsync(AudioClip clip)
        {
            try { await PlayOfflineClipAsync(clip,true); }
            finally { if(clip)Destroy(clip); }
        }

async Task SpeakTextAsync(string text, bool localResponse)
        {
            if (liveKit && liveKit.Connected) { await liveKit.SayAsync(text); return; }
            if(!Listening || string.IsNullOrWhiteSpace(text)) return;

            int ticket = generation;
            try
            {
                byte[] bytes = await QuestAtlasSpeechCache.GetAsync(this,text,lifetime.Token);
                if(ticket != generation || !Listening) return;

                owner.spokenGuide.Cancel();
                speaker.Stop();
                ReleaseReply();
                lastAnswerPcm = (byte[])bytes.Clone();
                float[] samples = AtlasVoiceDetector.DecodePcm16(bytes);
                replyClip = AudioClip.Create("Voz local de Atlas", samples.Length, 1, 24000, false);
                ownsReplyClip = true;
                replyClip.SetData(samples, 0);
                speaker.clip = replyClip;
                speaker.Play();
                nextInterruptAt = Time.unscaledTime + 1.1f;
                SetStatus(localResponse ? "Atlas responde · repositorio anatómico" : "Atlas responde");
                Changed?.Invoke();

                while(speaker.isPlaying && ticket == generation)
                    await AtlasWebClock.Delay(50, lifetime.Token);

                if(ticket == generation && Listening)
                {
                    suppressCaptureUntil = Time.unscaledTime + .45f;
                    SetStatus(IdleHint);
                }
            }
            catch(OperationCanceledException) { }
            catch(Exception ex)
            {
                Debug.LogWarning("[Atlas Voice] No se pudo reproducir la respuesta: " + ex.Message);
                if(ticket == generation && Listening)
                {
                    SetStatus("Respuesta visible · reintentando voz en la próxima consulta");
                    Changed?.Invoke();
                }
            }
        }



async Task SubmitAsync(JObject payload)
        {
            int ticket = generation;
            HasError = false; Busy = true; SetStatus("Atlas está pensando…");
            try {
                var response = await RequestAsync("/voice", payload, lifetime.Token);
                cloudAvailable = true;
                if(ticket != generation || !Listening) return;
                string state = response.Value<string>("state");
                if (state == "actions") {
                    Transcript = response.Value<string>("transcript") ?? "";
                    PerformingAction = true; Answer = "Realizando tu solicitud…"; SetStatus("Aplicando la orden en el atlas…");
                    var receipts = await actions.ExecuteAsync(response, lifetime.Token, () => ticket == generation && Listening);
                    if (ticket != generation || !Listening) return;
                    PerformingAction = false;
                    foreach (var receipt in receipts)
                        if (receipt.Value<string>("status") == "applied") LastActionAt = Time.unscaledTime;
                    string localAnswer = actions.Describe((JArray)response["actions"], receipts);
                    Answer = localAnswer; Changed?.Invoke();
                    try {
                        var confirmation = await RequestAsync("/actions/result", new JObject {
                            ["session_id"] = session, ["action_batch_id"] = response.Value<string>("action_batch_id"),
                            ["receipts"] = receipts
                        }, lifetime.Token);
                        if (ticket != generation || !Listening) return;
                        if (confirmation.Value<string>("state") != "answer") throw new InvalidOperationException();
                        response = confirmation;
                    } catch (OperationCanceledException) { throw; }
                      catch (Exception) {
                        if (ticket != generation || !Listening) return;
                        response = new JObject { ["state"] = "answer", ["transcript"] = Transcript, ["answer"] = localAnswer,
                            ["audio_warning"] = "Resultado en pantalla. Confirmación de voz no disponible." };
                    }
                    state = response.Value<string>("state");
                }
                if(state == "sleeping") { SetStatus(IdleHint); return; }
                if(state == "listening") {
                    owner.PlaySelectionSound();
                    suppressCaptureUntil = Time.unscaledTime + .25f;
                    replyWindow.Open(suppressCaptureUntil, 25f); countdown = -1;
                    SetStatus("Te escucho. Haz tu pregunta."); return;
                }
                if(state == "error") throw new InvalidOperationException(response.Value<string>("message") ?? "No se pudo responder.");
                string responseTranscript = response.Value<string>("transcript") ?? "";
                Debug.Log("[Atlas PTT] HTTP result; state=" + state + "; transcript=" + responseTranscript);
                if(TryLocalCommand(responseTranscript)) return;
                if(response.Value<bool?>("local_repository") == true &&
                    TryShowLocalAnswer(response, payload["context"] as JObject, responseTranscript, false))
                    return;

                Transcript = responseTranscript;
                Answer = response.Value<string>("answer") ?? "";
                var referenced=response["part_ids"] as JArray;
                if(referenced!=null)owner.Learning.ShowNarration(referenced.ToObject<string[]>(),Answer,12f);
                string audio = response.Value<string>("audio_pcm");
                if(!string.IsNullOrEmpty(audio)) {
                    byte[] bytes = Convert.FromBase64String(audio);
                    lastAnswerPcm=(byte[])bytes.Clone();
                    if(bytes.Length > 4000000) throw new InvalidOperationException("La respuesta de voz es demasiado larga.");
                    owner.spokenGuide.Cancel();
                    ReleaseReply();
                    float[] samples = AtlasVoiceDetector.DecodePcm16(bytes);
                    replyClip = AudioClip.Create("Respuesta de Atlas", samples.Length, 1, 24000, false);
                    ownsReplyClip=true;
                    replyClip.SetData(samples, 0); speaker.clip = replyClip; speaker.Play(); nextInterruptAt = Time.unscaledTime + 1.1f;
                    SetStatus("Atlas responde · voz generada por IA");
                    while(speaker.isPlaying && ticket == generation) await AtlasWebClock.Delay(50, lifetime.Token);
                    suppressCaptureUntil = Time.unscaledTime + .45f;
                }
                if(string.IsNullOrEmpty(audio) && !string.IsNullOrWhiteSpace(Answer))
                    await SpeakTextAsync(Answer, false);
                if(ticket == generation && Listening)
                {
                    // Follow-up questions no longer reopen the microphone: the user answers with push-to-talk.
                    if ((response.Value<int?>("followup_seconds") ?? 0) > 0)
                        SetStatus("Mantén A o X para responder");
                    else SetStatus(response.Value<string>("audio_warning") ?? IdleHint);
                    if (MuteAfterReply) { MuteAfterReply = false; StopListening(); }
                }
            } catch(OperationCanceledException) { }
              catch(Exception ex) { if(ticket == generation) {
                    cloudAvailable = false;
                    nextCloudProbeAt = Time.unscaledTime + 8f;
                    Debug.LogWarning("[Atlas Voice] Cloud request failed: " + ex.Message);
                    SetStatus("Voz sin conexión · revisa la vinculación de Atlas");
                    Changed?.Invoke();
                } }
            finally { if(ticket == generation) { Busy = false; PerformingAction = false; detector.Reset(); Changed?.Invoke(); } }
        }

        Uri RealtimeUri()
        {
            var source=new Uri(connection.serverUrl.TrimEnd('/')+"/realtime?session_id="+session);
            var builder=new UriBuilder(source){Scheme=source.Scheme=="https"?"wss":"ws"};
            return builder.Uri;
        }

        async Task ConnectRealtimeAsync(int ticket)
        {
            if(ticket!=generation || patientMode)return;
            await DisconnectRealtimeAsync();
            if(ticket!=generation || patientMode)return;
            realtimeServerReady=false;
            string bearer=await EnsureAccessTokenAsync(lifetime.Token);
            if(ticket!=generation || patientMode)return;
            realtimeCancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            realtimeSocket=new ClientWebSocket();
            realtimeSocket.Options.SetRequestHeader("Authorization","Bearer "+bearer);
            realtimeSocket.Options.KeepAliveInterval=TimeSpan.FromSeconds(15);
            await realtimeSocket.ConnectAsync(RealtimeUri(),realtimeCancellation.Token);
            if(ticket!=generation){await DisconnectRealtimeAsync();return;}
            realtimeConnected=true;
            await SendRealtimeJsonAsync(new JObject{["type"]="start",["session_id"]=session,
                ["action_protocol"]=2,["local_repository"]=true,["context"]=context.Capture()});
            _=ReceiveRealtimeAsync(ticket);
        }

        async Task DisconnectRealtimeAsync()
        {
            realtimeConnected=false;
            realtimeServerReady=false;
            var socket=realtimeSocket;realtimeSocket=null;
            var cancellation=realtimeCancellation;realtimeCancellation=null;
            try
            {
                cancellation?.Cancel();
                if(socket!=null&&socket.State==WebSocketState.Open)
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure,"fin",CancellationToken.None);
            }
            catch(Exception){}
            finally{socket?.Dispose();cancellation?.Dispose();}
        }

        void QueueRealtimeFrame(float[] samples)
        {
            byte[] pcm=AtlasVoiceDetector.Pcm16(samples);
            _=SendRealtimeBinaryAsync(pcm);
        }

        async Task SendRealtimeBinaryAsync(byte[] bytes)
        {
            if(!realtimeConnected||realtimeSocket==null||realtimeSocket.State!=WebSocketState.Open)return;
            await realtimeSendLock.WaitAsync();
            try
            {
                if(realtimeSocket!=null&&realtimeSocket.State==WebSocketState.Open)
                    await realtimeSocket.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Binary,true,realtimeCancellation.Token);
            }
            catch(Exception ex){RealtimeFailed(ex);}
            finally{realtimeSendLock.Release();}
        }

        async Task SendRealtimeJsonAsync(JObject value)
        {
            byte[] bytes=Encoding.UTF8.GetBytes(value.ToString(Newtonsoft.Json.Formatting.None));
            if(!realtimeConnected||realtimeSocket==null||realtimeSocket.State!=WebSocketState.Open)return;
            await realtimeSendLock.WaitAsync();
            try
            {
                if(realtimeSocket!=null&&realtimeSocket.State==WebSocketState.Open)
                    await realtimeSocket.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,realtimeCancellation.Token);
            }
            catch(Exception ex){RealtimeFailed(ex);}
            finally{realtimeSendLock.Release();}
        }

        void RealtimeFailed(Exception ex)
        {
            if(!realtimeConnected)return;
            realtimeConnected=false;
            realtimeServerReady=false;
            if(!assistantReady)
            {
                cloudAvailable=false;
                nextCloudProbeAt=Time.unscaledTime+8f;
            }
            Debug.LogWarning("[Atlas Realtime] Se usará HTTP: "+ex.Message);
        }

        async Task ReceiveRealtimeAsync(int ticket)
        {
            byte[] chunk=new byte[65536];
            try
            {
                while(ticket==generation&&realtimeSocket!=null&&realtimeSocket.State==WebSocketState.Open)
                {
                    using(var message=new MemoryStream())
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            result=await realtimeSocket.ReceiveAsync(new ArraySegment<byte>(chunk),realtimeCancellation.Token);
                            if(result.MessageType==WebSocketMessageType.Close){RealtimeFailed(new InvalidOperationException("Conexión cerrada"));return;}
                            message.Write(chunk,0,result.Count);
                            if(message.Length>4000000)throw new InvalidOperationException("Mensaje realtime demasiado grande.");
                        }while(!result.EndOfMessage);
                        if(ticket!=generation || patientMode)return;
                        byte[] data=message.ToArray();
                        if(result.MessageType==WebSocketMessageType.Binary) ReceiveRealtimeAudio(data);
                        else await HandleRealtimeMessageAsync(JObject.Parse(Encoding.UTF8.GetString(data)),ticket);
                    }
                }
            }
            catch(OperationCanceledException){}
            catch(Exception ex){if(ticket==generation)RealtimeFailed(ex);}
        }

        void ReceiveRealtimeAudio(byte[] pcm)
        {
            if(!realtimeAudio||realtimeAnswerPcm==null||PushToTalkActive)return;
            if(realtimeAnswerPcm.Length+pcm.Length>4000000)throw new InvalidOperationException("Audio demasiado largo.");
            realtimeAnswerPcm.Write(pcm,0,pcm.Length);
        }

        async Task HandleRealtimeMessageAsync(JObject message,int ticket)
        {
            if(ticket!=generation || patientMode)return;
            string type=message.Value<string>("type");
            if(type=="ready"){realtimeConnected=true;realtimeServerReady=true;Debug.Log("[Atlas Realtime] Deepgram ready.");return;}
            if(type=="transcript_partial")
            {
                diagnosticPartials++;
                Transcript=message.Value<string>("text")??"";SetStatus("Escuchando · "+Transcript);Changed?.Invoke();return;
            }
            if(type=="transcript_final")
            {
                Debug.Log("[Atlas Realtime] Final transcript: "+(message.Value<string>("text")??""));
                awaitingPushToTalk=false;pendingPushToTalk=null;
                Transcript=message.Value<string>("text")??"";Busy=true;SetStatus("Atlas está pensando…");Changed?.Invoke();return;
            }
            if(type=="speech_started"&&PushToTalkActive&&speaker.isPlaying&&Time.unscaledTime>=nextInterruptAt)
            {speaker.Stop();ReleaseReply();realtimeAudio=false;SetStatus("Te escucho…");return;}
            if(type=="state")
            {
                string state=message.Value<string>("state");
                if(pendingPushToTalk!=null){Busy=false;FallBackToHttp("estado "+state);return;}
                awaitingPushToTalk=false;if(state=="listening")SetStatus("Mantén A o X y haz tu pregunta");
                else SetStatus(IdleHint);
                Busy=false;return;
            }
            if(type=="audio_start")
            {
                if(PushToTalkActive||localCommandTurn)return;
                speaker.Stop();ReleaseReply();
                realtimeBuffer=new RealtimePcmBuffer();realtimeAnswerPcm?.Dispose();realtimeAnswerPcm=new MemoryStream();
                realtimeAudio=true;return;
            }
            if(type=="audio_end")
            {
                byte[] pcm=realtimeAnswerPcm?.ToArray();realtimeAnswerPcm?.Dispose();realtimeAnswerPcm=null;
                realtimeAudio=false;realtimeBuffer=null;Busy=false;
                if(pcm!=null&&pcm.Length>0&&message.Value<bool?>("interrupted")!=true)PlayCompletePcm(pcm,"Voz de Atlas");
                return;
            }
            if(type=="audio_error")
            {realtimeBuffer?.Finish();Busy=false;SetStatus(message.Value<string>("message")??"Respuesta visible");return;}
            if(type=="error")
            {if(pendingPushToTalk!=null){Busy=false;FallBackToHttp("error realtime");return;}
             Busy=false;HasError=true;awaitingPushToTalk=false;Debug.LogWarning("[Atlas Realtime] Server error: "+message.Value<string>("message"));SetStatus(message.Value<string>("message")??"Fallo de Atlas realtime");return;}
            if(type=="result") await HandleRealtimeResultAsync((JObject)message["payload"],ticket);
        }

        async Task HandleRealtimeResultAsync(JObject response,int ticket)
        {
            awaitingPushToTalk=false;
            string state=response.Value<string>("state");
            if(state=="actions")
            {
                Transcript=response.Value<string>("transcript")??Transcript;PerformingAction=true;
                Answer="Realizando tu solicitud…";SetStatus("Aplicando la orden en el atlas…");
                var receipts=await actions.ExecuteAsync(response,lifetime.Token,()=>ticket==generation&&Listening);
                if(ticket!=generation||!Listening)return;
                PerformingAction=false;foreach(var receipt in receipts)if(receipt.Value<string>("status")=="applied")LastActionAt=Time.unscaledTime;
                string localAnswer=actions.Describe((JArray)response["actions"],receipts);Answer=localAnswer;Changed?.Invoke();
                try
                {
                    var confirmation=await RequestAsync("/actions/result",new JObject{["session_id"]=session,
                        ["action_batch_id"]=response.Value<string>("action_batch_id"),["receipts"]=receipts},lifetime.Token);
                    if(ticket!=generation||!Listening)return;
                    Answer=confirmation.Value<string>("answer")??localAnswer;
                    string audio=confirmation.Value<string>("audio_pcm");
                    if(!string.IsNullOrEmpty(audio))PlayCompletePcm(Convert.FromBase64String(audio),"Confirmación de Atlas");
                    else _=SpeakTextAsync(Answer,false);
                }
                catch(Exception){if(ticket!=generation || patientMode || !Listening)return;_=SpeakTextAsync(localAnswer,false);}
                Busy=false;Changed?.Invoke();return;
            }
            string question=response.Value<string>("transcript")??Transcript;
            Debug.Log("[Atlas PTT] Realtime result; state="+state+"; route="+response.Value<string>("route"));
            if(TryLocalCommand(question)){Busy=false;return;}
            if(response.Value<bool?>("local_repository")==true&&TryShowLocalAnswer(response,capturedContext,question,false))
            {Busy=false;return;}
            Transcript=question;Answer=response.Value<string>("answer")??"";
            var referenced=response["part_ids"] as JArray;
            if(referenced!=null)owner.Learning.ShowNarration(referenced.ToObject<string[]>(),Answer,12f);
            SetStatus("Atlas prepara la voz…");Changed?.Invoke();
        }

        void PlayCompletePcm(byte[] bytes,string name)
        {
            if(patientMode || !Listening)return;
            owner.spokenGuide.Cancel();
            speaker.Stop();ReleaseReply();lastAnswerPcm=(byte[])bytes.Clone();
            replyClip=QuestDialogueAudio.Play(speaker,bytes,name);ownsReplyClip=true;
            nextInterruptAt=Time.unscaledTime+1.1f;SetStatus("Atlas responde");
        }

        Uri AuthenticationUri()
        {
            var atlas=new Uri(connection.serverUrl.TrimEnd('/'));
            string path=atlas.AbsolutePath.TrimEnd('/');
            if(!path.EndsWith("/atlas",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La dirección remota de Atlas debe terminar en /api/v1/atlas.");
            path=path.Substring(0,path.Length-"/atlas".Length)+"/auth/login";
            return new UriBuilder(atlas){Path=path,Query="",Fragment=""}.Uri;
        }

        async Task<string> EnsureAccessTokenAsync(CancellationToken cancellation)
        {
            if(string.IsNullOrWhiteSpace(connection.appId))return connection.deviceToken;
            if(!string.Equals(authenticatedAppId,connection.appId,StringComparison.Ordinal))
            {accessToken="";accessTokenExpiresAt=0;authenticatedAppId=connection.appId;}
            long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if(!string.IsNullOrEmpty(accessToken)&&accessTokenExpiresAt>now+60)return accessToken;
            if(tokenRefresh==null)tokenRefresh=LoginAsync(cancellation);
            try{return await tokenRefresh;}
            finally{if(tokenRefresh!=null&&tokenRefresh.IsCompleted)tokenRefresh=null;}
        }

        async Task<string> LoginAsync(CancellationToken cancellation)
        {
            var payload=new JObject{{"app_id",connection.appId.Trim()},{"app_secret",connection.appSecret}};
            using(var request=new UnityWebRequest(AuthenticationUri().AbsoluteUri,"POST"))
            {
                activeRequest=request;
                request.downloadHandler=new DownloadHandlerBuffer();
                request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload.ToString(Newtonsoft.Json.Formatting.None)));
                request.SetRequestHeader("Content-Type","application/json");
                request.timeout=12;request.redirectLimit=0;
                try
                {
                    var operation=request.SendWebRequest();
                    while(!operation.isDone){cancellation.ThrowIfCancellationRequested();await Task.Yield();}
                    if(request.result!=UnityWebRequest.Result.Success)
                        throw new InvalidOperationException(request.responseCode==401
                            ? "Las credenciales de Atlas no son válidas."
                            : "No se pudo iniciar sesión con Atlas.");
                    var response=JObject.Parse(request.downloadHandler.text);
                    accessToken=response.Value<string>("token")??"";
                    accessTokenExpiresAt=response.Value<long?>("expires_at")??0;
                    if(accessToken.Length<32||accessTokenExpiresAt<=DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                        throw new InvalidOperationException("El servidor devolvió una sesión de Atlas inválida.");
                    return accessToken;
                }
                finally{if(activeRequest==request)activeRequest=null;}
            }
        }

        async Task<JObject> RequestAsync(string path, JObject body, CancellationToken cancellation)
        {
            if(AtlasWebPlatform.Active)return await AtlasWebPlatform.RequestAsync(path,body,cancellation);
            for(int attempt=0;attempt<2;attempt++)
            {
                string bearer=await EnsureAccessTokenAsync(cancellation);
                string url=connection.serverUrl.TrimEnd('/')+path;
                using(var request=new UnityWebRequest(url,body==null?"GET":"POST"))
                {
                    activeRequest=request;
                    request.downloadHandler=new DownloadHandlerBuffer();
                    request.timeout=path=="/health"?8:path=="/speech"?20:32;
                    request.redirectLimit=0;
                    request.SetRequestHeader("Authorization","Bearer "+bearer);
                    if(body!=null)
                    {
                        request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None)));
                        request.SetRequestHeader("Content-Type","application/json");
                    }
                    try
                    {
                        var operation=request.SendWebRequest();
                        while(!operation.isDone){cancellation.ThrowIfCancellationRequested();await Task.Yield();}
                        if(request.downloadedBytes>6000000)throw new InvalidOperationException("Respuesta demasiado grande.");
                        if(request.responseCode==401&&!string.IsNullOrWhiteSpace(connection.appId)&&attempt==0)
                        {accessToken="";accessTokenExpiresAt=0;continue;}
                        if(request.result!=UnityWebRequest.Result.Success)
                            throw new InvalidOperationException(request.responseCode==401?"La sesión de Atlas no es válida."
                                :request.responseCode==429?"Atlas está ocupado. Espera un momento."
                                :"No se pudo conectar con Atlas. Comprueba el servicio y la red.");
                        return JObject.Parse(request.downloadHandler.text);
                    }
                    finally{if(activeRequest==request)activeRequest=null;}
                }
            }
            throw new InvalidOperationException("No se pudo renovar la sesión de Atlas.");
        }

        public void StopSpeaking()
        {
            if (liveKit && liveKit.Connected) { liveKit.Interrupt(); return; }
            if(!speaker||!speaker.isPlaying)return;speaker.Stop();ReleaseReply();suppressCaptureUntil=Time.unscaledTime+.35f;
            SetStatus(Listening?"Interrumpido · "+IdleHint:"Respuesta interrumpida");
        }
        public void RepeatAnswer()
        {
            if (liveKit && liveKit.Connected) { _ = liveKit.SayAsync(Answer); return; }
            if(patientMode)return;
            if(lastAnswerPcm==null||lastAnswerPcm.Length==0){SetStatus("Todavía no hay una respuesta para repetir.");return;}
            _=PlayStoredAnswerAsync();
        }
        async Task PlayStoredAnswerAsync()
        {
            if(patientMode)return;
            owner.spokenGuide.Cancel();
            int ticket=generation;speaker.Stop();ReleaseReply();float[] samples=AtlasVoiceDetector.DecodePcm16(lastAnswerPcm);
            replyClip=AudioClip.Create("Repetición de Atlas",samples.Length,1,24000,false);ownsReplyClip=true;replyClip.SetData(samples,0);
            speaker.clip=replyClip;speaker.Play();nextInterruptAt=Time.unscaledTime+1.1f;SetStatus("Repitiendo respuesta");
            while(speaker.isPlaying&&ticket==generation)await AtlasWebClock.Delay(50,lifetime.Token);
            if(ticket==generation){suppressCaptureUntil=Time.unscaledTime+.45f;SetStatus(Listening?IdleHint:"Micrófono apagado");}
        }

        void SetStatus(string value) { Status = value; Changed?.Invoke(); }
        void ReleaseReply() { speaker.clip = null; if(replyClip && ownsReplyClip) Destroy(replyClip); replyClip = null; ownsReplyClip=false; }
        void EndMicrophone() { if(microphone) { Microphone.End(null); Destroy(microphone); microphone = null; } }

        public void StopListening()
        {
            if (liveKit) liveKit.Close();
            generation++; wantsListening=false; Reconnecting=false; resumeAfterPause = false; enabling = false;
            interruptRequestInFlight=false;
            if(activeRequest != null) activeRequest.Abort();
            _=DisconnectRealtimeAsync();realtimeAudio=false;realtimeBuffer=null;realtimeAnswerPcm?.Dispose();realtimeAnswerPcm=null;
            Listening = false; Busy = false; IsWelcoming = false; PerformingAction = false; HasError = false; replyWindow.Cancel(); countdown = -1;
            PushToTalkActive = false; pushToTalkCapturing = false; awaitingPushToTalk = false; pushToTalkSamples.Clear();
            EndMicrophone(); detector.Reset(); interruptionDetector.Reset();
            if(speaker) { speaker.Stop(); ReleaseReply(); }
            SetStatus("Micrófono apagado");
        }
        public void SuspendForPatient()
        {
            patientMode = true;
            StopListening();
        }
        public void ResumeFromPatient()
        {
            patientMode = false;
            if(!Listening) ToggleListening();
        }
        void OnApplicationPause(bool paused)
        {
            if(lifetime == null) return;
            if(requestingPermission) return;
            if(paused)
            {
                bool resume = resumeAfterPause || Listening || IsWelcoming || enabling || wantsListening;
                StopListening(); resumeAfterPause = resume;
                if(resume) SetStatus("Micrófono pausado · vuelve a Atlas");
            }
            else ResumeIfNeeded();
        }
        void OnApplicationFocus(bool focused)
        {
            if(focused && lifetime != null && !requestingPermission) ResumeIfNeeded();
        }
        void ResumeIfNeeded()
        {
            if(!resumeAfterPause) return;
            resumeAfterPause = false;
            SetStatus("Reactivando micrófono…");
            _ = ResumeListeningAsync();
        }
        async Task ResumeListeningAsync()
        {
            int ticket = generation;
            try
            {
                await AtlasWebClock.Delay(500,lifetime.Token);
#if UNITY_ANDROID && !UNITY_EDITOR
                while(!Application.isFocused || (!AtlasWebPlatform.Active && (!OVRManager.instance || !OVRManager.instance.isUserPresent)))
                {
                    await AtlasWebClock.Delay(250,lifetime.Token);
                    if(ticket!=generation)return;
                }
#endif
                if(ticket==generation&&!Listening&&!Busy)await EnableAsync();
            }
            catch(OperationCanceledException) { }
        }
        void OnDisable() { if(lifetime != null) StopListening(); }
        void OnDestroy()
        {
            lifetime?.Cancel(); lifetime?.Dispose();
            detector.Started -= CaptureContext; detector.Completed -= AudioReady;
            interruptionDetector.Completed -= InterruptionReady;
        }
    

async Task PlayOfflineClipAsync(AudioClip clip, bool offline)
        {
            if(!Listening || clip == null || PushToTalkActive) return;
            int ticket = generation;
            owner.spokenGuide.Cancel();
            speaker.Stop();
            ReleaseReply();
            replyClip = clip;
            ownsReplyClip = false;
            speaker.clip = clip;
            speaker.Play();
            nextInterruptAt = Time.unscaledTime + .8f;
            SetStatus(offline ? "Atlas responde sin conexión" : "Atlas responde · repositorio inmediato");
            Changed?.Invoke();
            try
            {
                while(speaker.isPlaying && ticket == generation)
                    await AtlasWebClock.Delay(50, lifetime.Token);
                if(ticket == generation && Listening)
                {
                    suppressCaptureUntil = Time.unscaledTime + .45f;
                    SetStatus(IdleHint);
                }
            }
            catch(OperationCanceledException) { }
        }
}
}

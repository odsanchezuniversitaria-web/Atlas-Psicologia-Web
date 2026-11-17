#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Xyla {
public sealed class QuestLiveKitVoice : MonoBehaviour {
 public bool preferLiveKit=true;public GameObject microphoneHost;public QuestAudioEnvelope speechEnvelope;public float connectionTimeout=18;public bool echoCancellation=true,noiseSuppression=true,automaticGain=true;
 public bool Connected=>ready&&!closing;public bool Speaking=>Connected&&(state=="speaking"||level>.03f);public bool Thinking=>Connected&&state=="thinking";public bool UserSpeaking=>Connected&&(held||userSpeaking);public string State=>state;public float SpeechLevel=>Connected?level:0;
 public event Action<JObject> Message;public event Action<string> Failed;
 static QuestLiveKitVoice owner;string state="disconnected",role,agent="",currentTurn="";float level;bool ready,closing,held,userSpeaking;int epoch;
 Func<JObject> context;Func<JObject,CancellationToken,Task<JArray>> execute;CancellationTokenSource lifetime,actionTurn;TaskCompletionSource<bool> connected;
 readonly Dictionary<string,Task<JArray>> receipts=new Dictionary<string,Task<JArray>>();
 [DllImport("__Internal")]static extern void AWVoiceOpen(string receiver,string config);
 [DllImport("__Internal")]static extern void AWVoiceClose(string receiver);
 [DllImport("__Internal")]static extern void AWVoiceSend(string receiver,string payload);
 [DllImport("__Internal")]static extern void AWVoiceRpc(string receiver,string id,string payload,bool error);
 [DllImport("__Internal")]static extern void AWVoiceMute(string receiver,bool muted);
 public async Task<bool> OpenAsync(JObject details,string mode,AudioSource target,Func<JObject> snapshot,Func<JObject,CancellationToken,Task<JArray>> apply,CancellationToken cancellation){
  Close();if(owner&&owner!=this)owner.Close();owner=this;
  if(details.Value<int?>("protocol")!=1||!Uri.TryCreate(details.Value<string>("url"),UriKind.Absolute,out var uri)||uri.Scheme!="wss")throw new InvalidOperationException("Configuración LiveKit inválida.");
  role=mode;context=snapshot;execute=apply;lifetime=CancellationTokenSource.CreateLinkedTokenSource(cancellation);connected=new TaskCompletionSource<bool>();closing=false;state="connecting";
  var config=(JObject)details.DeepClone();config["_epoch"]=epoch;config["mode"]=mode;AWVoiceOpen(gameObject.name,config.ToString(Newtonsoft.Json.Formatting.None));
  float deadline=Time.realtimeSinceStartup+connectionTimeout;
  try{while(!connected.Task.IsCompleted){lifetime.Token.ThrowIfCancellationRequested();if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("El agente web no respondió.");await AtlasWebClock.Delay(25,lifetime.Token);}return await connected.Task;}
  catch{Close();throw;}
 }
 public void ReceiveWeb(string json){
  if(closing)return;
  try{
   var value=JObject.Parse(json);if(value.Value<int?>("_epoch")!=epoch)return;string kind=value.Value<string>("type"),sender=value.Value<string>("_sender");
   if(kind=="__level"){level=Mathf.Clamp01(value.Value<float>("level"));return;}
   if(kind=="__error"){connected?.TrySetException(new InvalidOperationException("No se pudo conectar la voz del navegador."));Failed?.Invoke("Voz web sin conexión. Revisa la red y el permiso del micrófono.");return;}
   if(kind=="__rpc"){_ = Rpc(value);return;}
   if(!string.IsNullOrEmpty(agent)&&sender!=agent)return;if(string.IsNullOrEmpty(sender))return;agent=sender;
   if(kind=="ready"){ready=true;state="listening";connected.TrySetResult(true);}
   else if(kind=="user_state"){userSpeaking=value.Value<string>("state")=="speaking";if(userSpeaking)CancelActions();}
   else if(kind=="transcript_final"){CancelActions();currentTurn=value.Value<string>("turn_id")??"";actionTurn=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);}
   else if(kind=="agent_state")state=value.Value<string>("state")??"listening";
   Message?.Invoke(value);
  }catch(Exception error){Debug.LogWarning("[Atlas Web Voice] Evento inválido: "+error.GetType().Name);}
 }
 async Task Rpc(JObject value){
  string id=value.Value<string>("id");
  try{
   if(string.IsNullOrEmpty(agent)||value.Value<string>("_sender")!=agent)throw new InvalidOperationException("Agente no autorizado.");
   if(value.Value<string>("method")=="atlas.context"){AWVoiceRpc(gameObject.name,id,context().ToString(Newtonsoft.Json.Formatting.None),false);return;}
   if(role!="atlas")throw new InvalidOperationException("La paciente no controla objetos.");
   var batch=JObject.Parse(value.Value<string>("payload"));string batchId=batch.Value<string>("action_batch_id");long expires=batch.Value<long?>("expires_at")??0,now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
   if(actionTurn==null||batch.Value<string>("turn_id")!=currentTurn||string.IsNullOrEmpty(batchId)||expires<now||expires>now+30000)throw new InvalidOperationException("Orden caducada.");
   if(!receipts.TryGetValue(batchId,out var result)){if(receipts.Count>=256)receipts.Clear();result=execute(batch,actionTurn.Token);receipts[batchId]=result;}
   AWVoiceRpc(gameObject.name,id,(await result).ToString(Newtonsoft.Json.Formatting.None),false);
  }catch(Exception error){AWVoiceRpc(gameObject.name,id,error.Message,true);}
 }
 public void SetPushToTalk(bool value){if(!Connected||held==value)return;held=value;if(value){CancelActions();AWVoiceMute(gameObject.name,true);}Send(new JObject{["type"]=value?"ptt_start":"ptt_end"});}
 public void Send(JObject value){if(!closing)AWVoiceSend(gameObject.name,value.ToString(Newtonsoft.Json.Formatting.None));}
 public async Task SayAsync(string text){if(!Connected)throw new InvalidOperationException("Voz desconectada.");int ticket=epoch;Send(new JObject{["type"]="speak",["text"]=text});float until=Time.realtimeSinceStartup+8;while(!Speaking&&ticket==epoch&&Time.realtimeSinceStartup<until)await AtlasWebClock.Delay(25);until=Time.realtimeSinceStartup+90;while(Speaking&&ticket==epoch&&Time.realtimeSinceStartup<until)await AtlasWebClock.Delay(25);}
 public void Interrupt(){CancelActions();AWVoiceMute(gameObject.name,true);Send(new JObject{["type"]="interrupt"});}
 void CancelActions(){currentTurn="";actionTurn?.Cancel();actionTurn?.Dispose();actionTurn=null;}
 public void Close(){CancelActions();closing=true;ready=held=userSpeaking=false;level=0;state="disconnected";epoch++;connected?.TrySetCanceled();lifetime?.Cancel();lifetime?.Dispose();lifetime=null;receipts.Clear();AWVoiceClose(gameObject.name);agent="";if(owner==this)owner=null;}
 void OnDestroy(){Close();}
}}
#else
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiveKit;
using LiveKit.Proto;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Xyla
{
    // One media owner for the entire app. Scene tools remain in QuestAtlasVoiceActions.
    public sealed class QuestLiveKitVoice : MonoBehaviour
    {
        [Header("Transporte de voz · sin secretos")]
        public bool preferLiveKit = true;
        public GameObject microphoneHost;
        [Header("Lipsync · salida PCM real")]
        public QuestAudioEnvelope speechEnvelope;
        [Range(8f, 30f)] public float connectionTimeout = 18f;
        public bool echoCancellation = true;
        public bool noiseSuppression = true;
        public bool automaticGain = true;
        public bool Connected => room != null && room.IsConnected && ready;
        public bool Speaking => Connected && (state == "speaking" || speechEnvelope.Level > .03f);
        public bool Thinking => Connected && state == "thinking";
        public bool UserSpeaking => Connected && (held || userSpeaking);
        public string State => state;
        public event Action<JObject> Message;
        public event Action<string> Failed;
        static QuestLiveKitVoice owner;
        Room room;
        MicrophoneSource capture;
        LocalAudioTrack localTrack;
        LiveKit.AudioStream output;
        AudioSource speaker;
        LiveKit.Internal.Threading.YieldInstruction pendingConnect;
        Func<JObject> context;
        Func<JObject, CancellationToken, Task<JArray>> execute;
        readonly Dictionary<string, Task<JArray>> receipts = new Dictionary<string, Task<JArray>>();
        readonly float[] meter = new float[128];
        CancellationTokenSource lifetime, actionTurn;
        string currentTurn = "";
        string role, state = "disconnected", agentIdentity = "";
        bool ready, closing, held, userSpeaking;
        int epoch;
        public float SpeechLevel => Connected ? speechEnvelope.Level : 0f;

        public async Task<bool> OpenAsync(JObject details, string mode, AudioSource target,
            Func<JObject> snapshot, Func<JObject, CancellationToken, Task<JArray>> apply, CancellationToken cancellation)
        {
            Close();
            if (owner && owner != this) owner.Close();
            owner = this;
            if (details.Value<int?>("protocol") != 1 || !details.Value<string>("url").StartsWith("wss://"))
                throw new InvalidOperationException("Configuración LiveKit inválida.");
            role = mode; speaker = target; context = snapshot; execute = apply;
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            var token = lifetime.Token;
            int ticket = ++epoch;
            closing = false; ready = false; state = "connecting"; agentIdentity = "";
            receipts.Clear();
            speaker.Stop(); speaker.clip = null; speaker.pitch = 1f; speaker.mute = false;
            speaker.spatialBlend = 0f; speaker.spatialize = false; speaker.loop = false;
            var connection = new Room();
            room = connection;
            connection.TrackSubscribed += Subscribed;
            connection.DataReceived += Received;
            connection.Disconnected += Disconnected;
            try
            {
                var connect = connection.Connect(details.Value<string>("url"), details.Value<string>("token"), new LiveKit.RoomOptions());
                pendingConnect = connect;
                float until = Time.realtimeSinceStartup + connectionTimeout;
                while (!connect.IsDone)
                {
                    token.ThrowIfCancellationRequested();
                    if (Time.realtimeSinceStartup > until) throw new TimeoutException("LiveKit no respondió.");
                    await AtlasWebClock.Delay(20, token);
                }
                pendingConnect = null;
                if (connect.IsError || ticket != epoch) throw new InvalidOperationException("No se pudo entrar a la sala de voz.");
                connection.LocalParticipant.RegisterRpcMethod("atlas.context", ContextRpc);
                connection.LocalParticipant.RegisterRpcMethod("atlas.execute", ExecuteRpc);
                if (!microphoneHost)
                {
                    microphoneHost = new GameObject("Micrófono LiveKit · AEC");
                    microphoneHost.transform.SetParent(transform, false);
                }
                if (Microphone.devices.Length == 0) throw new InvalidOperationException("No se detecta un micrófono.");
                capture = new MicrophoneSource(Microphone.devices[0], microphoneHost, new AudioProcessingOptions
                {
                    EchoCancellation = echoCancellation, NoiseSuppression = noiseSuppression, AutoGainControl = automaticGain
                });
                localTrack = LocalAudioTrack.CreateAudioTrack("microfono-atlas", capture, connection);
                var publish = connection.LocalParticipant.PublishTrack(localTrack, new TrackPublishOptions
                {
                    Source = TrackSource.SourceMicrophone, AudioEncoding = new AudioEncoding { MaxBitrate = 32000 }
                });
                while (!publish.IsDone)
                {
                    token.ThrowIfCancellationRequested();
                    if (Time.realtimeSinceStartup > until) throw new TimeoutException("No se pudo publicar el micrófono.");
                    await AtlasWebClock.Delay(20, token);
                }
                if (publish.IsError) throw new InvalidOperationException("LiveKit rechazó el micrófono.");
                capture.Start();
                if (mode == "patient") capture.SetMute(true);
                float nextHello = 0f;
                until = Time.realtimeSinceStartup + connectionTimeout;
                while (!ready)
                {
                    token.ThrowIfCancellationRequested();
                    if (ticket != epoch) throw new OperationCanceledException();
                    if (Time.realtimeSinceStartup > until) throw new TimeoutException("El agente de voz no está disponible.");
                    if (Time.realtimeSinceStartup > nextHello)
                    {
                        nextHello = Time.realtimeSinceStartup + 1f;
                        Send(new JObject { ["type"] = "hello" });
                    }
                    await AtlasWebClock.Delay(40, token);
                }
                Debug.Log("[Atlas LiveKit] Sala conectada; rol=" + role + "; audio incremental; AEC=" + echoCancellation);
                return true;
            }
            catch
            {
                if (ticket == epoch) Close();
                throw;
            }
        }

        void Subscribed(IRemoteTrack track, RemoteTrackPublication publication, RemoteParticipant participant)
        {
            if (!(track is RemoteAudioTrack audio)) return;
            if (!string.IsNullOrEmpty(agentIdentity) && agentIdentity != participant.Identity) return;
            agentIdentity = participant.Identity;
            output?.Dispose();
            output = new LiveKit.AudioStream(audio, speaker);
            speechEnvelope.BindNativeStream(speaker);
        }

        void Received(byte[] bytes, Participant participant, DataPacketKind kind, string topic)
        {
            if (closing || topic != "atlas.event" || bytes.Length > 15000 || participant == null) return;
            if (room == null || participant.Identity == room.LocalParticipant.Identity) return;
            if (!string.IsNullOrEmpty(agentIdentity) && participant.Identity != agentIdentity) return;
            agentIdentity = participant.Identity;
            try
            {
                var value = JObject.Parse(Encoding.UTF8.GetString(bytes));
                string type = value.Value<string>("type");
                if (type == "ready") ready = true;
                else if (type == "user_state")
                {
                    userSpeaking = value.Value<string>("state") == "speaking";
                    if (userSpeaking) CancelActions();
                }
                else if (type == "transcript_final")
                {
                    CancelActions(); currentTurn = value.Value<string>("turn_id") ?? "";
                    actionTurn = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                }
                else if (type == "agent_state")
                {
                    state = value.Value<string>("state") ?? "listening";
                    if (state == "speaking") speaker.mute = false;
                }
                Message?.Invoke(value);
            }
            catch (Exception ex) { Debug.LogWarning("[Atlas LiveKit] Evento inválido: " + ex.GetType().Name); }
        }

        Task<string> ContextRpc(RpcInvocationData data)
        {
            Authorize(data);
            return Task.FromResult(context().ToString(Newtonsoft.Json.Formatting.None));
        }

        async Task<string> ExecuteRpc(RpcInvocationData data)
        {
            Authorize(data);
            if (role != "atlas" || data.Payload.Length > 12000) throw new InvalidOperationException("Herramienta no disponible.");
            var batch = JObject.Parse(data.Payload);
            if (string.IsNullOrEmpty(currentTurn) || batch.Value<string>("turn_id") != currentTurn || actionTurn == null)
                throw new InvalidOperationException("La pregunta cambió; orden cancelada.");
            string id = batch.Value<string>("action_batch_id");
            long expires = batch.Value<long?>("expires_at") ?? 0;
            if (string.IsNullOrEmpty(id) || expires < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                || expires > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 30000)
                throw new InvalidOperationException("La orden ha caducado.");
            if (!receipts.TryGetValue(id, out var result))
            {
                if (receipts.Count >= 256)
                {
                    string completedId = null;
                    foreach (var previous in receipts) if (previous.Value.IsCompleted) { completedId = previous.Key; break; }
                    if (completedId == null) throw new InvalidOperationException("Hay demasiadas órdenes pendientes.");
                    receipts.Remove(completedId);
                }
                result = execute(batch, actionTurn.Token);
                receipts.Add(id, result);
            }
            var completed = await result;
            return completed.ToString(Newtonsoft.Json.Formatting.None);
        }

        void Authorize(RpcInvocationData data)
        {
            if (closing || room == null || string.IsNullOrEmpty(agentIdentity) || data.CallerIdentity != agentIdentity)
                throw new InvalidOperationException("Agente no autorizado.");
        }

        public void SetPushToTalk(bool value)
        {
            if (!Connected || held == value) return;
            held = value;
            if (value)
            {
                CancelActions();
                speaker.mute = true;
                capture.SetMute(false);
            }
            Send(new JObject { ["type"] = value ? "ptt_start" : "ptt_end" });
            if (!value && role == "patient") _ = MuteAfterFlushAsync(epoch);
        }

        async Task MuteAfterFlushAsync(int ticket)
        {
            await AtlasWebClock.Delay(160);
            if (ticket == epoch && !held && role == "patient") capture?.SetMute(true);
        }

        public void Send(JObject value)
        {
            if (room == null || !room.IsConnected) return;
            room.LocalParticipant.PublishData(Encoding.UTF8.GetBytes(value.ToString(Newtonsoft.Json.Formatting.None)),
                reliable: true, topic: "atlas.control");
        }

        public async Task SayAsync(string text)
        {
            if (!Connected) throw new InvalidOperationException("Voz desconectada.");
            int ticket = epoch;
            Send(new JObject { ["type"] = "speak", ["text"] = text });
            float until = Time.realtimeSinceStartup + 8f;
            while (!Speaking && ticket == epoch && Time.realtimeSinceStartup < until) await AtlasWebClock.Delay(25);
            until = Time.realtimeSinceStartup + 90f;
            while (Speaking && ticket == epoch && Time.realtimeSinceStartup < until) await AtlasWebClock.Delay(25);
        }

        public void Interrupt()
        {
            CancelActions();
            if (speaker) speaker.mute = true;
            Send(new JObject { ["type"] = "interrupt" });
        }

        void Disconnected(Room disconnectedRoom)
        {
            if (closing) return;
            ready = false; state = "disconnected";
            Failed?.Invoke("Se perdió la conexión de voz. Vuelve a activar el micrófono.");
        }

        public void Close()
        {
            CancelActions();
            closing = true; epoch++; ready = false; held = false; userSpeaking = false; state = "disconnected";
            lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
            capture?.Dispose(); capture = null; localTrack = null;
            if (speechEnvelope) speechEnvelope.UnbindNativeStream();
            output?.Dispose(); output = null;
            var previous = room; room = null;
            if (previous != null)
            {
                previous.Disconnected -= Disconnected;
                previous.TrackSubscribed -= Subscribed;
                previous.DataReceived -= Received;
                if (pendingConnect != null && !pendingConnect.IsDone) _ = DisposePendingAsync(previous, pendingConnect);
                else { previous.Disconnect(); previous.Dispose(); }
            }
            pendingConnect = null;
            if (speaker) { speaker.Stop(); speaker.mute = false; }
            if (owner == this) owner = null;
        }

        static async Task DisposePendingAsync(Room previous, LiveKit.Internal.Threading.YieldInstruction pending)
        {
            await pending;
            previous.Disconnect(); previous.Dispose();
        }

        void CancelActions()
        {
            currentTurn = "";
            actionTurn?.Cancel(); actionTurn?.Dispose(); actionTurn = null;
        }
        void OnDestroy() { Close(); }
    }
}

#endif

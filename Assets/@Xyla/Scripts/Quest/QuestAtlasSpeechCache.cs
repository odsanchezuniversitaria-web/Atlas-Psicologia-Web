using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Xyla {
public static class QuestAtlasSpeechCache {
    static string profile;
    static string server;
    static float nextCheck;
    static string Root => Path.Combine(Application.persistentDataPath, "AtlasVoiceProfiles");
    static string Hash(string value) {
        using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
    }
    static void Bind(QuestAtlasVoiceAgent voice) {
        string current=voice.PatientServerUrl;
        if(server==current)return;
        server=current; profile=null; nextCheck=0f;
        string path=Path.Combine(Root,Hash(server)+".profile");
        if(File.Exists(path))profile=File.ReadAllText(path);
    }
    static bool Valid(byte[] bytes) => bytes!=null && bytes.Length>=2 && bytes.Length<=4000000 && bytes.Length%2==0;
    static string AudioPath(string text) => Path.Combine(Root,Hash(server+"|"+profile+"|"+text)+".pcm");
    public static byte[] TryRead(QuestAtlasVoiceAgent voice,string text) {
        try {
            Bind(voice);
            if(string.IsNullOrEmpty(profile))return null;
            string path=AudioPath(text);
            if(!File.Exists(path))return null;
            byte[] bytes=File.ReadAllBytes(path);
            return Valid(bytes)?bytes:null;
        } catch(Exception) {return null;}
    }
    static async Task<JObject> Request(QuestAtlasVoiceAgent voice,string path,JObject payload,CancellationToken token) {
        if(AtlasWebPlatform.Active)return await AtlasWebPlatform.RequestAsync(path,payload,token);
        string bearer=await voice.PatientAccessTokenAsync(token);
        using(var request=new UnityWebRequest(voice.PatientServerUrl+path,payload==null?"GET":"POST")) {
            if(payload!=null)request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload.ToString()));
            request.downloadHandler=new DownloadHandlerBuffer();request.timeout=payload==null?6:25;
            request.SetRequestHeader("Content-Type","application/json");
            request.SetRequestHeader("Authorization","Bearer "+bearer);
            using(token.Register(()=>request.Abort())) {
                var op=request.SendWebRequest();
                while(!op.isDone){token.ThrowIfCancellationRequested();await AtlasWebClock.Delay(40,token);}
                token.ThrowIfCancellationRequested();
                if(request.result!=UnityWebRequest.Result.Success)throw new InvalidOperationException("El servicio de voz no respondió.");
                return JObject.Parse(request.downloadHandler.text);
            }
        }
    }
    static void SetProfile(string value) {
        if(string.IsNullOrWhiteSpace(value)||value.Length>120)throw new InvalidOperationException("El servidor no indicó el perfil de voz.");
        profile=value;
        try {Directory.CreateDirectory(Root);File.WriteAllText(Path.Combine(Root,Hash(server)+".profile"),profile);}
        catch(Exception ex){Debug.LogWarning("[Atlas Voice] No se pudo guardar el perfil: "+ex.GetType().Name);}
    }
    public static async Task<byte[]> GetAsync(QuestAtlasVoiceAgent voice,string text,CancellationToken token) {
        Bind(voice);
        if(Time.realtimeSinceStartup>=nextCheck) {
            nextCheck=Time.realtimeSinceStartup+60f;
            try {
                JObject health=await Request(voice,"/health",null,token);
                SetProfile(health.Value<string>("voice_profile"));
            } catch(OperationCanceledException){throw;}
            catch(Exception) {
                byte[] saved=TryRead(voice,text);
                if(saved!=null)return saved;
            }
        }
        byte[] cached=TryRead(voice,text);
        if(cached!=null)return cached;
        JObject response=await Request(voice,"/speech",new JObject {["text"]=text},token);
        string encoded=response.Value<string>("audio_pcm");
        if(string.IsNullOrEmpty(encoded))throw new InvalidOperationException(response.Value<string>("audio_warning")??response.Value<string>("message")??"La voz no está disponible.");
        byte[] bytes=Convert.FromBase64String(encoded);
        if(!Valid(bytes))throw new InvalidOperationException("Audio de voz inválido.");
        SetProfile(response.Value<string>("voice_profile"));
        try {Directory.CreateDirectory(Root);File.WriteAllBytes(AudioPath(text),bytes);}
        catch(Exception ex){Debug.LogWarning("[Atlas Voice] Audio sin caché: "+ex.GetType().Name);}
        return bytes;
    }
    public static AudioClip Clip(byte[] bytes,string name) {
        if(!Valid(bytes))throw new InvalidOperationException("Audio de voz inválido.");
        float[] samples=AtlasVoiceDetector.DecodePcm16(bytes);
        AudioClip clip=AudioClip.Create(name,samples.Length,1,24000,false);clip.SetData(samples,0);return clip;
    }
}
}

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
namespace Xyla {
public static class AtlasWebPlatform {
 public static bool Active=>AtlasWebRuntime.Current;
 public static string ApiBaseUrl="https://api.oscar-vr.iudcdesarrollo.tech/api/v1/atlas",AccessToken="",Profile="practice";public static bool Evaluated;
#if UNITY_WEBGL && !UNITY_EDITOR
 [DllImport("__Internal")]static extern void AWState(string json);
 [DllImport("__Internal")]static extern void AWReady(string receiver);
 [DllImport("__Internal")]static extern void AWHaptic(float strength,float seconds);
 [DllImport("__Internal")]static extern void AWCheckpoint(string json);
#endif
 public static void Ready(){
#if UNITY_WEBGL && !UNITY_EDITOR
 AWReady("AtlasWeb");
#endif
 }
 public static void State(JObject value){
#if UNITY_WEBGL && !UNITY_EDITOR
 AWState(value.ToString(Newtonsoft.Json.Formatting.None));
#endif
 }
 public static void Haptic(float strength,float seconds){if(Active)AtlasWebRuntime.Current.Pulse(strength,seconds);}
 public static void Save(JObject value){
#if UNITY_WEBGL && !UNITY_EDITOR
 AWCheckpoint(value.ToString(Newtonsoft.Json.Formatting.None));
#endif
 }
 public static async Task<JObject> RequestAsync(string path,JObject payload,CancellationToken token){
  if(string.IsNullOrEmpty(AccessToken))throw new InvalidOperationException("Abre Atlas desde Nexus para habilitar la voz.");
  var uri=new Uri(ApiBaseUrl.TrimEnd('/')+path);
  if(uri.Scheme!="https"&&!(uri.Scheme=="http"&&(uri.IsLoopback)))throw new InvalidOperationException("La voz necesita HTTPS.");
  using(var request=new UnityWebRequest(uri,path=="/health"?"GET":"POST")){
   if(path!="/health")request.uploadHandler=new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes((payload??new JObject()).ToString(Newtonsoft.Json.Formatting.None)));
   request.downloadHandler=new DownloadHandlerBuffer();request.timeout=30;request.SetRequestHeader("Content-Type","application/json");
   if(!string.IsNullOrEmpty(AccessToken))request.SetRequestHeader("Authorization","Bearer "+AccessToken);
   var send=request.SendWebRequest();while(!send.isDone){token.ThrowIfCancellationRequested();await AtlasWebClock.Delay(20,token);}
   token.ThrowIfCancellationRequested();if(request.result!=UnityWebRequest.Result.Success)throw new InvalidOperationException("Servicio de voz no disponible ("+request.responseCode+").");
   return JObject.Parse(request.downloadHandler.text);
  }
 }
}
}

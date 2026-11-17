using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace Xyla {
public sealed class AtlasWebClock : MonoBehaviour {
 sealed class Wait { public float deadline; public TaskCompletionSource<bool> done; public CancellationToken token; }
 static AtlasWebClock instance; readonly List<Wait> waits=new List<Wait>();
 public static Task Delay(int milliseconds,CancellationToken token=default(CancellationToken)){
#if UNITY_WEBGL && !UNITY_EDITOR
  if(token.IsCancellationRequested)return Task.FromCanceled(token);
  if(!instance){var go=new GameObject("WEB · reloj asíncrono");instance=go.AddComponent<AtlasWebClock>();DontDestroyOnLoad(go);}
  var wait=new Wait{deadline=Time.realtimeSinceStartup+milliseconds/1000f,done=new TaskCompletionSource<bool>(),token=token};instance.waits.Add(wait);return wait.done.Task;
#else
  return Task.Delay(milliseconds,token);
#endif
 }
 public static Task Delay(TimeSpan duration,CancellationToken token=default(CancellationToken)){return Delay((int)duration.TotalMilliseconds,token);}
 void Update(){for(int i=waits.Count-1;i>=0;i--){var w=waits[i];if(w.token.IsCancellationRequested){waits.RemoveAt(i);w.done.TrySetCanceled(w.token);}else if(Time.realtimeSinceStartup>=w.deadline){waits.RemoveAt(i);w.done.TrySetResult(true);}}}
 void OnDestroy(){foreach(var w in waits)w.done.TrySetCanceled();waits.Clear();if(instance==this)instance=null;}
}}

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
namespace Xyla {
public sealed class QuestAtlasSpokenGuide : MonoBehaviour {
 public Text caption;
 public AudioSource speaker;
 [TextArea(2,5)] public string[] atlasSteps = {
 "Paso uno. Para hablar conmigo, mantén pulsado A en el control derecho, o X en el izquierdo. Habla y suelta el botón al terminar. No necesitas decir Atlas.",
 "Paso dos. Apunta a una pieza con el rayo y pregunta: ¿qué es esto?, o ¿para qué sirve? También puedes preguntar sobre anatomía sin señalar nada.",
 "Paso tres. Puedes pedirme: muestra el sistema óseo, oculta los músculos, o acércame cualquier costilla. Para agarrar una pieza, apunta y pulsa el botón lateral de agarre.",
 "Paso cuatro. Usa la palanca para desplazarte y el botón de menú del control izquierdo para abrir las opciones. Pulsa Y para repetir estas instrucciones. Ya puedes comenzar."
 };
 [TextArea(2,5)] public string[] patientSteps = {
 "Soy Atlas. Te guiaré en esta entrevista simulada. María representa un caso educativo de ansiedad. Escucha su experiencia antes de proponer conclusiones.",
 "Mantén A o X mientras hablas con María y suéltalo para enviar la pregunta. El indicador muestra si estás hablando, si María está pensando o si está respondiendo.",
 "Comienza con un saludo y una pregunta abierta, por ejemplo: hola María, ¿qué te trae hoy? Después explora sus preocupaciones, el sueño y cómo se siente en su vida diaria.",
 "Cuando termines, pulsa Finalizar entrevista para ver la retroalimentación. Puedes pulsar Y para repetir esta guía. Ahora escucharás a María."
 };
 public bool Playing {get; private set;}
 public Func<bool> CanPlayPatientGuide;
 QuestAtlasVoiceAgent voice;
 CancellationTokenSource session;
 int generation;
 public void Initialize(QuestAtlasVoiceAgent value) { voice=value; caption.transform.parent.gameObject.SetActive(false); }
 public void Cancel() {
 generation++;
 var previous=session; session=null;
 previous?.Cancel();
 speaker.Stop(); speaker.clip=null;
 caption.transform.parent.gameObject.SetActive(false);
 Playing=false;
 }
 public async Task PlayGuide(bool patient) {
 if(Playing || (patient && CanPlayPatientGuide != null && !CanPlayPatientGuide())) return;
 if(!patient && (voice.IsWelcoming || voice.Busy || voice.Speaking)) return;
 var current=new CancellationTokenSource(); session=current;
 var token=current.Token; int ticket=++generation; Playing=true;
 try {
 var steps=patient?patientSteps:atlasSteps;
 var viewer=Camera.main;
 if(viewer) {
 var forward=Vector3.ProjectOnPlane(viewer.transform.forward,Vector3.up).normalized;
 if(forward.sqrMagnitude<.1f) forward=Vector3.forward;
 caption.transform.parent.SetPositionAndRotation(viewer.transform.position+forward*1.25f-Vector3.up*.3f,Quaternion.LookRotation(forward));
 }
 speaker.volume=voice.VoiceVolume;
 caption.transform.parent.gameObject.SetActive(true);
 bool audioAvailable=true;
 for(int i=0;i<steps.Length;i++) {
 token.ThrowIfCancellationRequested();
 caption.text="GUÍA DE ATLAS · "+(i+1)+" / "+steps.Length+"\n"+steps[i]+"\nA / X: omitir · Y: repetir";
 AudioClip clip=null;
 try {
 try { if(audioAvailable) clip=await GetClip(steps[i],token); }
 catch(OperationCanceledException){throw;}
 catch(Exception ex){ audioAvailable=false; Debug.LogWarning("[Atlas Guide] Voz no disponible: "+ex.Message); }
 // A canceled network request may still finish: it must never restart the old guide.
 token.ThrowIfCancellationRequested();
 if(ticket!=generation) return;
 if(clip) {
 speaker.clip=clip; speaker.Play();
 while(speaker.isPlaying) await AtlasWebClock.Delay(60,token);
 } else await AtlasWebClock.Delay(6500,token);
 } finally {
 if(clip) {
 if(speaker.clip==clip) {speaker.Stop(); speaker.clip=null;}
 Destroy(clip);
 }
 }
 await AtlasWebClock.Delay(250,token);
 }
 } catch(OperationCanceledException) { }
 finally {
 if(ticket==generation) {
 speaker.Stop(); speaker.clip=null;
 caption.transform.parent.gameObject.SetActive(false);
 Playing=false; session=null;
 }
 current.Dispose();
 }
 }
 async Task<AudioClip> GetClip(string text,CancellationToken token) {
 byte[] bytes=await QuestAtlasSpeechCache.GetAsync(voice,text,token);
 token.ThrowIfCancellationRequested();
 return QuestAtlasSpeechCache.Clip(bytes,"Guía de Atlas");
 }
 void OnDisable(){Cancel();}
}
}

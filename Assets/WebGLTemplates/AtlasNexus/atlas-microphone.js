/* Local, user-triggered microphone diagnostic. Audio is never recorded or uploaded. */
(function(){
'use strict';const $=id=>document.getElementById(id);let active=null,epoch=0;
function message(value){$('microphone-status').textContent=value;}
function describeError(error){
 if(error?.name==='NotAllowedError'||error?.name==='SecurityError')return 'Permiso de micrófono bloqueado. Permítelo en los ajustes de este sitio y vuelve a probar.';
 if(error?.name==='NotFoundError'||error?.name==='DevicesNotFoundError')return 'No se encontró un micrófono. Conéctalo o selecciona una entrada de audio en el navegador.';
 if(error?.name==='NotReadableError'||error?.name==='TrackStartError')return 'No se puede abrir el micrófono. Revisa si otra aplicación lo está usando o si está desactivado en el sistema.';
 return 'No fue posible comprobar el micrófono. Revisa el permiso del sitio y el dispositivo de entrada.';
}
function release(c){if(!c)return;clearInterval(c.interval);clearTimeout(c.timeout);try{c.source?.disconnect();c.analyser?.disconnect();}catch{}for(const track of c.stream?.getTracks()||[])track.stop();if(c.context)c.context.close().catch(()=>{});}
function stop(reason){
 ++epoch;const c=active;active=null;release(c);$('microphone-test').textContent='Probar micrófono';$('microphone-test').setAttribute('aria-pressed','false');$('microphone-level').value=0;
 if(reason)message(reason);else if(c)message(c.detected?'Micrófono funcionando: se detectó señal. Prueba finalizada.':'Micrófono abierto, pero sin señal suficiente. Habla cerca de él y revisa la entrada y su volumen.');
}
async function test(){
 if(active){stop();return;}
 if(!window.isSecureContext){message('El micrófono necesita una página HTTPS.');return;}
 if(!navigator.mediaDevices?.getUserMedia){message('Este navegador no ofrece acceso al micrófono. Prueba un navegador actualizado y revisa sus permisos.');return;}
 const c={id:++epoch,stream:null,context:null,source:null,analyser:null,detected:false};active=c;
 $('microphone-test').textContent='Detener prueba';$('microphone-test').setAttribute('aria-pressed','true');message('Esperando permiso del navegador…');
 try{
  const stream=await navigator.mediaDevices.getUserMedia({audio:{echoCancellation:true,noiseSuppression:true,autoGainControl:true},video:false});
  if(active!==c||epoch!==c.id){for(const track of stream.getTracks())track.stop();return;}c.stream=stream;
  const C=window.AudioContext||window.webkitAudioContext;c.context=new C();await c.context.resume();
  if(active!==c||epoch!==c.id){release(c);return;}
  c.source=c.context.createMediaStreamSource(stream);c.analyser=c.context.createAnalyser();c.analyser.fftSize=512;c.source.connect(c.analyser);
  // No connection to speakers; no echo, MediaRecorder, network call or stored audio.
  const samples=new Float32Array(c.analyser.fftSize);message('Micrófono abierto. Habla durante unos segundos; observa el nivel.');
  c.interval=setInterval(()=>{
   c.analyser.getFloatTimeDomainData(samples);let energy=0;for(const v of samples)energy+=v*v;const rms=Math.sqrt(energy/samples.length);
   $('microphone-level').value=Math.min(1,rms*12);
   if(rms>.003){c.detected=true;message('Se detecta señal del micrófono. La prueba es local y no envía audio.');}
  },80);
  c.timeout=setTimeout(()=>stop(),8000);
 }catch(error){if(active===c)stop(describeError(error));else release(c);}
}
$('microphone-test').addEventListener('click',()=>test());
document.addEventListener('visibilitychange',()=>{if(document.hidden&&active)stop('Prueba detenida al salir de la página.');});
window.addEventListener('pagehide',()=>{if(active)stop('Prueba detenida.');});
window.AtlasMicrophone={test,stop,describeError,isTesting:()=>!!active};
})();

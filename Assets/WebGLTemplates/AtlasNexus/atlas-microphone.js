/* User-triggered local input diagnostic; no recording, uploads or saved device IDs. */
(function(){
'use strict';const $=id=>document.getElementById(id);let active=null,epoch=0;
function message(value){$('microphone-status').textContent=value;}
function captureOptions(){const id=$('microphone-device').value;return {echoCancellation:true,noiseSuppression:true,autoGainControl:true,channelCount:1,...(id?{deviceId:{exact:id}}:{})};}
async function refresh(){
 if(!navigator.mediaDevices?.enumerateDevices)return;
 try{
  const devices=(await navigator.mediaDevices.enumerateDevices()).filter(d=>d.kind==='audioinput'&&d.deviceId&&d.deviceId!=='default'&&d.deviceId!=='communications');
  const chosen=$('microphone-device').value;
  $('microphone-device').replaceChildren(new Option('Predeterminado del navegador',''),...devices.map((d,i)=>new Option(d.label||'Micrófono '+(i+1),d.deviceId)));
  $('microphone-device').value=devices.some(d=>d.deviceId===chosen)?chosen:'';
  $('microphone-device-help').textContent=devices.length&&devices.every(d=>/steam|oculus|virtual/i.test(d.label))?'Sólo aparecen entradas virtuales. Windows debe reconocer el micrófono de tus auriculares antes de poder elegirlo aquí.':'Elige la entrada que quieres usar y pulsa Probar micrófono. Si faltan nombres, permite primero el acceso.';
  if(chosen&&!devices.some(d=>d.deviceId===chosen)&&active)stop('La entrada elegida ya no está disponible. Elige otra y vuelve a probar.');
 }catch{$('microphone-device-help').textContent='No fue posible listar las entradas. Revisa el permiso de micrófono y pulsa Actualizar entradas.';}
}
function describeError(error){
 if(error?.name==='NotAllowedError'||error?.name==='SecurityError')return 'Permiso de micrófono bloqueado. Permítelo en los ajustes de este sitio y vuelve a probar.';
 if(error?.name==='NotFoundError'||error?.name==='DevicesNotFoundError')return 'No se encontró un micrófono. Conéctalo o selecciona una entrada de audio en el navegador.';
 if(error?.name==='OverconstrainedError')return 'La entrada elegida ya no está disponible. Pulsa Actualizar entradas y selecciona otro micrófono.';
 if(error?.name==='NotReadableError'||error?.name==='TrackStartError')return 'No se puede abrir el micrófono. Revisa si otra aplicación lo está usando o si está desactivado en el sistema.';
 if(error?.name==='AudioContextSuspended')return 'El navegador suspendió la prueba de audio. Mantén esta pestaña activa y vuelve a pulsar Probar micrófono.';
 return 'No fue posible comprobar el micrófono. Revisa el permiso del sitio y el dispositivo de entrada.';
}
function release(c){if(!c)return;clearInterval(c.interval);clearTimeout(c.timeout);try{c.source?.disconnect();c.analyser?.disconnect();c.silent?.disconnect();}catch{}for(const track of c.stream?.getTracks()||[])track.stop();if(c.context)c.context.close().catch(()=>{});}
function stop(reason){
 ++epoch;const c=active;active=null;release(c);$('microphone-test').textContent='Probar micrófono';$('microphone-test').setAttribute('aria-pressed','false');
 if(c)$('microphone-level').value=c.peak;
 if(reason)message(reason);else if(c)message(c.detected?'Micrófono funcionando: se detectó señal. La barra conserva el nivel máximo de la prueba.':'No se detectó señal suficiente en '+(c.label||'esta entrada')+'. Elige otra entrada y revisa el conector y el silencio de los auriculares.');
}
async function test(){
 if(active){stop();return;}
 if(!window.isSecureContext){message('El micrófono necesita una página HTTPS.');return;}
 if(!navigator.mediaDevices?.getUserMedia){message('Este navegador no ofrece acceso al micrófono. Prueba un navegador actualizado y revisa sus permisos.');return;}
 const c={id:++epoch,stream:null,context:null,source:null,analyser:null,silent:null,detected:false,peak:0,label:''};active=c;
 $('microphone-level').value=0;$('microphone-device-name').textContent='';$('microphone-test').textContent='Detener prueba';$('microphone-test').setAttribute('aria-pressed','true');message('Esperando permiso del navegador…');
 try{
  const stream=await navigator.mediaDevices.getUserMedia({audio:captureOptions(),video:false});
  if(active!==c||epoch!==c.id){for(const track of stream.getTracks())track.stop();return;}c.stream=stream;
  const track=stream.getAudioTracks?.()[0]||stream.getTracks()[0];c.label=track?.label||'el micrófono predeterminado';$('microphone-device-name').textContent='Entrada abierta: '+c.label;
  await refresh();if(active!==c||epoch!==c.id){release(c);return;}
  const C=window.AudioContext||window.webkitAudioContext;c.context=new C();await c.context.resume();
  if(active!==c||epoch!==c.id){release(c);return;}
  if(c.context.state&&c.context.state!=='running')throw Object.assign(new Error(),{name:'AudioContextSuspended'});
  c.source=c.context.createMediaStreamSource(stream);c.analyser=c.context.createAnalyser();c.silent=c.context.createGain();c.silent.gain.value=0;c.analyser.fftSize=512;c.source.connect(c.analyser);c.analyser.connect(c.silent);c.silent.connect(c.context.destination);
  // A zero-gain output keeps analysis active without playing microphone audio.
  const samples=new Float32Array(c.analyser.fftSize);message('Habla ahora durante ocho segundos. Observa el nivel de esta entrada.');
  c.interval=setInterval(()=>{
   c.analyser.getFloatTimeDomainData(samples);let energy=0;for(const v of samples)energy+=v*v;const rms=Math.sqrt(energy/samples.length),level=Math.min(1,rms*12);
   c.peak=Math.max(c.peak,level);$('microphone-level').value=level;
   if(rms>.003){c.detected=true;message('Se detecta señal del micrófono. La prueba es local y no envía audio.');}
  },80);c.timeout=setTimeout(()=>stop(),8000);
 }catch(error){if(active===c)stop(describeError(error));else release(c);}
}
$('microphone-test').addEventListener('click',()=>test());$('microphone-refresh').addEventListener('click',()=>refresh());
$('microphone-device').addEventListener('change',async()=>{if(active)stop('Entrada cambiada. Pulsa Probar micrófono para comprobarla.');$('microphone-device-name').textContent='';try{await window.AtlasVoice?.setInputDevice?.($('microphone-device').value);}catch(error){message(describeError(error));}});
navigator.mediaDevices?.addEventListener?.('devicechange',()=>refresh());
document.addEventListener('visibilitychange',()=>{if(document.hidden&&active)stop('Prueba detenida al salir de la página.');});
window.addEventListener('pagehide',()=>{if(active)stop('Prueba detenida.');});
window.AtlasMicrophone={test,stop,refresh,captureOptions,describeError,isTesting:()=>!!active};refresh();
})();

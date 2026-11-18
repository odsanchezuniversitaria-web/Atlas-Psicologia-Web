/* Static Atlas client for GitHub Pages and a Nexus host. No credentials on disk. */
(function(){
'use strict';const $=id=>document.getElementById(id),cfg=window.ATLAS_CONFIG||{};
let unity=null,receiver='',state={},toastTimer=0,firstFrame=false,expectedLanding=null,selectedTab='systems',devPrevious=null;
let voiceFailure='';
let accessToken='',profile='practice',expiresAt=0,expiryTimer=0,checkpoint=empty();
const hostOrigins=new Set(cfg.nexusOrigins||[]),mobile=matchMedia('(pointer:coarse)').matches,input={moveX:0,moveY:0,lookX:0,lookY:0,x:.5,y:.5,talk:false,typing:false,ui:false};
const text=(id,v)=>{if($(id)&&$(id).textContent!==String(v??''))$(id).textContent=String(v??'');};
function empty(){return {schemaVersion:1,missions:[],milestones:[],flags:{},reports:{},poses:[]};}
function emit(method,payload){if(unity&&receiver)unity.SendMessage(receiver,method,typeof payload==='string'?payload:JSON.stringify(payload));}
function configuration(){return {apiBaseUrl:cfg.apiBaseUrl||'https://api.oscar-vr.iudcdesarrollo.tech/api/v1/atlas',accessToken,profileKey:profile,evaluated:false,checkpoint};}
function hasVoiceSession(){return !!accessToken&&Date.now()<expiresAt;}
function voiceUI(){
 const enabled=hasVoiceSession();
 text('speech-status',enabled?(voiceFailure||state.status||'Conectando con el servicio de voz…'):'IA sin sesión de Nexus. La conversación no está habilitada; esto no indica un fallo del micrófono.');
 text('voice-state',enabled?(voiceFailure?'Revisa el panel Asistente':state.talk?'Te escucho':state.speaking?(state.patientOpen?'María habla':'Atlas habla'):state.thinking?'Preparando respuesta…':'Mantén para hablar'):'IA sin sesión de Nexus');
 text('talk',enabled?'Mantén para hablar':'Probar micrófono');
 document.querySelector('[data-op="listen"]').disabled=!enabled;
 $('talk').classList.toggle('recording',enabled&&!!state.talk);
 if(!enabled)text('transcript','No se está enviando voz a la IA. Abre desde una sesión válida de Nexus. Puedes comprobar el micrófono con la prueba local de abajo.');
}
function configure(){emit('Configure',configuration());voiceUI();text('profile-label',devPrevious?'Modo de pruebas':accessToken?'Nexus · voz vinculada':'Práctica local · IA sin sesión');text('session-help',accessToken?'Nexus habilitó la voz de Atlas y María.':'Puedes explorar y guardar tu avance. La conversación con Atlas y María requiere una sesión de Nexus; el botón Probar micrófono sólo comprueba tu dispositivo.');$('logout').hidden=!accessToken;}
function toast(message){text('toast',message);$('toast').hidden=false;clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('toast').hidden=true,6500);}
function error(message){toast(message);if(!firstFrame)text('loading-text',message);}
function draftKey(){return 'atlas.psicologia.progress.v1.'+profile;}
function readDraft(){try{const v=JSON.parse(localStorage.getItem(draftKey())||'null');return v?.schemaVersion===1?v:empty();}catch{return empty();}}
function notify(type,payload){const message={type,version:1,profileId:profile,...payload};for(const origin of hostOrigins){if(window.parent!==window)window.parent.postMessage(message,origin);if(window.opener)window.opener.postMessage(message,origin);}window.dispatchEvent(new CustomEvent(type,{detail:message}));}
function save(value){if(!value||value.schemaVersion!==1)return;checkpoint=structuredClone(value);try{localStorage.setItem(draftKey(),JSON.stringify(checkpoint));text('sync','Avance guardado en este navegador');}catch{text('sync','No se pudo guardar el avance local');}if(accessToken&&!devPrevious)notify('atlas:progress',{checkpoint});}
async function connect(value){
 if(!value||typeof value.accessToken!=='string'||value.accessToken.length<32||value.accessToken.length>8192||!Number.isFinite(value.expiresAt)||value.expiresAt<=Date.now())throw new Error('Nexus debe entregar un acceso de voz temporal válido.');
 if(typeof value.profileId!=='string'||!/^[-_a-zA-Z0-9]{1,100}$/.test(value.profileId))throw new Error('Nexus debe entregar un identificador de perfil válido.');
 const changed=profile!==value.profileId;if(changed){save(checkpoint);await window.AtlasVoice?.closeAll();}
 if(window.AtlasMicrophone?.isTesting())window.AtlasMicrophone.stop('Prueba finalizada al activar la voz de Nexus.');voiceFailure='';
 accessToken=value.accessToken;expiresAt=Math.min(value.expiresAt,Date.now()+3600000);profile=value.profileId;devPrevious=null;
 checkpoint=value.checkpoint?.schemaVersion===1?structuredClone(value.checkpoint):readDraft();clearTimeout(expiryTimer);expiryTimer=setTimeout(()=>{logout();notify('atlas:session-required',{});toast('El acceso de voz terminó. Abre de nuevo Atlas desde Nexus.');},expiresAt-Date.now());
 if(changed&&unity&&!state.landing)command('home');configure();notify('atlas:connected',{});
}
async function logout(){voiceFailure='';save(checkpoint);await window.AtlasVoice?.closeAll();accessToken='';expiresAt=0;clearTimeout(expiryTimer);profile='practice';devPrevious=null;checkpoint=readDraft();if(unity&&!state.landing)command('home');configure();}
async function api(route,payload={}){
 if(!accessToken||Date.now()>=expiresAt)throw new Error('Abre Atlas desde Nexus para habilitar la voz.');
 if(!['/health','/speech','/livekit/session','/patient/feedback'].includes(route))throw new Error('Operación no disponible.');
 const response=await fetch(configuration().apiBaseUrl.replace(/\/$/,'')+route,{method:route==='/health'?'GET':'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+accessToken},...(route==='/health'?{}:{body:JSON.stringify(payload)}),credentials:'omit',cache:'no-store',redirect:'error',signal:AbortSignal.timeout(35000)});
 if(!response.ok)throw new Error(response.status===401?'El acceso de voz caducó. Abre desde Nexus.':'El servicio de voz no está disponible ('+response.status+').');return response.json();
}
window.addEventListener('message',event=>{
 const trusted=(event.source===window.parent&&window.parent!==window)||(window.opener&&event.source===window.opener);if(!trusted||!hostOrigins.has(event.origin)||!event.data||event.data.version!==1)return;
 if(event.data.type==='atlas:session')connect(event.data).catch(e=>toast(e.message));
 if(event.data.type==='atlas:logout')logout();
 if(event.data.type==='atlas:progress-ack'&&event.data.profileId===profile)text('sync','Nexus confirmó la recepción del avance');
});
function unlockAudio(){try{const C=window.AudioContext||window.webkitAudioContext;if(!window.AtlasAudioContext)window.AtlasAudioContext=new C();window.AtlasAudioContext.resume().catch(()=>{});}catch{}}
function command(op,extra={}){
 if(op==='listen'&&!hasVoiceSession()){tab('assistant');voiceUI();return;}
 if(op==='listen'&&window.AtlasMicrophone?.isTesting())window.AtlasMicrophone.stop('Prueba finalizada al activar la conversación.');
 unlockAudio();if(!unity||!receiver){toast('Espera a que termine de cargar el laboratorio.');return;}
 if(op==='start_atlas'||op==='start_patient'||op==='home'){expectedLanding=op==='home';$('scene-loading').hidden=false;setTimeout(()=>{if(expectedLanding!==null){$('scene-loading').hidden=true;expectedLanding=null;toast('La carga está tardando más de lo esperado. Revisa el estado del laboratorio.');}},45000);}
 emit('Command',{op,...extra});
 if(['patient','start_patient','case','finish'].includes(op))tab('patient');
 if(['mission','neuro','tutorial','lesson','exam','time','detective','survival','dissection'].includes(op))tab('learning');
}
function tab(name){selectedTab=name;for(const section of document.querySelectorAll('[data-page]'))section.hidden=section.dataset.page!==name;for(const button of document.querySelectorAll('[data-tab]'))button.classList.toggle('active',button.dataset.tab===name);}
function list(container,values,key,make,update){const parent=$(container),existing=new Map([...parent.children].map(child=>[child.dataset.key,child]));for(const value of values||[]){const id=String(key(value));let item=existing.get(id);if(!item){item=make(value);item.dataset.key=id;parent.appendChild(item);}existing.delete(id);update(item,value);}for(const old of existing.values())old.remove();}
function receive(value){
 if(value.error){error(value.error);return;}state=value;
 if(value.ready&&!firstFrame){firstFrame=true;requestAnimationFrame(()=>requestAnimationFrame(()=>$('loading').hidden=true));notify('atlas:ready',{version:1});}
 if(expectedLanding!==null&&value.landing===expectedLanding&&value.ready){expectedLanding=null;$('scene-loading').hidden=true;}
 document.body.classList.toggle('in-vr',!!value.vr);$('home').hidden=!value.landing;$('panel').hidden=!!value.landing||$('panel').dataset.collapsed==='true';$('voice-dock').hidden=!!value.landing;$('touch-controls').hidden=!mobile||value.landing||value.vr;$('crosshair').hidden=!!value.landing||mobile||value.vr;
 if(value.landing)return;
 document.querySelector('[data-tab="systems"]').hidden=!!value.patientOpen;if(value.patientOpen&&selectedTab==='systems')tab('patient');
 text('assistant-help',value.patientOpen?'Habla con María como en una entrevista educativa. Comienza con un saludo y una pregunta abierta, escucha su respuesta y utiliza Terminar y evaluar para revisar las evidencias.':'Puedes preguntar «¿qué es esto?», «¿para qué sirve?» o pedir «oculta todo menos el corazón». Señala la pieza antes de hablar.');
 voiceUI();
 text('speaker-heading',value.patientOpen?'María · entrevista':'Atlas · asistente');if(hasVoiceSession())text('transcript',value.transcript||'Mantén Hablar o espacio y suelta al terminar.');text('answer',value.answer||'');
 $('talk').classList.toggle('recording',hasVoiceSession()&&!!value.talk);window.AtlasVoice?.setVolume(value.volume??1);
 list('systems',value.systems,v=>v.id,v=>{const b=document.createElement('button');b.addEventListener('click',()=>command('system',{index:(state.systems||[]).findIndex(s=>s.id===v.id)}));return b;},(b,v)=>{b.textContent=(v.visible?'● ':'○ ')+v.name;b.setAttribute('aria-pressed',String(v.visible));});
 list('missions',value.missions,v=>v.index,v=>{const b=document.createElement('button');b.addEventListener('click',()=>command('mission',{index:v.index}));return b;},(b,v)=>{b.textContent=(v.done?'✓ ':'○ ')+v.title;});
 const selection=$('case-select');if(selection.options.length!==(value.cases||[]).length){selection.replaceChildren(...(value.cases||[]).map((c,i)=>new Option(c.title,String(i))));}
 if(document.activeElement!==selection)selection.value=String(value.caseIndex||0);
 text('case-brief',value.cases?.[value.caseIndex||0]?.brief||'');$('patient-session').hidden=!value.patientOpen;
 if(value.emotion){$('comfort').value=value.emotion.comfort;text('comfort-label',value.emotion.comfort+'%');text('emotion-signal',value.emotion.signal);text('advice',value.emotion.advice);}
 text('learning-status',value.learning?.status);text('learning-progress',value.learning?.progress);text('mission-question',value.neuro?.question);text('mission-feedback',value.neuro?.feedback);text('tutorial-text',value.neuro?.tutorial);
 $('part').hidden=!value.part;if(value.part){text('part-name',value.part.name);text('part-description',value.part.description);text('part-function',value.part.function);}
 $('results').hidden=!value.results;
 if(value.results){
  const result=value.results,score=result.score_percent??(result.total?100*result.observed/result.total:0);text('result-score',Math.round(score)+'%');text('result-summary',result.summary||'Revisa las evidencias de tu entrevista.');
  list('criteria',result.criteria,(c)=>c.name,c=>{const article=document.createElement('article');article.className='criterion';for(const tag of ['h4','meter','p','p'])article.appendChild(document.createElement(tag));return article;},(article,c)=>{const percent=c.score_percent??(c.observed?100:0);article.children[0].textContent=c.name+' · '+Math.round(percent)+'%';article.children[1].min=0;article.children[1].max=100;article.children[1].value=percent;article.children[2].textContent=c.evidence?'Evidencia: '+c.evidence:'Sin evidencia suficiente';article.children[3].textContent=c.suggestion||c.recommendation||'';});
 }
}
function sendInput(patch){if(!hasVoiceSession()){input.talk=false;if(patch.talk)patch.talk=false;}Object.assign(input,patch);emit('ReceiveInput',input);input.lookX=input.lookY=0;delete input.down;delete input.up;delete input.cancel;}
function resetInput(){sendInput({moveX:0,moveY:0,lookX:0,lookY:0,talk:false,cancel:true});$('talk').classList.remove('recording');}
for(const b of document.querySelectorAll('[data-op]'))b.addEventListener('click',()=>command(b.dataset.op,{value:Number(b.dataset.value||0)}));
for(const b of document.querySelectorAll('[data-tab]'))b.addEventListener('click',()=>tab(b.dataset.tab));
$('case-select').addEventListener('change',e=>command('case',{index:Number(e.target.value)}));$('voice-volume').addEventListener('input',e=>command('volume',{value:Number(e.target.value)}));
$('logout').addEventListener('click',logout);
$('menu-toggle').addEventListener('click',()=>{const collapsed=$('panel').dataset.collapsed!=='true';$('panel').dataset.collapsed=String(collapsed);$('panel').hidden=collapsed;$('menu-toggle').setAttribute('aria-expanded',String(!collapsed));});
$('fullscreen').addEventListener('click',async()=>{try{if(document.fullscreenElement)await document.exitFullscreen();else await document.documentElement.requestFullscreen();}catch{toast('Este navegador no permite cambiar a pantalla completa.');}});
$('vr').addEventListener('click',()=>command('vr'));
$('talk').addEventListener('pointerdown',e=>{e.preventDefault();if(!hasVoiceSession()){tab('assistant');window.AtlasMicrophone?.test();return;}e.currentTarget.setPointerCapture(e.pointerId);unlockAudio();sendInput({talk:true});});
for(const type of ['pointerup','pointercancel','lostpointercapture'])$('talk').addEventListener(type,()=>sendInput({talk:false}));
document.addEventListener('focusin',e=>sendInput({typing:!!e.target.closest('input,textarea,select')}));
document.addEventListener('focusout',()=>sendInput({typing:false}));
document.addEventListener('pointermove',e=>{input.ui=!!e.target.closest('button,input,select,#panel,#home,#toolbar,dialog');sendInput({ui:input.ui});},{passive:true});
const stick=$('joystick');let stickId=null;
stick.addEventListener('pointerdown',e=>{stickId=e.pointerId;stick.setPointerCapture(stickId);updateStick(e);});
function updateStick(e){if(e.pointerId!==stickId)return;const r=stick.getBoundingClientRect(),x=Math.max(-1,Math.min(1,(e.clientX-r.left-r.width/2)/(r.width*.35))),y=Math.max(-1,Math.min(1,(r.top+r.height/2-e.clientY)/(r.height*.35)));sendInput({moveX:x,moveY:y});stick.firstElementChild.style.transform='translate('+x*29+'px,'+-y*29+'px)';}
stick.addEventListener('pointermove',updateStick);for(const kind of ['pointerup','pointercancel','lostpointercapture'])stick.addEventListener(kind,e=>{if(e.pointerId!==stickId)return;stickId=null;sendInput({moveX:0,moveY:0});stick.firstElementChild.style.transform='';});
let lookId=null,lastX=0,lastY=0;
const look=$('look-pad');look.addEventListener('pointerdown',e=>{lookId=e.pointerId;look.setPointerCapture(lookId);lastX=e.clientX;lastY=e.clientY;sendInput({x:e.clientX/innerWidth,y:1-e.clientY/innerHeight,down:true,ui:false});});
look.addEventListener('pointermove',e=>{if(e.pointerId!==lookId)return;sendInput({x:e.clientX/innerWidth,y:1-e.clientY/innerHeight,lookX:e.clientX-lastX,lookY:-(e.clientY-lastY),ui:false});lastX=e.clientX;lastY=e.clientY;});
for(const kind of ['pointerup','pointercancel','lostpointercapture'])look.addEventListener(kind,e=>{if(e.pointerId!==lookId)return;lookId=null;sendInput({up:true,lookX:0,lookY:0});});
window.addEventListener('blur',resetInput);document.addEventListener('visibilitychange',()=>{if(document.hidden){resetInput();}});
window.addEventListener('pagehide',()=>{resetInput();save(checkpoint);});

$('send-report').addEventListener('click',()=>{
 const report={schemaVersion:1,createdAt:new Date().toISOString(),checkpoint,interview:state.results||null};
 if(accessToken)notify('atlas:results',{report});
 const url=URL.createObjectURL(new Blob([JSON.stringify(report,null,2)],{type:'application/json'})),link=document.createElement('a');link.href=url;link.download='atlas-resultados.json';link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);text('report-status','Resultados exportados.');
});
$('developer').addEventListener('click',async()=>{
 await window.AtlasVoice?.closeAll();if(!devPrevious){save(checkpoint);devPrevious={accessToken,profile,checkpoint:structuredClone(checkpoint),expiresAt};accessToken='';profile='developer';checkpoint=readDraft();$('developer').textContent='Salir del modo de pruebas';}
 else{({accessToken,profile,checkpoint,expiresAt}=devPrevious);devPrevious=null;$('developer').textContent='Modo de pruebas separado';}
 if(unity&&!state.landing)command('home');configure();
});
window.AtlasApp={voiceError:message=>{voiceFailure=message;voiceUI();},bind:instance=>{unity=instance;window.unityInstance=instance;configure();},ready:name=>{receiver=name;configure();},receive,save,error,command,connect,logout,haptic:()=>{},api};
checkpoint=readDraft();tab('systems');configure();notify('atlas:ready',{});if(navigator.xr)navigator.xr.isSessionSupported('immersive-vr').then(ok=>{$('vr').hidden=!ok;}).catch(()=>{});
})();

'use strict';
const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {JSDOM}=require('jsdom');
const dir=path.resolve(__dirname,'../Assets/WebGLTemplates/AtlasNexus');
function fixture(t){
 const dom=new JSDOM(fs.readFileSync(path.join(dir,'index.html'),'utf8'),{url:'https://example.github.io/Atlas/',runScripts:'outside-only'}),w=dom.window;
 t.after(()=>w.close());w.matchMedia=()=>({matches:false});w.structuredClone=structuredClone;w.requestAnimationFrame=f=>f();
 const sent=[],unity=[];const host={postMessage:(value,origin)=>sent.push({value,origin})};Object.defineProperty(w,'parent',{value:host});
 w.ATLAS_CONFIG={apiBaseUrl:'https://api.example/api/v1/atlas',nexusOrigins:['https://nexus.example']};
 w.eval(fs.readFileSync(path.join(dir,'atlas-client.js'),'utf8'));w.AtlasApp.bind({SendMessage:(target,method,payload)=>unity.push({target,method,value:JSON.parse(payload)})});w.AtlasApp.ready('AtlasWeb');
 const session=(profileId='student-a')=>({type:'atlas:session',version:1,accessToken:'fixture-not-a-real-credential-xxxxxxxx',profileId,expiresAt:Date.now()+600000});
 return {w,host,sent,unity,session,post:(data,origin='https://nexus.example',source=host)=>w.dispatchEvent(new w.MessageEvent('message',{data,origin,source}))};
}
const tick=()=>new Promise(resolve=>setImmediate(resolve));
test('Carga sin configuración adicional y no hace solicitudes de autenticación',async t=>{
 const f=fixture(t);await assert.rejects(f.w.AtlasApp.api('/livekit/session',{}),/desde Nexus/);
 assert.equal(f.unity.at(-1).value.profileKey,'practice');assert.equal(f.unity.at(-1).value.accessToken,'');assert.equal(f.w.document.getElementById('qr-dialog'),null);
});
test('Rechaza mensajes de otros dominios o ventanas',async t=>{
 const f=fixture(t);f.post(f.session(),'https://stranger.example');f.post(f.session(),'https://nexus.example',f.w);await tick();assert.equal(f.unity.at(-1).value.accessToken,'');
 f.post(f.session());await tick();assert.equal(f.unity.at(-1).value.profileKey,'student-a');assert.ok(f.unity.at(-1).value.accessToken.length>32);
});
test('Los perfiles quedan separados y el token nunca se guarda en almacenamiento',async t=>{
 const f=fixture(t);await f.w.AtlasApp.connect(f.session());f.w.AtlasApp.save({schemaVersion:1,missions:['neuro.0'],flags:{},poses:[]});
 await f.w.AtlasApp.connect(f.session('student-b'));assert.deepEqual(Array.from(f.unity.at(-1).value.checkpoint.missions),[]);
 await f.w.AtlasApp.connect(f.session());assert.deepEqual(Array.from(f.unity.at(-1).value.checkpoint.missions),['neuro.0']);
 for(let i=0;i<f.w.localStorage.length;i++)assert.ok(!f.w.localStorage.getItem(f.w.localStorage.key(i)).includes('fixture-not-a-real-credential'));
 assert.equal(f.w.sessionStorage.length,0);await f.w.AtlasApp.logout();assert.equal(f.unity.at(-1).value.accessToken,'');
});
test('Una confirmación de progreso ajena no se presenta como guardada por Nexus',async t=>{
 const f=fixture(t);await f.w.AtlasApp.connect(f.session());f.post({type:'atlas:progress-ack',version:1,profileId:'student-b'});assert.notEqual(f.w.document.getElementById('sync').textContent,'Nexus confirmó la recepción del avance');
 f.post({type:'atlas:progress-ack',version:1,profileId:'student-a'});assert.equal(f.w.document.getElementById('sync').textContent,'Nexus confirmó la recepción del avance');
});

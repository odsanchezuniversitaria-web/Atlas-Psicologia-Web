'use strict';
const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {JSDOM}=require('jsdom');
const dir=path.resolve(__dirname,'../Assets/WebGLTemplates/AtlasNexus');
function fixture(t,getUserMedia,devices=[]){
 const dom=new JSDOM('<select id="microphone-device"><option value="">Default</option></select><p id="microphone-device-help"></p><button id="microphone-refresh"></button><p id="microphone-device-name"></p><button id="microphone-test"></button><p id="microphone-status"></p><meter id="microphone-level" min="0" max="1"></meter>',{url:'https://example.github.io',runScripts:'outside-only'}),w=dom.window;t.after(()=>w.close());
 Object.defineProperty(w,'isSecureContext',{value:true});Object.defineProperty(w.navigator,'mediaDevices',{value:{getUserMedia,enumerateDevices:async()=>devices}});
 let sample=0,step=null,finished=null,stops=0;
 w.setInterval=fn=>{step=fn;return 1;};w.clearInterval=()=>{};w.setTimeout=fn=>{finished=fn;return 2;};w.clearTimeout=()=>{};
 w.AudioContext=class{resume(){return Promise.resolve();}close(){return Promise.resolve();}createGain(){return {gain:{value:1},connect(){},disconnect(){}};}createMediaStreamSource(){return {connect(){},disconnect(){}};}createAnalyser(){return {fftSize:512,getFloatTimeDomainData(data){data.fill(sample);},connect(){},disconnect(){}};}};
 const stream={getTracks:()=>[{stop(){stops++;}}]};
 w.eval(fs.readFileSync(path.join(dir,'atlas-microphone.js'),'utf8'));
 return {w,stream,status:()=>w.document.getElementById('microphone-status').textContent,stops:()=>stops,step:()=>step(),finish:()=>finished(),signal:v=>{sample=v;}};
}
test('Only requests microphone after user action; detects level and releases tracks',async t=>{
 let calls=0,f;f=fixture(t,async constraints=>{calls++;assert.equal(constraints.video,false);return f.stream;});
 assert.equal(calls,0);await f.w.AtlasMicrophone.test();assert.equal(calls,1);f.signal(.02);f.step();assert.match(f.status(),/Se detecta señal/);assert.ok(f.w.document.getElementById('microphone-level').value>0);
 f.finish();assert.equal(f.stops(),1);assert.equal(f.w.AtlasMicrophone.isTesting(),false);assert.match(f.status(),/funcionando/);
});
test('Distinguishes silence from successful microphone access',async t=>{
 let f;f=fixture(t,async()=>f.stream);await f.w.AtlasMicrophone.test();f.step();f.finish();assert.match(f.status(),/No se detectó señal suficiente/);assert.equal(f.stops(),1);
});
test('Reports denied permission without claiming a server connection failure',async t=>{
 const f=fixture(t,async()=>{throw Object.assign(new Error(),{name:'NotAllowedError'});});await f.w.AtlasMicrophone.test();assert.match(f.status(),/Permiso.*bloqueado/);assert.equal(f.w.AtlasMicrophone.isTesting(),false);
});
test('Stops a late stream if the user cancels while the permission prompt is open',async t=>{
 let resolve;const f=fixture(t,()=>new Promise(done=>{resolve=done;}));const pending=f.w.AtlasMicrophone.test();f.w.AtlasMicrophone.stop('Cancelada');resolve(f.stream);await pending;assert.equal(f.stops(),1);assert.equal(f.status(),'Cancelada');assert.equal(f.w.AtlasMicrophone.isTesting(),false);
});
test('Uses selected input, shows its real label and preserves peak after the test',async t=>{
 const devices=[{kind:'audioinput',deviceId:'headset',label:'Micrófono de auriculares'}];let f,constraints;
 f=fixture(t,async options=>{constraints=options;return {getTracks:()=>[{label:'Micrófono de auriculares',stop(){}}]};},devices);
 await f.w.AtlasMicrophone.refresh();f.w.document.getElementById('microphone-device').value='headset';await f.w.AtlasMicrophone.test();
 assert.equal(constraints.audio.deviceId.exact,'headset');assert.match(f.w.document.getElementById('microphone-device-name').textContent,/Micrófono de auriculares/);
 f.signal(.04);f.step();f.finish();assert.ok(f.w.document.getElementById('microphone-level').value>0);assert.match(f.status(),/funcionando/);
});
test('Enumerating virtual inputs does not request audio and explains missing hardware',async t=>{
 let calls=0;const f=fixture(t,async()=>{calls++;return f.stream;},[{kind:'audioinput',deviceId:'virtual',label:'Oculus Virtual Audio Device'}]);
 await f.w.AtlasMicrophone.refresh();assert.equal(calls,0);assert.match(f.w.document.getElementById('microphone-device-help').textContent,/entradas virtuales/);
 assert.equal(f.w.document.getElementById('microphone-device').options.length,2);
});
test('Explains a removed input instead of silently switching to an unrelated microphone',async t=>{
 const f=fixture(t,async()=>{throw Object.assign(new Error(),{name:'OverconstrainedError'});});
 await f.w.AtlasMicrophone.test();assert.match(f.status(),/entrada elegida ya no está disponible/);assert.equal(f.w.AtlasMicrophone.isTesting(),false);
});

/* Regression tests for the mobile gesture recognizer. Run with node --test. */
const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const source=fs.readFileSync(path.join(__dirname,'../Assets/WebGLTemplates/AtlasNexus/atlas-touch.js'),'utf8');
function rig(patient=false){
 const listeners={},timers=new Map(),frames=new Map(),sent=[],looks=[];
 let id=0;
 const surface={addEventListener:(type,fn)=>listeners[type]=fn,setPointerCapture:()=>{}};
 const context={window:{},setTimeout:fn=>{timers.set(++id,fn);return id;},clearTimeout:key=>timers.delete(key),
  requestAnimationFrame:fn=>{frames.set(++id,fn);return id;},cancelAnimationFrame:key=>frames.delete(key)};
 vm.runInNewContext(source,context);
 const handle=context.window.AtlasTouch.bind(surface,{canvas:{getBoundingClientRect:()=>({left:20,top:30,width:400,height:800})},
  enabled:()=>true,patient:()=>patient,send:g=>sent.push(g),onLook:(x,y)=>looks.push([x,y]),mark:()=>{}});
 function event(type,x,y,pointerId=1){listeners[type]({type,pointerType:'touch',clientX:x,clientY:y,pointerId,preventDefault:()=>{}});}
 function hold(){for(const [key,fn] of timers){timers.delete(key);fn();}}
 return {event,hold,sent,looks,handle,kinds:()=>sent.map(g=>g.kind)};
}
test('a tap identifies the part at its canvas-relative coordinates',()=>{const r=rig();r.event('pointerdown',220,230);r.event('pointerup',220,230);assert.deepEqual(r.kinds(),['tap']);assert.equal(r.sent[0].x,.5);assert.equal(r.sent[0].y,.75);});
test('a swipe orbits without picking or moving a part',()=>{const r=rig();r.event('pointerdown',100,200);r.event('pointermove',146,225);r.hold();r.event('pointerup',146,225);assert.deepEqual(r.kinds(),['orbit']);});
test('pinch and pan have no stray tap or hold',()=>{const r=rig();r.event('pointerdown',100,200);r.event('pointerdown',200,200,2);r.event('pointermove',250,250,2);r.hold();r.event('pointerup',250,250,2);r.event('pointerup',100,200);assert.deepEqual(r.kinds(),['pinch']);assert.ok(r.sent[0].ratio>1);assert.ok(r.sent[0].dy>0);});
test('long press grabs once and supports scaling before releasing',()=>{const r=rig();r.event('pointerdown',100,200);r.hold();r.event('pointerdown',200,200,2);r.event('pointermove',250,200,2);r.event('pointerup',250,200,2);r.event('pointermove',110,220);r.event('pointerup',110,220);assert.deepEqual(r.kinds(),['hold','pinch','drag','drag','release']);});
test('loss of capture ends a hold without a second release',()=>{const r=rig();r.event('pointerdown',100,200);r.hold();r.event('lostpointercapture',100,200);r.event('pointerup',100,200);assert.deepEqual(r.kinds(),['hold','cancel']);});
test('patient gestures look around without selecting anatomy',()=>{const r=rig(true);r.event('pointerdown',100,200);r.hold();r.event('pointermove',123,217);r.event('pointerup',123,217);assert.deepEqual(r.kinds(),[]);assert.deepEqual(r.looks,[[23,-17]]);});

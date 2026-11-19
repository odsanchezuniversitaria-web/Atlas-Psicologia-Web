(function () {
'use strict';
function bind(surface, {canvas, send, enabled, onLook, patient, mark}) {
 const points = new Map();
 let mode='idle', timer=0, frame=0, start, previous, heldId;
 function uv(p) {
  const r=canvas.getBoundingClientRect();
  return {x:(p.x-r.left)/r.width,y:1-(p.y-r.top)/r.height,tolX:5/r.width,tolY:5/r.height};
 }
 function pair() {
  const [a,b]=points.values();
  return {x:(a.x+b.x)/2,y:(a.y+b.y)/2,gap:Math.max(1,Math.hypot(b.x-a.x,b.y-a.y))};
 }
 function stopTimer(){clearTimeout(timer);timer=0;}
 function flush() {
  if(frame) cancelAnimationFrame(frame);
  frame=0;
  const r=canvas.getBoundingClientRect();
  if(!r.width||!r.height)return;
  if(mode==='pinch'||mode==='hold-pinch') {
   const now=pair();
   if(!patient() && (now.x!==previous.x||now.y!==previous.y||now.gap!==previous.gap))
    send({kind:'pinch',...uv(previous),dx:(now.x-previous.x)/r.width,dy:(now.y-previous.y)/r.height,ratio:now.gap/previous.gap});
   if(mode==='hold-pinch')send({kind:'drag',...uv(points.get(heldId))});
   previous=now;
  } else if(mode==='orbit'||mode==='hold') {
   const now=points.values().next().value;
   if(!now)return;
   const x=now.x-previous.x,y=now.y-previous.y;
   if(x||y) {
    if(mode==='hold')send({kind:'drag',...uv(now)});
    else if(patient())onLook(x,-y);
    else send({kind:'orbit',dx:x/r.width,dy:y/r.height});
   }
   previous=now;
  }
 }
 function cancel() {
  stopTimer(); if(frame)cancelAnimationFrame(frame); frame=0;
  if(mode==='hold'||mode==='hold-pinch')send({kind:'cancel'});
  mode='idle';points.clear();heldId=null;
 }
 surface.addEventListener('pointerdown',e=>{
  if(!enabled()||e.pointerType==='mouse')return;
  e.preventDefault();
  if(frame)flush();
  const p={x:e.clientX,y:e.clientY};points.set(e.pointerId,p);
  surface.setPointerCapture(e.pointerId);
  if(points.size===1) {
   start=previous=p;mode='pending';
   if(!patient())timer=setTimeout(()=>{
    if(mode!=='pending'||points.size!==1)return;
    mode='hold';heldId=e.pointerId;previous=points.get(heldId);
    send({kind:'hold',...uv(previous)});mark(previous,'hold');
   },430);
  } else if(points.size===2 && mode!=='blocked') {
   stopTimer();mode=mode==='hold'?'hold-pinch':'pinch';previous=pair();
  } else {
   stopTimer();if(mode==='hold-pinch'||mode==='hold')send({kind:'cancel'});mode='blocked';
  }
 });
 surface.addEventListener('pointermove',e=>{
  if(!points.has(e.pointerId))return;
  e.preventDefault();const p={x:e.clientX,y:e.clientY};points.set(e.pointerId,p);
  if(mode==='pending'&&Math.hypot(p.x-start.x,p.y-start.y)>8){stopTimer();mode='orbit';previous=start;}
  if(mode!=='pending'&&mode!=='blocked'&&!frame)frame=requestAnimationFrame(flush);
 });
 function end(e) {
  if(!points.has(e.pointerId))return;
  if(e.type!=='pointerup'){cancel();return;}
  points.set(e.pointerId,{x:e.clientX,y:e.clientY});flush();stopTimer();
  if(mode==='pending'&&!patient()&&Math.hypot(e.clientX-start.x,e.clientY-start.y)<=8){
   const pos=points.get(e.pointerId);send({kind:'tap',...uv(pos)});mark(pos,'tap');
  }
  if(mode==='hold'||(mode==='hold-pinch'&&e.pointerId===heldId))send({kind:'release'});
  const keepHold=mode==='hold-pinch'&&e.pointerId!==heldId;
  points.delete(e.pointerId);
  mode=points.size?(keepHold?'hold':'blocked'):'idle';
  if(keepHold)previous=points.get(heldId);
 }
 for(const type of ['pointerup','pointercancel','lostpointercapture'])surface.addEventListener(type,end);
 surface.addEventListener('contextmenu',e=>e.preventDefault());
 return {cancel};
}
window.AtlasTouch={bind};
})();

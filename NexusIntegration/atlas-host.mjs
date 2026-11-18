// Runs in Nexus's authenticated frontend, not inside the Unity web build.
export function attachAtlas({iframe, atlasUrl, sessionEndpoint='/api/atlas/session', getCsrfToken=()=>'', onProgress=()=>{}, onResults=()=>{}, onError=console.error, windowRef=window, fetchImpl=fetch}={}) {
  const url=new URL(atlasUrl), endpoint=new URL(sessionEndpoint,windowRef.location.href);
  if(url.protocol!=='https:' || endpoint.origin!==windowRef.location.origin)throw new Error('Atlas needs HTTPS and a same-origin session endpoint.');
  if(!iframe || !iframe.contentWindow)throw new Error('Attach Atlas after mounting the iframe.');
  iframe.allow='microphone; autoplay; xr-spatial-tracking; fullscreen';iframe.allowFullscreen=true;
  let stopped=false,renewal=0,inFlight=null,currentProfile='';const abort=new AbortController();
  const send=data=>{if(!stopped)iframe.contentWindow.postMessage(data,url.origin);};
  function renew(){
    if(stopped)return Promise.resolve();
    if(inFlight)return inFlight;
    inFlight=(async()=>{
      clearTimeout(renewal);
      const csrf=getCsrfToken();const response=await fetchImpl(endpoint.href,{method:'POST',credentials:'same-origin',cache:'no-store',redirect:'error',headers:{'Content-Type':'application/json',...(csrf?{'X-CSRF-Token':csrf}:{})},body:'{}',signal:abort.signal});
      if(!response.ok)throw new Error('Nexus no pudo autorizar la voz de Atlas ('+response.status+').');
      const session=await response.json();
      if(session.type!=='atlas:session'||session.version!==1||typeof session.accessToken!=='string'||!/^[-_a-zA-Z0-9]{1,100}$/.test(session.profileId)||!Number.isFinite(session.expiresAt)||session.expiresAt<=Date.now())throw new Error('Nexus devolvió una sesión de Atlas inválida.');
      if(stopped)return;
      currentProfile=session.profileId;send(session);
      renewal=setTimeout(()=>renew().catch(onError),Math.max(1000,session.expiresAt-Date.now()-60000));
    })().catch(error=>{if(!stopped){renewal=setTimeout(()=>renew().catch(onError),15000);}throw error;}).finally(()=>{inFlight=null;});
    return inFlight;
  }
  async function message(event){
    if(stopped||event.source!==iframe.contentWindow||event.origin!==url.origin||event.data?.version!==1)return;
    const data=event.data;
    try {
      if(data.type==='atlas:ready'||data.type==='atlas:session-required')await renew();
      if(data.profileId!==currentProfile||!currentProfile)return;
      if(data.type==='atlas:progress'){
        // Acknowledge only after Nexus confirms its own persistence succeeded.
        const profileId=currentProfile;
        const saved=await onProgress({profileId,checkpoint:data.checkpoint});
        if(saved===true&&currentProfile===profileId)send({type:'atlas:progress-ack',version:1,profileId});
      }
      if(data.type==='atlas:results')await onResults({profileId:currentProfile,report:data.report});
    }catch(error){if(!stopped)onError(error);}
  }
  const loaded=()=>renew().catch(onError);
  windowRef.addEventListener('message',message);iframe.addEventListener('load',loaded);
  // Also covers an iframe that loaded before the host listener was mounted.
  renew().catch(onError);
  return {renew,dispose(){if(stopped)return;send({type:'atlas:logout',version:1});stopped=true;clearTimeout(renewal);abort.abort();windowRef.removeEventListener('message',message);iframe.removeEventListener('load',loaded);}};
}

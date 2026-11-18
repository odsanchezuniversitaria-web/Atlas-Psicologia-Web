import test from 'node:test';
import assert from 'node:assert/strict';
import {createAtlasSessionIssuer} from './atlas-session-server.mjs';
import {attachAtlas} from './atlas-host.mjs';

function fixture(){
 const calls=[];
 const issuer=createAtlasSessionIssuer({appId:'fixture-app',appSecret:'fixture-private-secret',profileSecret:'x'.repeat(40),authorize:async({user})=>user.id!=='denied',fetchImpl:async(url,options)=>{
  const body=JSON.parse(options.body);calls.push({url,options,body});
  return {ok:true,json:async()=>url.endsWith('/auth/login')?{token:'server-app-credential',expires_at:Math.floor(Date.now()/1000)+3600,assigned_password:'never-return-this'}:{type:'atlas:session',version:1,accessToken:'scoped-browser-credential',profileId:body.profileId,expiresAt:Date.now()+600000}};
 }});return {issuer,calls};
}
test('Requires authenticated, authorized Nexus learner and course before upstream login',async()=>{
 const f=fixture();await assert.rejects(f.issuer({}),/not authorized/);await assert.rejects(f.issuer({user:{id:'denied'},courseId:'c'}),/not authorized/);assert.equal(f.calls.length,0);
});
test('Only scoped token leaves Nexus server; stable opaque profiles and cached service login',async()=>{
 const f=fixture(),a=await f.issuer({user:{id:'student-a'},courseId:'course1'}),b=await f.issuer({user:{id:'student-b'},courseId:'course1'}),a2=await f.issuer({user:{id:'student-a'},courseId:'course1'});
 assert.match(a.profileId,/^[a-f0-9]{64}$/);assert.notEqual(a.profileId,b.profileId);assert.equal(a.profileId,a2.profileId);
 assert.equal(f.calls.filter(c=>c.url.endsWith('/auth/login')).length,1);assert.ok(!JSON.stringify(a).includes('private-secret'));assert.ok(!JSON.stringify(a).includes('server-app-credential'));assert.ok(!JSON.stringify(a).includes('assigned_password'));
 assert.equal(f.calls[1].options.headers.Authorization,'Bearer server-app-credential');assert.equal(f.calls[1].options.redirect,'error');
});
test('Rejects HTTP Atlas origin and missing authorization callback',()=>{
 assert.throws(()=>createAtlasSessionIssuer({appId:'a',appSecret:'s',profileSecret:'x'.repeat(40),authorize:()=>true,atlasOrigin:'http://bad.example'}),/HTTPS/);assert.throws(()=>createAtlasSessionIssuer({}),/authorization/);
});
test('Host validates origin and window, delivers session, and does not acknowledge unsaved progress',async t=>{
 let listener;const sent=[],errors=[],saved=[];
 const target={postMessage:(data,origin)=>sent.push({data,origin})};
 const iframe={contentWindow:target,addEventListener(){},removeEventListener(){}};
 const host={location:{href:'https://nexus.example/course',origin:'https://nexus.example'},addEventListener:(type,fn)=>{listener=fn;},removeEventListener(){}};
 const session={type:'atlas:session',version:1,accessToken:'web-token',profileId:'student-1',expiresAt:Date.now()+600000};let requests=0;
 const bridge=attachAtlas({iframe,atlasUrl:'https://example.github.io/Atlas/',windowRef:host,fetchImpl:async()=>{requests++;return {ok:true,json:async()=>session};},onProgress:async data=>{saved.push(data);return false;},onError:e=>errors.push(e)});
 t.after(()=>bridge.dispose());await bridge.renew();assert.equal(requests,1);assert.equal(sent[0].data.accessToken,'web-token');assert.equal(sent[0].origin,'https://example.github.io');assert.match(iframe.allow,/microphone/);
 const msg={type:'atlas:progress',version:1,profileId:'student-1',checkpoint:{schemaVersion:1}};
 await listener({origin:'https://attacker.example',source:target,data:msg});await listener({origin:'https://example.github.io',source:{},data:msg});assert.equal(saved.length,0);
 await listener({origin:'https://example.github.io',source:target,data:msg});assert.equal(saved.length,1);assert.equal(sent.filter(x=>x.data.type==='atlas:progress-ack').length,0);assert.equal(errors.length,0);
 bridge.dispose();assert.equal(sent.at(-1).data.type,'atlas:logout');
});

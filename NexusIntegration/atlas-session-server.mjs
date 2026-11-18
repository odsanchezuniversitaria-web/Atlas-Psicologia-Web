// Import into the existing Nexus backend; this is not a separate gateway.
import {createHmac} from 'node:crypto';

export function createAtlasSessionIssuer({appId, appSecret, profileSecret = appSecret,
  atlasOrigin = 'https://odsanchezuniversitaria-web.github.io',
  apiBaseUrl = 'https://api.oscar-vr.iudcdesarrollo.tech/api/v1',
  authorize, fetchImpl = globalThis.fetch} = {}) {
  if (!appId || !appSecret || typeof profileSecret !== 'string' || profileSecret.length < 32 || typeof authorize !== 'function') {
    throw new Error('Atlas requires server credentials, a profile secret, and learner authorization.');
  }
  const origin = new URL(atlasOrigin);
  if (origin.protocol !== 'https:' || origin.origin !== atlasOrigin) throw new Error('Use the exact HTTPS player origin.');
  const base = new URL(apiBaseUrl);
  if (base.protocol !== 'https:' || base.username || base.password || base.search || base.hash) throw new Error('Invalid Atlas HTTPS API.');
  let application = null, loginInFlight = null;
  async function post(path, body, token) {
    const response = await fetchImpl(apiBaseUrl.replace(/\/$/, '') + path, {
      method:'POST', headers:{'Content-Type':'application/json', ...(token?{Authorization:'Bearer '+token}:{})},
      body:JSON.stringify(body), redirect:'error', cache:'no-store', signal:AbortSignal.timeout(15000)
    });
    if (!response.ok) { const error = new Error('Atlas session service unavailable.'); error.status=response.status; throw error; }
    return response.json();
  }
  async function login() {
    if (application && application.expires_at * 1000 > Date.now()+60000) return application.token;
    if (!loginInFlight) loginInFlight=post('/auth/login', {app_id:appId, app_secret:appSecret}).then(result=>{
      if (typeof result.token !== 'string' || !Number.isFinite(result.expires_at)) throw new Error('Invalid application login response.');
      application={token:result.token,expires_at:result.expires_at}; return application.token;
    }).finally(()=>{loginInFlight=null;});
    return loginInFlight;
  }
  return async function issue({user, courseId} = {}) {
    // Both values must come from Nexus's authenticated server session/authorized route.
    // Never accept a user ID supplied by the browser as the authenticated identity.
    if (!user || typeof user.id !== 'string' || !user.id || typeof courseId !== 'string' || !courseId || !(await authorize({user,courseId}))) {
      const error=new Error('Learner not authorized for Atlas.');error.status=403;throw error;
    }
    const profileId=createHmac('sha256',profileSecret).update('atlas-web-profile-v1\0'+JSON.stringify([user.id,courseId])).digest('hex');
    let result;
    for (let attempt=0;attempt<2;attempt++) {
      try {result=await post('/atlas/web/session',{profileId,origin:atlasOrigin},await login());break;}
      catch(error){if(error.status!==401 || attempt)throw error;application=null;}
    }
    if (result.type!=='atlas:session' || result.version!==1 || result.profileId!==profileId || typeof result.accessToken!=='string' || !Number.isFinite(result.expiresAt) || result.expiresAt<=Date.now() || result.expiresAt>Date.now()+11*60000) throw new Error('Invalid scoped Atlas session.');
    return result;
  };
}

'use strict';
const http=require('node:http'),fs=require('node:fs'),path=require('node:path');
const root=path.resolve(process.argv[2]||path.join(__dirname,'../Builds/AtlasPages'));
const port=Number(process.env.ATLAS_PREVIEW_PORT)||8860;
const types={'.html':'text/html; charset=utf-8','.js':'application/javascript','.css':'text/css','.json':'application/json','.wasm':'application/wasm','.png':'image/png','.jpg':'image/jpeg','.txt':'text/plain'};
http.createServer((req,res)=>{
 if(req.method!=='GET'&&req.method!=='HEAD'){res.writeHead(405);res.end();return;}
 const pathname=new URL(req.url,'http://localhost').pathname;let filename;
 try{filename=path.resolve(root,'.'+decodeURIComponent(pathname==='/'?'/index.html':pathname));}catch{res.writeHead(400);res.end();return;}
 if(!filename.startsWith(root+path.sep)||!fs.existsSync(filename)||!fs.statSync(filename).isFile()){res.writeHead(404);res.end('No se encontró el build web.');return;}
 res.writeHead(200,{'Content-Type':types[path.extname(filename)]||'application/octet-stream','Cache-Control':'no-cache','X-Content-Type-Options':'nosniff','Referrer-Policy':'no-referrer','Permissions-Policy':'microphone=(self), xr-spatial-tracking=(self)'});
 if(req.method==='HEAD')res.end();else fs.createReadStream(filename).pipe(res);
}).listen(port,'127.0.0.1',()=>console.log('Atlas local: http://127.0.0.1:'+port));

import fs from 'node:fs';import path from 'node:path';import {createHash} from 'node:crypto';
const root=path.resolve(process.argv[2]||'Builds/site/play');
// Each release loads every runtime resource from its own immutable directory.
const files=[];function walk(dir){for(const e of fs.readdirSync(dir,{withFileTypes:true})){if(e.name==='releases'||e.name.endsWith('.meta'))continue;const p=path.join(dir,e.name);if(e.isDirectory())walk(p);else if(!['index.html','release.json'].includes(e.name))files.push(p);}}walk(root);
const hash=createHash('sha256');for(const p of files.sort()){hash.update(path.relative(root,p));hash.update(fs.readFileSync(p));}hash.update(fs.readFileSync(path.join(root,'index.html')));const id=hash.digest('hex').slice(0,12);const dest=path.join(root,'releases',id);fs.mkdirSync(dest,{recursive:true});
for(const p of files){const out=path.join(dest,path.relative(root,p));fs.mkdirSync(path.dirname(out),{recursive:true});fs.copyFileSync(p,out);}
let html=fs.readFileSync(path.join(root,'index.html'),'utf8');html=html.replace('<head>','<head><meta name="coast-build" content="'+id+'">');
html=html.replace(/(src|href)="((?:icons\/[^"?]+|[^"/?]+\.(?:js|css)))(?:\?[^" ]*)?"/g,(_,attr,file)=>attr+'="releases/'+id+'/'+file+'"');
html=html.replace(/(['"])Build\//g,'$1releases/'+id+'/Build/').replace("streamingAssetsUrl:'StreamingAssets'","streamingAssetsUrl:'releases/"+id+"/StreamingAssets'");
fs.writeFileSync(path.join(root,'index.html'),html);fs.writeFileSync(path.join(root,'release.json'),JSON.stringify({id}));console.log('Packaged release '+id);

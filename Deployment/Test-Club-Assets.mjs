import fs from 'node:fs';
const checks=[];function check(v,name){if(!v)throw Error(name);checks.push(name);}
const root='Assets/WebGLTemplates/CoastRacer/audio/';
for(const name of ['menu','race','result','click','coin','special']){
 const b=fs.readFileSync(root+name+'.wav');check(b.toString('ascii',0,4)==='RIFF'&&b.toString('ascii',8,12)==='WAVE',name+' valid WAV');
 let max=0,energy=0;for(let i=44;i<b.length;i+=2){const v=b.readInt16LE(i);max=Math.max(max,Math.abs(v));energy+=v*v;}
 check(max<32767&&energy>0,name+' has non-clipped audio');
 check(Math.abs(b.readInt16LE(44)-b.readInt16LE(b.length-2))<64,name+' loop boundary is continuous');
}
const html=fs.readFileSync('Assets/WebGLTemplates/CoastRacer/index.html','utf8');
const ids=[...html.matchAll(/\bid="([^"]+)"/g)].map(m=>m[1]);check(new Set(ids).size===ids.length,'No duplicate HTML IDs');
for(const file of ['racer.js','club.js']){const code=fs.readFileSync('Assets/WebGLTemplates/CoastRacer/'+file,'utf8');for(const [,id]of code.matchAll(/\$\('([^']+)'\)/g))check(ids.includes(id),file+' DOM target '+id);}
fs.writeFileSync('Logs/club-assets-checks.json',JSON.stringify({checks},null,2));console.log('PASS '+checks.length+' audio and DOM checks');
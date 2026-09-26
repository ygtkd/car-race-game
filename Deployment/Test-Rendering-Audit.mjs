import fs from 'node:fs';
import path from 'node:path';
import {spawn} from 'node:child_process';
const output=process.argv[2]||'docs/REVISION_9_PERFORMANCE_AUDIT.json';
const logs=path.resolve('Logs');fs.mkdirSync(logs,{recursive:true});
const native=process.argv[3]==='native';
const edge=spawn('C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',['--headless=new','--disable-backgrounding-occluded-windows','--disable-renderer-backgrounding','--disable-background-timer-throttling','--no-first-run','--no-default-browser-check','--remote-debugging-port=9228',...(native?[]:['--enable-unsafe-swiftshader','--use-angle=swiftshader']),'--window-size=1100,650','--user-data-dir='+path.resolve('Temp/RenderingAudit'),'about:blank'],{windowsHide:true,stdio:'ignore'});
const delay=ms=>new Promise(r=>setTimeout(r,ms));const errors=[];const checks=[];
function assert(value,label){if(!value)throw Error(label);checks.push(label);console.log('PASS '+label);}
async function session(url){
 const ws=new WebSocket(url);await new Promise((r,j)=>{ws.onopen=r;ws.onerror=j;});
 let id=0;const pending=new Map();ws.onclose=()=>{for(const p of pending.values()){clearTimeout(p.timer);p.reject(Error('CDP closed'));}pending.clear();};
 ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.id){const p=pending.get(m.id);if(p){pending.delete(m.id);clearTimeout(p.timer);m.error?p.reject(Error(JSON.stringify(m.error))):p.resolve(m.result);}}else if(m.method==='Runtime.exceptionThrown')errors.push(m.params.exceptionDetails.exception?.description||m.params.exceptionDetails.text);else if(m.method==='Runtime.consoleAPICalled'&&m.params.type==='error')errors.push(m.params.args.map(a=>a.value||a.description).join(' '));};
 const call=(method,params={})=>new Promise((resolve,reject)=>{const n=++id;const timer=setTimeout(()=>{pending.delete(n);reject(Error('CDP timeout '+method));},25000);pending.set(n,{resolve,reject,timer});ws.send(JSON.stringify({id:n,method,params}));});
 const evaluate=async expression=>{const r=await call('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw Error(r.exceptionDetails.exception?.description||r.exceptionDetails.text);return r.result.value;};
 await call('Runtime.enable');await call('Page.enable');await call('Emulation.setDeviceMetricsOverride',{width:1100,height:650,deviceScaleFactor:1,mobile:true});await call('Emulation.setTouchEmulationEnabled',{enabled:true,maxTouchPoints:5});
 return{ws,call,evaluate};
}
async function waitFor(tab,expression,seconds=70){for(let i=0;i<seconds*2;i++){if(await tab.evaluate(expression))return true;await delay(500);}return false;}
async function screenshot(tab,file){const data=await tab.call('Page.captureScreenshot',{format:'png'});fs.writeFileSync(path.join(logs,file),Buffer.from(data.data,'base64'));}
const instrument=`
window.audit={calls:0,triangles:0,gl:null};
for(const proto of [WebGLRenderingContext.prototype,WebGL2RenderingContext.prototype]){
 for(const name of ['drawElements','drawArrays','drawElementsInstanced','drawArraysInstanced']){
  if(!Object.prototype.hasOwnProperty.call(proto,name))continue;
  const original=proto[name];
  proto[name]=function(...args){const a=window.audit;a.gl=this;a.calls++;const n=name.includes('Elements')?args[1]:args[2],instances=name.endsWith('Instanced')?args[name.includes('Elements')?4:3]:1;a.triangles+=(args[0]===4?n/3:args[0]===5||args[0]===6?Math.max(0,n-2):0)*instances;return original.apply(this,args);};
 }
}
window.auditSample=duration=>new Promise(resolve=>{
 const samples=[];let previous,started;window.audit.calls=window.audit.triangles=0;
 function tick(t){const a=window.audit;if(started===undefined)started=t;if(previous!==undefined)samples.push({dt:t-previous,calls:a.calls,triangles:a.triangles,t});previous=t;a.calls=a.triangles=0;if(t-started<duration)requestAnimationFrame(tick);else resolve(samples);}
 requestAnimationFrame(tick);
});
Object.defineProperty(window,'Racer',{configurable:true,set(v){const loaded=v.loaded;v.loaded=function(i){window.testUnity=i;return loaded(i)};Object.defineProperty(window,'Racer',{value:v,writable:true,configurable:true})}});
`;
let first;const reports=[];const metadata={};
function percentile(a,p){return a.length?[...a].sort((a,b)=>a-b)[Math.min(a.length-1,Math.floor(a.length*p))]:null;}
try{
 let tabs;for(let i=0;i<40;i++){try{tabs=await(await fetch('http://127.0.0.1:9228/json/list')).json();if(tabs.length)break;}catch{}await delay(500);}
 first=await session(tabs.find(t=>t.type==='page').webSocketDebuggerUrl);
 await first.call('Page.addScriptToEvaluateOnNewDocument',{source:instrument});
 await first.call('Performance.enable');
 metadata.browser=await first.call('Browser.getVersion');
 const getMetrics=async()=>Object.fromEntries((await first.call('Performance.getMetrics')).metrics.map(m=>[m.name,m.value]));
 for(const [version,base,file] of [['revision8','http://127.0.0.1:5111','Logs/revision9-baseline-tracks.json'],['revision9','http://127.0.0.1:5110','Art/Blender/tracks.json']]){
  await first.call('Page.navigate',{url:base+'/play/'});
  assert(await waitFor(first,'window.Racer&&window.testUnity&&document.querySelector("#loading").hidden',150),version+' loaded');
  await first.evaluate("testUnity.SendMessage('RaceGame','CommandFromWeb',JSON.stringify({action:'quality',quality:0}));document.querySelector('#app').style.display='none';document.querySelector('#game').style.visibility='visible'");
  metadata[version]={release:await(await fetch(base+'/play/release.json')).json()};
  const tracks=JSON.parse(fs.readFileSync(file,'utf8'));
  const cases=[['bridge','shonan',{x:9.5,z:-90},8,1100],['hill','shonan',{x:180,z:-650},8,1100],['town','shonan',{x:-445,z:330},8,1100]];
  if(version==='revision9')cases.push(['hill-half-resolution','shonan',{x:180,z:-650},8,550],['hill-one-car','shonan',{x:180,z:-650},1,1100],['ridge','ridge',null,8,1100],['suzuka','suzuka',null,8,1100]);
  for(const [label,trackId,target,count,width] of (native?cases.filter(c=>['hill','ridge','suzuka'].includes(c[0])):cases))for(const night of [false,true]){
   // Same CSS viewport and field of view; vary only the pixel ratio for resolution diagnosis.
   await first.call('Emulation.setDeviceMetricsOverride',{width:1100,height:650,deviceScaleFactor:width/1100,mobile:true});
   await first.evaluate(`testUnity.Module.devicePixelRatio=${width}/1100;testUnity.Module.matchWebGLToCanvasSize=false;document.querySelector('#game').width=${width};document.querySelector('#game').height=${Math.round(width*650/1100)}`);
   const track=tracks.find(t=>t.id===trackId);if(!track)throw Error('Missing '+trackId);
   const index=target?track.points.map((p,i)=>({i,d:(p.x-target.x)**2+(p.z-target.z)**2})).sort((a,b)=>a.d-b.d)[0].i:0;
   const cars=Array.from({length:count},(_,k)=>{const j=(index-k*2+track.points.length)%track.points.length,p=track.points[j],q=track.points[(j+1)%track.points.length];return{id:k?'bot'+k:'perf',vehicle:'apex',x:p.x,y:p.y,z:p.z,yaw:Math.atan2(q.x-p.x,q.z-p.z),index:j,connected:true};});
   const raceId=version+label+night;
   await first.evaluate(`testUnity.SendMessage('RaceGame','NetworkSnapshot',JSON.stringify(${JSON.stringify({track:trackId,phase:'menu',self:'perf',raceId,cars,coins:[],night})}))`);
   assert(await waitFor(first,`Racer.telemetry?.raceId===${JSON.stringify(raceId)}&&Racer.telemetry.cars.length===${count}&&Racer.telemetry.night===${night}`,20),raceId+' state applied');
   await delay(3500);
   const gl=await first.evaluate(`(()=>{const gl=audit.gl,e=gl.getExtension('WEBGL_debug_renderer_info');return{renderer:e?gl.getParameter(e.UNMASKED_RENDERER_WEBGL):gl.getParameter(gl.RENDERER),width:gl.drawingBufferWidth,height:gl.drawingBufferHeight,visibility:document.visibilityState,quality:document.querySelector('#quality').value}})()`);
   for(let repeat=0;repeat<2;repeat++){
    const before=await getMetrics(),samples=await first.evaluate('auditSample(4500)'),after=await getMetrics();
    const drawn=samples.filter(s=>s.calls>0),span=samples.reduce((n,s)=>n+s.dt,0),drawIntervals=drawn.slice(1).map((s,i)=>s.t-drawn[i].t);
    const report={version,label,track:trackId,night,cars:count,index,position:cars[0],repeat,gl,durationMs:span,rafCount:samples.length,renderedIntervals:drawn.length,renderedHz:drawn.length*1000/span,frameMedianMs:percentile(drawIntervals,.5),frameP95Ms:percentile(drawIntervals,.95),drawCallsMedian:percentile(drawn.map(s=>s.calls),.5),trianglesMedian:percentile(drawn.map(s=>s.triangles),.5),rendererMainThreadBusy:(after.TaskDuration-before.TaskDuration)/((after.Timestamp-before.Timestamp)),samples};
    reports.push(report);const {samples:_,position:__,...brief}=report;console.log(JSON.stringify(brief));
    fs.writeFileSync(output,JSON.stringify({metadata,reports,errors,mode:native?'native-auto':'forced-swiftshader',note:'Headless Edge; actual GL renderer recorded per sample. Counts WebGL draw submissions per browser animation interval, not GPU completion. Mobile default low quality (30 fps cap), stationary scene, no race simulation; HTML menu/HUD hidden to isolate 3D rendering. Eight cars loaded, trailing other cars mostly outside camera view. Two 4.5s samples after 3.5s warm-up. Different track geometry means version comparisons are representative, not identical camera scenes.'},null,2));
   }
  }
 }
 assert(errors.length===0,'No runtime errors');
}finally{if(first)try{await first.call('Browser.close')}catch{}first?.ws.close();edge.kill();}

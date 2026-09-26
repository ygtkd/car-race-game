import fs from 'node:fs';
import path from 'node:path';
import {spawn} from 'node:child_process';
const base=process.argv[2]||'http://127.0.0.1:5083';
const logs=path.resolve('Logs');fs.mkdirSync(logs,{recursive:true});
const edge=spawn('C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',['--headless=new','--disable-backgrounding-occluded-windows','--disable-renderer-backgrounding','--disable-background-timer-throttling','--no-first-run','--no-default-browser-check','--remote-debugging-port=9228','--enable-unsafe-swiftshader','--use-angle=swiftshader','--window-size=1100,650','--user-data-dir='+path.resolve('Temp/ClubBrowserCheck'),'about:blank'],{windowsHide:true,stdio:'ignore'});
const delay=ms=>new Promise(r=>setTimeout(r,ms));const errors=[];const checks=[];
function assert(value,label){if(!value)throw Error(label);checks.push(label);console.log('PASS '+label);}
async function session(url){
 const ws=new WebSocket(url);await new Promise((r,j)=>{ws.onopen=r;ws.onerror=j;});
 let id=0;const pending=new Map();
 ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.id){const p=pending.get(m.id);if(p){pending.delete(m.id);clearTimeout(p.timer);m.error?p.reject(Error(JSON.stringify(m.error))):p.resolve(m.result);}}else if(m.method==='Runtime.exceptionThrown')errors.push(m.params.exceptionDetails.exception?.description||m.params.exceptionDetails.text);else if(m.method==='Runtime.consoleAPICalled'&&m.params.type==='error')errors.push(m.params.args.map(a=>a.value||a.description).join(' '));};
 const call=(method,params={})=>new Promise((resolve,reject)=>{const n=++id;const timer=setTimeout(()=>{pending.delete(n);reject(Error('CDP timeout '+method));},60000);pending.set(n,{resolve,reject,timer});ws.send(JSON.stringify({id:n,method,params}));});
 const evaluate=async expression=>{const r=await call('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw Error(r.exceptionDetails.exception?.description||r.exceptionDetails.text);return r.result.value;};
 await call('Runtime.enable');await call('Page.enable');await call('Emulation.setDeviceMetricsOverride',{width:1100,height:650,deviceScaleFactor:1,mobile:true});await call('Emulation.setTouchEmulationEnabled',{enabled:true,maxTouchPoints:5});
 return{ws,call,evaluate};
}
async function waitFor(tab,expression,seconds=70){for(let i=0;i<seconds*2;i++){if(await tab.evaluate(expression))return true;await delay(500);}return false;}
async function screenshot(tab,file){const data=await tab.call('Page.captureScreenshot',{format:'png'});fs.writeFileSync(path.join(logs,file),Buffer.from(data.data,'base64'));}

let first;try{let tabs;for(let i=0;i<40;i++){try{tabs=await(await fetch('http://127.0.0.1:9228/json/list')).json();if(tabs.length)break;}catch{}await delay(500);}first=await session(tabs.find(t=>t.type==='page').webSocketDebuggerUrl);
await first.call('Page.addScriptToEvaluateOnNewDocument',{source:"Object.defineProperty(window,'Racer',{configurable:true,set(v){const loaded=v.loaded;v.loaded=function(i){window.testUnity=i;return loaded(i)};Object.defineProperty(window,'Racer',{value:v,writable:true,configurable:true})}})"});
await first.call('Page.navigate',{url:base+'/play/'});assert(await waitFor(first,'window.Racer&&window.testUnity&&document.querySelector("#loading").hidden',150),'Final game loads');
assert(await first.evaluate('!document.querySelector("#menuSettings")&&document.querySelector("#settingsButton svg")!==null'),'Menu keeps only gear');await screenshot(first,'revision9-final-menu.png');
await first.evaluate("testUnity.SendMessage('RaceGame','CommandFromWeb',JSON.stringify({action:'showcar',vehicle:'apex',badge:''}));testUnity.SendMessage('RaceGame','CommandFromWeb',JSON.stringify({action:'badgeView',id:'left'}))");await delay(1000);await screenshot(first,'revision9-final-showroom.png');
const track=JSON.parse(fs.readFileSync('Art/Blender/tracks.json','utf8')).find(t=>t.id==='shonan');
for(const [index,night]of [[140,true],[290,false]]){const p=track.points[index],q=track.points[index+1],c={id:'view',vehicle:'apex',x:p.x,y:p.y,z:p.z,yaw:Math.atan2(q.x-p.x,q.z-p.z),index,connected:true};
await first.evaluate(`testUnity.SendMessage('RaceGame','NetworkSnapshot',JSON.stringify(${JSON.stringify({track:'shonan',phase:'menu',raceId:'final'+night,self:'view',cars:[c],coins:[],night})}))`);assert(await waitFor(first,`Racer.telemetry.track==='shonan'&&Racer.telemetry.cars[0].index===${index}`),'Final coast '+night);await delay(1600);await screenshot(first,'revision9-final-sky-'+night+'.png');}
assert(errors.length===0,'No final shader/runtime errors');const id=await first.evaluate('document.querySelector("meta[name=coast-build]").content');fs.writeFileSync('Logs/revision9-final-checks.json',JSON.stringify({id,checks,errors},null,2));
}finally{if(first)try{await first.call('Browser.close')}catch{}first?.ws.close();edge.kill();}

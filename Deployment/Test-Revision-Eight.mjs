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

let first;try{
 let tabs;for(let i=0;i<40;i++){try{tabs=await(await fetch('http://127.0.0.1:9228/json/list')).json();if(tabs.length)break;}catch{}await delay(500);}
 first=await session(tabs.find(t=>t.type==='page').webSocketDebuggerUrl);
 await first.call('Page.addScriptToEvaluateOnNewDocument',{source:"Object.defineProperty(window,'Racer',{configurable:true,set(v){const loaded=v.loaded;v.loaded=function(i){window.testUnity=i;return loaded(i)};Object.defineProperty(window,'Racer',{value:v,writable:true,configurable:true})}})"});
 await first.call('Page.navigate',{url:base+'/play/'});assert(await waitFor(first,'window.Racer&&window.testUnity&&document.querySelector("#loading").hidden',150),'Revision 8 WebGL loads');
 assert(await first.evaluate('document.querySelectorAll("#settingsButton svg,#raceSettings svg").length===2'),'Both settings icons are SVG');
 await first.evaluate('document.querySelector("#singleMode").click()');await waitFor(first,'Racer.screen==="vehicles"');await first.evaluate('document.querySelector("#vehicleNext").click()');await waitFor(first,'Racer.screen==="courses"');await first.evaluate('document.querySelector("[data-track=shonan]").click()');await delay(500);
 await first.evaluate('document.querySelector("#solo").click()');assert(await waitFor(first,'Racer.telemetry.phase==="countdown"',120),'Countdown entered');await screenshot(first,'revision8-signals.png');assert(await first.evaluate('document.querySelector("#startLights").getBoundingClientRect().height<60'),'Compact signal height');
 assert(await waitFor(first,'Racer.telemetry.phase==="race"',20),'Race starts');assert(await first.evaluate('Racer.telemetry.length>=3000&&Racer.telemetry.length<=4000'),'Shonan runtime 3-4km');
 const tracks=JSON.parse(fs.readFileSync('Art/Blender/tracks.json','utf8'));
 async function fixture(track,index,extra={},night=false){const p=track.points[index],q=track.points[(index+1)%640],c={id:'view',vehicle:'apex',name:'VIEW',x:p.x,y:p.y,z:p.z,yaw:Math.atan2(q.x-p.x,q.z-p.z),index,speed:0,connected:true,cpuLevel:3,elapsed:30,...extra};await first.evaluate(`testUnity.SendMessage('RaceGame','NetworkSnapshot',JSON.stringify(${JSON.stringify({track:track.id,phase:'menu',raceId:'view-'+track.id+index,self:'view',cars:[c],coins:[],night,difficulty:3,time:30})}))`);assert(await waitFor(first,`Racer.telemetry.track==='${track.id}'&&Racer.telemetry.cars[0].index===${index}`,20),'Scene '+track.id+' '+index);await delay(1100);return c;}
 for(const track of tracks.filter(t=>!process.argv.includes('--coast-only')||t.id==='shonan'))for(const night of [false,true]){
  const indices=track.id==='shonan'?[35,75,140,205,290,350,410,495,540,590]:track.id==='ridge'?[37,55,65]:[180,220,525];
  for(const index of indices){await fixture(track,index,{},night);await screenshot(first,`revision8-view-${track.id}-${index}-${night?'night':'day'}.png`);}
 }
 const track=tracks.find(t=>t.id==='shonan');const car=await fixture(track,400,{finished:true,finishTime:100,lap:2,rank:1,cruiseDistance:2100});
 await delay(2300);await screenshot(first,'revision8-finish-camera.png');assert(await first.evaluate('Racer.telemetry.cars[0].finished&&Racer.telemetry.cars[0].finishTime===100'),'Finish remains immutable during camera orbit');

 for(const mode of ['self-ring','remote-ring','remote-block']){
  const p=track.points[400],q=track.points[401],yaw=Math.atan2(q.x-p.x,q.z-p.z),me={id:'view',vehicle:mode==='self-ring'?'vortex':'apex',name:'ME',x:p.x,y:p.y,z:p.z,yaw,index:400,speed:0,connected:true,abilityImmunity:mode==='self-ring'?12:0,specialTime:mode==='self-ring'?5:0};
  const other={...me,id:'other',name:'OTHER',vehicle:'vortex',x:p.x+Math.cos(yaw)*4,z:p.z-Math.sin(yaw)*4,abilityImmunity:12,specialTime:5,guardFx:mode==='remote-block'?12:0,guardBoost:mode==='remote-block'?5:0};
  await first.evaluate(`testUnity.SendMessage('RaceGame','NetworkSnapshot',JSON.stringify(${JSON.stringify({track:'shonan',phase:'race',raceId:mode,self:'view',cars:mode==='self-ring'?[me]:[me,other],coins:[],night:false,time:30})}))`);assert(await waitFor(first,`Racer.telemetry.raceId==='${mode}'`),'Guard visual fixture '+mode);await delay(600);await screenshot(first,'revision8-'+mode+'.png');
 }
 for(const vehicle of (process.argv.includes('--coast-only')?[]:['apex','swift','vortex','atlas','kebab','banana','tuktuk','bicycle','stormbike','aerobike'])){await first.evaluate(`testUnity.SendMessage('RaceGame','CommandFromWeb',JSON.stringify({action:'showcar',vehicle:'${vehicle}',badge:''}));testUnity.SendMessage('RaceGame','CommandFromWeb',JSON.stringify({action:'badgeView',id:'front'}))`);assert(await waitFor(first,`Racer.telemetry.showroom&&Racer.telemetry.cars[0].vehicle==='${vehicle}'`),'Front model '+vehicle);await delay(700);await screenshot(first,'revision8-front-'+vehicle+'.png');}
 for(const [width,height] of [[852,393],[915,412],[891,412]]){await first.call('Emulation.setDeviceMetricsOverride',{width,height,deviceScaleFactor:1,mobile:true});await delay(100);assert(await first.evaluate('(()=>{const r=document.querySelector("#raceSettings").getBoundingClientRect();return r.right<=innerWidth&&r.left>innerWidth*.75})()'),'Right gear '+width);}
 assert(errors.length===0,'No revision 8 runtime errors');fs.writeFileSync(process.argv.includes('--coast-only')?'Logs/revision8-optimized-browser-checks.json':'Logs/revision8-browser-checks.json',JSON.stringify({checks,errors,note:'Desktop Edge touch emulation; scene snapshots used only for visual inspection.'},null,2));
}finally{if(first)try{await first.call('Browser.close')}catch{}first?.ws.close();edge.kill();}

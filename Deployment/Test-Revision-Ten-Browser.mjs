import fs from 'node:fs';
import path from 'node:path';
import {spawn} from 'node:child_process';
const base=process.argv[2]||'http://127.0.0.1:5083';
const logs=path.resolve('Logs');fs.mkdirSync(logs,{recursive:true});
const edge=spawn('C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',['--headless=new','--disable-backgrounding-occluded-windows','--disable-renderer-backgrounding','--disable-background-timer-throttling','--no-first-run','--no-default-browser-check','--remote-debugging-port=9228','--window-size=1100,650','--user-data-dir='+path.resolve('Temp/RevisionTenBrowser'),'about:blank'],{windowsHide:true,stdio:'ignore'});
const delay=ms=>new Promise(r=>setTimeout(r,ms));const errors=[];const checks=[];
function assert(value,label){if(!value)throw Error(label);checks.push(label);console.log('PASS '+label);}
async function session(url){
 const ws=new WebSocket(url);await new Promise((r,j)=>{ws.onopen=r;ws.onerror=j;});
 let id=0;const pending=new Map();ws.onclose=()=>{for(const p of pending.values()){clearTimeout(p.timer);p.reject(Error('CDP closed'));}pending.clear();};
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
 await first.call('Page.addScriptToEvaluateOnNewDocument',{source:"Object.defineProperty(window,'Racer',{configurable:true,set(v){const loaded=v.loaded;v.loaded=function(i){window.testUnity=i;window.testCommands=[];const send=i.SendMessage;i.SendMessage=function(o,m,s){if(m==='CommandFromWeb'){testCommands.push(JSON.parse(s));if(testCommands.length>100)testCommands.shift();}return send.apply(this,arguments)};return loaded(i)};Object.defineProperty(window,'Racer',{value:v,writable:true,configurable:true})}})"});
 await first.call('Page.navigate',{url:base+'/play/'});assert(await waitFor(first,'window.Racer&&window.testUnity&&document.querySelector("#loading").hidden',150),'Revision 10 game loads');
 await first.evaluate('document.querySelector("#singleMode").click()');assert(await waitFor(first,'Racer.screen==="vehicles"'),'Vehicle selection opens');await delay(400);
 async function swipe(dx,dy=0,cancel=false){const p=await first.evaluate('(()=>{const r=document.querySelector("#orbitSurface").getBoundingClientRect();return{x:r.x+r.width*.5,y:r.y+r.height*.5}})()');await first.call('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:p.x,y:p.y,id:1}]});for(let i=1;i<=4;i++){await first.call('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x:p.x+dx*i/4,y:p.y+dy*i/4,id:1}]});await delay(20);}await first.call('Input.dispatchTouchEvent',{type:cancel?'touchCancel':'touchEnd',touchPoints:[]});await delay(180);}
 await screenshot(first,'revision10-vehicle-selection.png');
 const original=await first.evaluate('Club.profile.vehicle');const ids=['apex','swift','vortex','atlas','kebab','banana','tuktuk','bicycle','stormbike','aerobike'];
 await swipe(-125);assert(await first.evaluate('Club.profile.vehicle')===ids[(ids.indexOf(original)+1)%ids.length],'Left swipe selects next vehicle');await swipe(125);assert(await first.evaluate('Club.profile.vehicle')===original,'Right swipe selects previous');
 for(const [dx,dy,cancel,label] of [[18,0,false,'Small movement'],[5,100,false,'Vertical scroll'],[-120,0,true,'Cancelled gesture']]){await swipe(dx,dy,cancel);assert(await first.evaluate('Club.profile.vehicle')===original,label+' does not select');}
 for(let i=0;i<10;i++)await swipe(-90);assert(await first.evaluate('Club.profile.vehicle')===original,'Ten swipes cycle all ten vehicles once');
 assert(await first.evaluate('!testCommands.some(c=>c.action==="orbit")'),'Selection swipe does not rotate model');
 const padPoint=await first.evaluate('(()=>{const r=document.querySelector("#orbitSurface").getBoundingClientRect();return{x:r.x+r.width*.5,y:r.y+r.height*.5}})()');
 for(const action of ['settings','blur','lostcapture','multitouch']){
  const p=padPoint;await first.call('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:p.x,y:p.y,id:1}]});
  await first.call('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x:p.x-100,y:p.y,id:1}]});
  if(action==='settings')await first.evaluate('document.querySelector("#settingsButton").click()');
  if(action==='blur')await first.evaluate('window.dispatchEvent(new Event("blur"));window.dispatchEvent(new Event("focus"))');
  if(action==='lostcapture')await first.evaluate('document.querySelector("#orbitSurface").dispatchEvent(new PointerEvent("lostpointercapture"))');
  if(action==='multitouch')await first.call('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:p.x-100,y:p.y,id:1},{x:p.x+30,y:p.y,id:2}]});
  await first.call('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
  if(action==='settings')await first.evaluate('document.querySelector("#closeSettings").click()');
  assert(await first.evaluate('Club.profile.vehicle')===original,action+' cancels selection gesture');
 }
 await first.call('Input.dispatchMouseEvent',{type:'mousePressed',x:padPoint.x,y:padPoint.y,button:'left',clickCount:1});
 await first.call('Input.dispatchMouseEvent',{type:'mouseMoved',x:padPoint.x-100,y:padPoint.y,button:'left',buttons:1});
 await first.call('Input.dispatchMouseEvent',{type:'mouseReleased',x:padPoint.x-100,y:padPoint.y,button:'left',clickCount:1});
 assert(await first.evaluate('Club.profile.vehicle')===ids[(ids.indexOf(original)+1)%ids.length],'Mouse drag selects next');await swipe(125);

 await first.evaluate('document.querySelector("#vehicleBack").click()');await waitFor(first,'Racer.screen==="menu"');await first.evaluate('document.querySelector("#galleryButton").click()');await waitFor(first,'Racer.screen==="gallery"');await delay(300);await swipe(-100);
 assert(await first.evaluate('Club.profile.vehicle')===original,'Gallery drag keeps vehicle');assert(await first.evaluate('testCommands.some(c=>c.action==="orbit")'),'Gallery drag rotates model');
 await first.evaluate('document.querySelector("#galleryBack").click()');await waitFor(first,'Racer.screen==="menu"');await first.evaluate('document.querySelector("#singleMode").click()');await waitFor(first,'Racer.screen==="vehicles"');await first.evaluate('document.querySelector("#vehicleNext").click()');await waitFor(first,'Racer.screen==="courses"');
 assert(await first.evaluate('document.querySelector("[data-track=shonan] strong").textContent')==='湘南海岸コース','Japanese course name');await first.evaluate('document.querySelector("[data-track=shonan]").click();document.querySelector("#solo").click()');const started=await waitFor(first,'Racer.telemetry?.track==="shonan"&&Racer.telemetry.phase==="race"');if(!started){console.log(JSON.stringify(await first.evaluate('({screen:Racer.screen,telemetry:Racer.telemetry,commands:testCommands,hidden:document.hidden,errors:'+JSON.stringify(errors)+'})')));await screenshot(first,'revision10-start-failure.png');}assert(started,'New Shonan starts');
 await screenshot(first,'revision10-start.png');
 const t=JSON.parse(fs.readFileSync('Art/Blender/tracks.json','utf8')).find(t=>t.id==='shonan');
 const cave=t.sections.findIndex(x=>x===1),highway=t.sections.findIndex(x=>x===2);
 const scenes=[['island',Math.round(8/45*640)],['cave-entry',cave+1],['cave',cave+28],['cave-exit',t.sections.lastIndexOf(1)],['station',Math.round(26/45*640)],['etc',highway-3],['highway',highway+35],['coast',Math.round(24/45*640)],['railway',Math.round(38/45*640)]];
 await first.evaluate('document.querySelector("#app").style.display="none"');
 for(const [label,index]of scenes)for(const night of [false,true]){
  const p=t.points[index],q=t.points[(index+1)%640],c={id:'inspect',vehicle:'apex',x:p.x,y:p.y,z:p.z,yaw:Math.atan2(q.x-p.x,q.z-p.z),index,connected:true};
  await first.evaluate(`testUnity.SendMessage('RaceGame','NetworkSnapshot',JSON.stringify(${JSON.stringify({track:'shonan',phase:'menu',self:'inspect',raceId:label+night,cars:[c],coins:[],night})}))`);
  assert(await waitFor(first,`Racer.telemetry.raceId===${JSON.stringify(label+night)}`),label+' '+night+' applied');await delay(1700);await screenshot(first,`revision10-${label}-${night?'night':'day'}.png`);
 }
 assert(errors.length===0,'No runtime or shader errors');const id=await first.evaluate('document.querySelector("meta[name=coast-build]").content');fs.writeFileSync('Logs/revision10-browser.json',JSON.stringify({id,checks,errors},null,2));
}finally{if(first)try{await first.call('Browser.close')}catch{}first?.ws.close();edge.kill();}

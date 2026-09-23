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
 const call=(method,params={})=>new Promise((resolve,reject)=>{const n=++id;const timer=setTimeout(()=>{pending.delete(n);reject(Error('CDP timeout '+method));},25000);pending.set(n,{resolve,reject,timer});ws.send(JSON.stringify({id:n,method,params}));});
 const evaluate=async expression=>{const r=await call('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw Error(r.exceptionDetails.text);return r.result.value;};
 await call('Runtime.enable');await call('Page.enable');await call('Emulation.setDeviceMetricsOverride',{width:1100,height:650,deviceScaleFactor:1,mobile:true});await call('Emulation.setTouchEmulationEnabled',{enabled:true,maxTouchPoints:5});
 return{ws,call,evaluate};
}
async function waitFor(tab,expression,seconds=70){for(let i=0;i<seconds*2;i++){if(await tab.evaluate(expression))return true;await delay(500);}return false;}
async function screenshot(tab,file){const data=await tab.call('Page.captureScreenshot',{format:'png'});fs.writeFileSync(path.join(logs,file),Buffer.from(data.data,'base64'));}

let first;try{
 let tabs;for(let i=0;i<40;i++){try{tabs=await(await fetch('http://127.0.0.1:9228/json/list')).json();if(tabs.length)break;}catch{}await delay(500);}
 first=await session(tabs.find(t=>t.type==='page').webSocketDebuggerUrl);
 await first.call('Page.addScriptToEvaluateOnNewDocument',{source:"Object.defineProperty(window,'Racer',{configurable:true,set(v){const loaded=v.loaded;v.loaded=function(i){window.testUnity=i;return loaded(i)};Object.defineProperty(window,'Racer',{value:v,writable:true,configurable:true})}})"});
 await first.call('Page.navigate',{url:base+'/play/'});assert(await waitFor(first,'window.Racer && window.testUnity && document.querySelector("#loading").hidden'),'Revision 4 game loads');
 await first.evaluate('window.originalReceive=Racer.receive;Racer.receive=d=>{if(d.map?.length)window.testMap=d.map;originalReceive(d)};document.querySelector("#singleMode").click()');await waitFor(first,'Racer.screen==="vehicles"');await first.evaluate('document.querySelector("#vehicleNext").click()');await waitFor(first,'Racer.screen==="courses"');await first.evaluate('document.querySelector("[data-track=ridge]").click();document.querySelector("#solo").click()');assert(await waitFor(first,'Racer.telemetry?.phase==="race"'),'Solo begins');
 await screenshot(first,'revision4-road-trees.png');
 const rects=await first.evaluate('["wheel","throttle"].map(id=>{const r=document.getElementById(id).getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2}})');
 const touch=async(type,points=[])=>first.call('Input.dispatchTouchEvent',{type,touchPoints:points});
 for(const h of [650,393]){await first.call('Emulation.setDeviceMetricsOverride',{width:1100,height:h,deviceScaleFactor:1,mobile:true});await delay(150);assert(await first.evaluate('Math.abs(parseFloat(getComputedStyle(document.querySelector("#speed")).fontSize)-'+(h===650?16.1:14)+')<.01 && Math.abs(parseFloat(getComputedStyle(document.querySelector("#gear")).fontSize)-9.1)<.01'),'Meter numerals at seventy percent '+h);}await first.call('Emulation.setDeviceMetricsOverride',{width:1100,height:650,deviceScaleFactor:1,mobile:true});await delay(150);
 await touch('touchStart',[{id:11,...rects[1]},{id:12,x:rects[0].x+40,y:rects[0].y}]);await delay(300);
 assert(await first.evaluate('Racer.drive.throttle===1 && Racer.drive.steer>.1'),'Two fingers drive independently');
 await touch('touchEnd',[{id:11,...rects[1]}]);await delay(150);assert(await first.evaluate('Racer.drive.throttle===0 && Racer.drive.steer>.1'),'Releasing pedal preserves steering only');await touch('touchEnd');await delay(100);
 await touch('touchStart',[{id:11,...rects[1]},{id:12,x:rects[0].x-40,y:rects[0].y}]);await delay(200);await touch('touchEnd',[{id:12,x:rects[0].x-40,y:rects[0].y}]);await delay(100);assert(await first.evaluate('Racer.drive.throttle===1 && Racer.drive.steer===0'),'Releasing steering preserves pedal only');await touch('touchCancel');await delay(100);assert(await first.evaluate('Racer.drive.throttle===0 && Racer.drive.steer===0'),'Cancellation releases both controls');
 await touch('touchStart',[{id:11,...rects[1]}]);await touch('touchMove',[{id:11,x:500,y:150}]);await delay(120);assert(await first.evaluate('Racer.drive.throttle===0'),'Pedal exit cancels throttle');await touch('touchEnd');
 await first.evaluate('const el=document.querySelector("#wheel"),r=el.getBoundingClientRect(),t=new Touch({identifier:999,target:el,clientX:r.x+40,clientY:r.y+40});el.dispatchEvent(new TouchEvent("touchstart",{bubbles:true,cancelable:true,touches:[t],changedTouches:[t]}))');
 await touch('touchStart',[{id:12,x:rects[0].x+40,y:rects[0].y}]);await delay(250);assert(await first.evaluate('Racer.drive.steer>.1'),'Live touch list replaces stale steering identifier');await touch('touchEnd');await delay(120);assert(await first.evaluate('Racer.drive.steer===0 && Racer.drive.throttle===0'),'No held input after final finger leaves');
 for(let i=0;i<30;i++){
  await touch('touchStart',[{id:1,x:rects[0].x+(i%2?-40:40),y:rects[0].y},{id:2,...rects[1]}]);await delay(35);await touch('touchEnd');await delay(60);
  assert(await first.evaluate('Math.abs(Racer.drive.steer)<.01 && Racer.drive.throttle===0'),'Rapid release neutral '+i);
 }
 for(let i=0;i<10;i++){
  await touch('touchStart',[{id:1,x:rects[0].x-40,y:rects[0].y},{id:2,...rects[1]}]);await delay(150);
  await first.evaluate('document.querySelector("#pauseButton").click()');await touch('touchEnd');await waitFor(first,'Racer.screen==="pause"');
  const before=await first.evaluate('({t:Racer.telemetry.cars[0].elapsed,g:Racer.telemetry.cars[0].gauge})');await delay(250);
  assert(await first.evaluate('Racer.telemetry.cars[0].elapsed==='+before.t+' && Racer.telemetry.cars[0].gauge==='+before.g),'Pause freezes time and gauge '+i);
  await first.evaluate('document.querySelector("#resume").click()');assert(await waitFor(first,'Racer.screen==="race" && Racer.telemetry.phase==="race"',5),'Resume unpauses simulation '+i);
  await touch('touchStart',[{id:1,x:rects[0].x+40,y:rects[0].y}]);await delay(200);assert(await first.evaluate('Racer.drive.steer>.2'),'Fresh steering after pause '+i);await touch('touchCancel');await delay(80);
 }
 await first.evaluate('document.querySelector("#raceSettings").click()');await delay(200);await first.evaluate('document.querySelector("#closeSettings").click()');assert(await waitFor(first,'Racer.telemetry.phase==="race"',5),'Settings resumes after repeated pauses');
 const gauge=await first.evaluate('Racer.telemetry.cars[0].gauge');console.log('BEFORE CHARGE',await first.evaluate('JSON.stringify({car:Racer.telemetry.cars[0],drive:Racer.drive})'));await first.evaluate('Object.defineProperty(Racer,"drive",{configurable:true,get(){return window.lastDrive},set(v){window.lastDrive=v;const c=Racer.telemetry.cars[0],m=testMap,p=m[(Math.floor(c.index/4)+4)%m.length];let a=Math.atan2(p.x-c.x,p.z-c.z)-c.yaw;while(a>Math.PI)a-=2*Math.PI;while(a<-Math.PI)a+=2*Math.PI;v.steer=Math.max(-1,Math.min(1,a*2.8));}})');await touch('touchStart',[{id:2,...rects[1]}]);await waitFor(first,'Racer.telemetry.cars[0].gauge>'+gauge,15);console.log('AFTER CHARGE',await first.evaluate('JSON.stringify({car:Racer.telemetry.cars[0],drive:Racer.drive})'));await touch('touchEnd');await first.evaluate('delete Racer.drive;Racer.drive={steer:0,throttle:0,brake:0}');assert(await first.evaluate('Racer.telemetry.cars[0].gauge>'+gauge),'Actual driving charges gauge after pauses');
 await touch('touchStart',[{id:1,x:rects[0].x-40,y:rects[0].y}]);await touch('touchMove',[{id:1,x:rects[0].x,y:20}]);await touch('touchEnd');await delay(120);assert(await first.evaluate('Racer.drive.steer===0'),'Release outside wheel neutralizes steering');
 await first.evaluate('window.dispatchEvent(new Event("blur"))');assert(await waitFor(first,'Racer.screen==="pause"'),'Background focus loss pauses solo');await first.evaluate('document.querySelector("#resume").click()');assert(await waitFor(first,'Racer.telemetry.phase==="race"'),'Focus resume restores simulation');
 assert(errors.length===0,'No browser runtime errors');fs.writeFileSync('Logs/revision4-browser-checks.json',JSON.stringify({checks,errors,note:'Desktop Edge touch emulation; gauge check uses steering assist along road after repeated manual input cycles.'},null,2));
}finally{if(first)try{await first.call('Browser.close')}catch{}first?.ws.close();edge.kill();}

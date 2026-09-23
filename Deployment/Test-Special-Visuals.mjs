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
 await first.call('Page.navigate',{url:base+'/play/'});assert(await waitFor(first,'window.Racer && window.testUnity && document.querySelector("#loading").hidden'),'Revision 3 game loads');
 await first.evaluate('window.originalReceive=Racer.receive;Racer.receive=d=>{if(d.map?.length)window.testMap=d.map;originalReceive(d)};document.querySelector("#singleMode").click()');await waitFor(first,'Racer.screen==="vehicles"');await first.evaluate('document.querySelector("#vehicleNext").click()');await waitFor(first,'Racer.screen==="courses"');await first.evaluate('document.querySelector("[data-track=ridge]").click();document.querySelector("#solo").click()');assert(await waitFor(first,'Racer.telemetry?.phase==="race"'),'Solo begins');
 await screenshot(first,'revision3-road-trees.png');
 // Visual-only snapshot fixtures exercise all renderers; they are not gameplay/balance evidence.
 for(const vehicle of ['apex','swift','vortex','atlas','kebab','banana','tuktuk','bicycle']){
  await first.evaluate('(function(){const c={...Racer.telemetry.cars[0],vehicle:'+JSON.stringify(vehicle)+',rank:1,elapsed:10,speed:0,vx:0,vz:0,specialTime:0,specialUses:0,finished:false,dnf:false,connected:true,jamTime:0};window.fxFixture={type:"state",track:"ridge",phase:"race",self:c.id,raceId:"fx-test",cars:[c],coins:[]};testUnity.SendMessage("RaceGame","NetworkSnapshot",JSON.stringify(fxFixture));})()');await waitFor(first,'Racer.telemetry.cars.length===1 && Racer.telemetry.cars[0].vehicle==='+JSON.stringify(vehicle),5);
  await first.evaluate('fxFixture.cars[0].specialTime=4;fxFixture.cars[0].specialUses=1;testUnity.SendMessage("RaceGame","NetworkSnapshot",JSON.stringify(fxFixture))');assert(await waitFor(first,'Racer.telemetry.cars[0].specialUses===1 && Racer.telemetry.cars[0].specialTime>0',5),'Special snapshot applies '+vehicle);await screenshot(first,'revision3-special-'+vehicle+'.png');
  assert(await first.evaluate('document.querySelector("#specialButton").classList.contains("effectActive")'),'Special HUD active '+vehicle);
 }
 await first.evaluate('const c=fxFixture.cars[0];fxFixture.cars=Club.catalog.map((v,i)=>({...c,id:i===0?c.id:"effect"+i,vehicle:v.id,x:c.x+(i%4-1.5)*3,z:c.z+Math.floor(i/4)*7,specialTime:4,specialUses:2}));testUnity.SendMessage("RaceGame","NetworkSnapshot",JSON.stringify(fxFixture))');
 assert(await waitFor(first,'Racer.telemetry.cars.length===8'),'Eight simultaneous effect fixtures load');
 const performanceResult=await first.evaluate('new Promise(resolve=>{const frames=[];let before=performance.now();function frame(now){frames.push(now-before);before=now;if(frames.length<90)requestAnimationFrame(frame);else{frames.sort((a,b)=>a-b);resolve({medianMs:frames[45],p95Ms:frames[85],wasmBytes:testUnity.Module?.HEAP8?.buffer?.byteLength??null,renderer:"Desktop Edge SwiftShader"})}}requestAnimationFrame(frame)})');fs.writeFileSync('Logs/revision3-effects-performance.json',JSON.stringify(performanceResult,null,2));
 await screenshot(first,'revision3-special-eight.png');
 await first.evaluate('fxFixture.phase="finished";fxFixture.cars.forEach(c=>{c.specialTime=0;c.finished=true});testUnity.SendMessage("RaceGame","NetworkSnapshot",JSON.stringify(fxFixture))');assert(await waitFor(first,'!document.querySelector("#specialButton").classList.contains("effectActive")'),'Special HUD clears at race end');
 assert(errors.length===0,'No browser runtime errors');fs.writeFileSync('Logs/revision3-special-checks.json',JSON.stringify({checks,errors,note:'Desktop Edge touch emulation. Special screenshots use snapshot fixtures.'},null,2));
}finally{if(first)try{await first.call('Browser.close')}catch{}first?.ws.close();edge.kill();}

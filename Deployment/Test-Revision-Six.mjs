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
 await first.call('Page.navigate',{url:base+'/play/'});assert(await waitFor(first,'window.Racer && window.testUnity && document.querySelector("#loading").hidden'),'Game loads');
 await first.evaluate(`const p=JSON.parse(localStorage.getItem('cr.profile'));p.badges=['finish','winner'];p.vehicle='apex';p.decals=Object.fromEntries(['apex','swift','vortex','atlas','kebab','banana','tuktuk','bicycle'].map(v=>[v,'finish']));p.badgeStyles={};localStorage.setItem('cr.profile',JSON.stringify(p));location.reload()`);
 assert(await waitFor(first,'window.testUnity && document.querySelector("#loading").hidden'),'Fixture loads');
 await first.evaluate('document.querySelector("#galleryButton").click()');assert(await waitFor(first,'document.querySelector("#badge-face")'),'Badge editor opens');
 assert(await first.evaluate('Club.choice().badge==="finish"'),'Legacy badge preserved');
 await first.evaluate(`for(const [k,v] of Object.entries({face:'right',shape:'hexagon',pattern:'bolt',color:'blue'})){const e=document.getElementById('badge-'+k);e.value=v;e.dispatchEvent(new Event('change'))}for(const [k,v]of Object.entries({size:1.5,angle:45,x:.27,y:.81})){const e=document.getElementById('badge-'+k);e.value=v;e.dispatchEvent(new Event('input'))}`);
 assert(await waitFor(first,'Racer.telemetry.cars[0].badge==="finish~right~0.27~0.81~1.5~45~blue~hexagon~bolt"'),'Custom badge reaches Unity');
 await screenshot(first,'revision6-badge-side.png');
 await first.evaluate('document.querySelector("#badgePlacement").scrollIntoView({block:"center"})');
 const rect=await first.evaluate('(()=>{const r=document.querySelector("#badgePlacement").getBoundingClientRect();return{x:r.x+r.width*.6,y:r.y+r.height*.35}})()');
 await first.call('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{id:1,...rect}]});await first.call('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
 assert(await first.evaluate('Math.abs(Club.profile.badgeStyles.apex.x-.6)<.02&&Math.abs(Club.profile.badgeStyles.apex.y-.65)<.02'),'Touch placement moves badge');
 const saved=await first.evaluate('Club.choice().badge');await first.evaluate('location.reload()');assert(await waitFor(first,'window.testUnity && document.querySelector("#loading").hidden'),'Reload completes');assert(await first.evaluate('Club.choice().badge==='+JSON.stringify(saved)),'Design survives reload');
 await first.evaluate('document.querySelector("#galleryButton").click()');await waitFor(first,'document.querySelector("#badge-face")');
 for(const vehicle of ['apex','swift','vortex','atlas','kebab','banana','tuktuk','bicycle']){
 await first.evaluate(`document.querySelector('#galleryChoices [data-vehicle="${vehicle}"]').click()`);await delay(400);
 assert(await waitFor(first,'Racer.telemetry.cars[0].vehicle==='+JSON.stringify(vehicle)),'Gallery renders '+vehicle);
 await screenshot(first,'revision6-badge-'+vehicle+'.png');
 }
 await first.evaluate('document.querySelector("#galleryChoices [data-vehicle=apex]").click()');assert(await first.evaluate('Club.choice().badge==='+JSON.stringify(saved)),'Vehicle-specific design retained');
 await first.evaluate('document.querySelector("#galleryBack").click()');await waitFor(first,'Racer.screen==="menu"');await first.evaluate('document.querySelector("#singleMode").click()');await waitFor(first,'Racer.screen==="vehicles"');await first.evaluate('document.querySelector("#vehicleNext").click()');await waitFor(first,'Racer.screen==="courses"');await first.evaluate('document.querySelector("[data-track=ridge]").click();document.querySelector("#solo").click()');assert(await waitFor(first,'Racer.telemetry?.phase==="race"'),'Race begins');
 assert(await first.evaluate('Racer.telemetry.cars[0].badge==='+JSON.stringify(saved)),'Race uses customized badge');await screenshot(first,'revision6-mountain-rocks.png');
 assert(errors.length===0,'No browser runtime errors');fs.writeFileSync('Logs/revision6-browser-checks.json',JSON.stringify({checks,errors,note:'Desktop Edge touch emulation; isolated unlocked badge fixture.'},null,2));
}finally{if(first)try{await first.call('Browser.close')}catch{}first?.ws.close();edge.kill();}
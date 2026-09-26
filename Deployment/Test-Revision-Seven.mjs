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
 await first.call('Page.navigate',{url:base+'/play/'});assert(await waitFor(first,'window.Racer && window.testUnity && document.querySelector("#loading").hidden',120),'Blender game loads');
 assert(await first.evaluate('Club.catalog.length===10&&RacerItems.length===30'),'Ten models and thirty item definitions');
 await first.evaluate('document.querySelector("#onlineMode").click()');await waitFor(first,'Racer.screen==="connect"');
 assert(await first.evaluate('!document.querySelector("#roomCode")&&!document.querySelector("#joinRoom")'),'Code entry removed');
 await delay(400);console.log('ALIGN',await first.evaluate('JSON.stringify([...document.querySelectorAll("#connect .cornerBack,#connect h2")].map(x=>({tag:x.tagName,rect:x.getBoundingClientRect().toJSON(),margin:getComputedStyle(x).margin})))'));await screenshot(first,'revision7-multiplayer.png');
 assert(await first.evaluate('(()=>{const a=document.querySelector("#connect .cornerBack").getBoundingClientRect(),b=document.querySelector("#connect h2").getBoundingClientRect();return Math.abs(a.y+a.height/2-b.y-b.height/2)<3})()'),'Multiplayer back and title align');await screenshot(first,'revision7-multiplayer.png');
 await first.evaluate('document.querySelector("#connect .close").click()');await waitFor(first,'Racer.screen==="menu"');await first.evaluate('document.querySelector("#galleryButton").click()');await waitFor(first,'Racer.screen==="gallery"');
 for(const vehicle of ['apex','swift','vortex','atlas','kebab','banana','tuktuk','bicycle','stormbike','aerobike']){await first.evaluate(`document.querySelector('#galleryChoices [data-vehicle="${vehicle}"]').click()`);assert(await waitFor(first,`Racer.telemetry.cars[0].vehicle==="${vehicle}"`,15),'Blender vehicle '+vehicle);await delay(200);await screenshot(first,'revision7-car-'+vehicle+'.png');}
 await first.evaluate(`Club.profile.badges=RacerItems.map(x=>x.id);document.querySelector('#galleryChoices [data-vehicle="apex"]').click()`);
 assert(await first.evaluate('!document.querySelector("#badge-shape")&&!document.querySelector("#badge-pattern")'),'Shape and pattern editors removed');
 for(const id of ['finish','finish50','wins25','disrupt50','nightExpert']){await first.evaluate(`(()=>{const b=[...document.querySelectorAll('#badgeChoices button')].find(b=>b.textContent.includes(RacerItems.find(x=>x.id==='${id}').name));b.click()})()`);await delay(350);await screenshot(first,'revision7-item-'+id+'.png');}
 assert(await first.evaluate('document.querySelectorAll(".itemSlots button").length===3'),'Three equipment slots');
 await first.evaluate('document.querySelector("#galleryBack").click()');await waitFor(first,'Racer.screen==="menu"');await first.evaluate('document.querySelector("#recordsButton").click()');await waitFor(first,'Racer.screen==="records"');assert(await first.evaluate('document.querySelectorAll("#achievementList .achievement").length===30'),'Thirty mission rows');await first.evaluate('document.querySelector("#recordsBack").click()');await waitFor(first,'Racer.screen==="menu"');
 for(const track of ['ridge','suzuka','shonan'])for(const night of [false,true]){
  await first.evaluate('document.querySelector("#singleMode").click()');await waitFor(first,'Racer.screen==="vehicles"');await first.evaluate('document.querySelector("#vehicleNext").click()');await waitFor(first,'Racer.screen==="courses"');
  await first.evaluate(`document.querySelector('[data-track="${track}"]').click();document.querySelector('#soloNight').value='${night?'night':'day'}';document.querySelector('#soloDifficulty').value='5';document.querySelector('#solo').click()`);
  assert(await waitFor(first,'Racer.telemetry.phase==="race"',120),'Race '+track+' '+night);assert(await first.evaluate('Racer.telemetry.night==='+night+'&&Racer.telemetry.difficulty===5'),'Race conditions applied');await delay(1200);await screenshot(first,'revision7-'+track+'-'+(night?'night':'day')+'.png');
  assert(await first.evaluate('document.querySelector("#specialButton").textContent!=="スペシャル"'),'Special button names technique');
  await first.evaluate('document.querySelector("#pauseButton").click()');await waitFor(first,'Racer.screen==="pause"');await first.evaluate('document.querySelector("#pauseMenu").click();document.querySelector("#acceptExit").click()');await waitFor(first,'Racer.screen==="menu"');
 }
 assert(errors.length===0,'No runtime errors');fs.writeFileSync('Logs/revision7-browser-checks.json',JSON.stringify({checks,errors,note:'Desktop Edge touch emulation. Gallery uses isolated unlocked rewards fixture.'},null,2));
}finally{if(first)try{await first.call('Browser.close')}catch{}first?.ws.close();edge.kill();}
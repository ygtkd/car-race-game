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
let first;try{let tabs;for(let i=0;i<40;i++){try{tabs=await(await fetch('http://127.0.0.1:9228/json/list')).json();if(tabs.length)break;}catch{}await delay(500);}first=await session(tabs.find(t=>t.type==='page').webSocketDebuggerUrl);await first.call('Page.navigate',{url:base+'/play/'});assert(await waitFor(first,'window.Racer && document.querySelector("#loading").hidden'),'Release loads');
 const id=await first.evaluate('document.querySelector("meta[name=coast-build]").content');
 assert(await first.evaluate('document.querySelector("#buildVersion").textContent.includes('+JSON.stringify(id)+')'),'Settings identifies running build');
 assert(await first.evaluate('[...document.scripts].filter(s=>s.src).every(s=>s.src.includes("/releases/"+'+JSON.stringify(id)+'+"/"))'),'All executable scripts belong to one release');
 await first.evaluate('window.originalFetch=fetch;window.fetch=(u,o)=>String(u).endsWith("release.json")?Promise.resolve(new Response(JSON.stringify({id:"newer-build"}),{status:200})):originalFetch(u,o);document.dispatchEvent(new Event("visibilitychange"))');
 assert(await waitFor(first,'!document.querySelector("#updateGame").hidden',5),'New release offered on menu');
 await first.evaluate('document.querySelector("#singleMode").click()');await waitFor(first,'Racer.screen==="vehicles"');await first.evaluate('document.dispatchEvent(new Event("visibilitychange"))');await delay(300);assert(await first.evaluate('document.querySelector("#updateGame").hidden'),'Update unavailable during selection');
 await first.evaluate('window.fetch=originalFetch;document.querySelector("#vehicleNext").click()');await waitFor(first,'Racer.screen==="courses"');await first.evaluate('document.querySelector("[data-track=suzuka]").click();document.querySelector("#solo").click()');assert(await waitFor(first,'Racer.telemetry?.phase==="race"'),'Final build race begins');await screenshot(first,'revision2-final-suzuka.png');
 assert(await first.evaluate('getComputedStyle(document.querySelector("#centerMessage")).display==="none"'),'Start text remains hidden');assert(errors.length===0,'No errors in release verification');fs.writeFileSync('Logs/revision2-release-checks.json',JSON.stringify({id,checks,errors},null,2));
}finally{if(first)try{await first.call('Browser.close')}catch{}first?.ws.close();edge.kill();}
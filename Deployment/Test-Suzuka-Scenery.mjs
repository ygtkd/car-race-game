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
 await first.evaluate('window.originalReceive=Racer.receive;Racer.receive=d=>{if(d.map?.length)window.testMap=d.map;originalReceive(d)};document.querySelector("#singleMode").click()');await waitFor(first,'Racer.screen==="vehicles"');await first.evaluate('document.querySelector("#vehicleNext").click()');await waitFor(first,'Racer.screen==="courses"');await first.evaluate('document.querySelector("[data-track=suzuka]").click();document.querySelector("#solo").click()');assert(await waitFor(first,'Racer.telemetry?.phase==="race"'),'Solo begins');
 await screenshot(first,'revision4-road-trees.png');

 const points=JSON.parse(fs.readFileSync('Tests/Fixtures/suzuka-crossing.json','utf8')).p;
 for(const index of [211,221,225,233,241,473,481,489,497,505]){
  const p=points[index],a=points[(index+639)%640],b=points[(index+1)%640];
  const pose={x:p[0],y:p[1],z:p[2],yaw:Math.atan2(b[0]-a[0],b[2]-a[2]),index};
  await first.evaluate('(function(){const c={...Racer.telemetry.cars[0],...'+JSON.stringify(pose)+',rank:1,elapsed:10,speed:0,vx:0,vz:0,specialTime:0,hasFront:false,finished:false,dnf:false,connected:true};testUnity.SendMessage("RaceGame","NetworkSnapshot",JSON.stringify({type:"state",track:"suzuka",phase:"race",self:c.id,raceId:"crossing-visual",cars:[c],coins:[]}));})()');
  assert(await waitFor(first,'Math.abs(Racer.telemetry.cars[0].index-'+index+')<4 && Math.abs(Racer.telemetry.cars[0].y-'+p[1]+')<1',5),'Correct crossing layer '+index);await delay(1100);await screenshot(first,'revision4-crossing-'+index+'.png');
 }
 assert(errors.length===0,'No crossing runtime errors');fs.writeFileSync('Logs/revision4-crossing-checks.json',JSON.stringify({checks,errors,note:'Camera snapshots on both branches, not a lap test.'},null,2));
}finally{if(first)try{await first.call('Browser.close')}catch{}first?.ws.close();edge.kill();}

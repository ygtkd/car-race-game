import fs from 'node:fs';
const url=process.argv[2]||'ws://127.0.0.1:5080/ws';
const delay=ms=>new Promise(r=>setTimeout(r,ms)),checks=[];
const assert=(v,m)=>{if(!v)throw Error(m);checks.push(m);console.log('PASS '+m);};
async function connect(){
 const ws=new WebSocket(url);const messages=[];
 await new Promise((resolve,reject)=>{ws.onopen=resolve;ws.onerror=reject;});
 ws.onmessage=e=>{messages.push(JSON.parse(e.data));if(messages.length>100)messages.shift();};
 const wait=async(test,ms=10000)=>{const end=Date.now()+ms;while(Date.now()<end){const i=messages.findIndex(test);if(i>=0)return messages.splice(i,1)[0];await delay(30);}throw Error('Protocol wait timed out');};
 return{ws,messages,wait,send:(action,rest={})=>ws.send(JSON.stringify({action,...rest}))};
}
let a,b,c;
try{
 a=await connect();a.send('create',{track:'ridge',name:'通信甲',vehicle:'swift',badge:'winner~right~0.27~0.81~1.2~45~blue~hexagon~bolt'});const ja=await a.wait(x=>x.type==='joined');
 b=await connect();b.send('join',{code:ja.code,name:'通信乙'});const jb=await b.wait(x=>x.type==='joined');
 let rejected=false;try{c=await connect();c.ws.close();}catch{rejected=true;}
 assert(!rejected,'Expanded configuration accepts more than two sockets');
 c=await connect();const invalidClosed=new Promise(r=>c.ws.onclose=r);c.ws.send('[]');await Promise.race([invalidClosed,delay(4000).then(()=>{throw Error('Malformed connection leaked')})]);assert(c.ws.readyState===3,'Invalid message connection is closed');
 b.send('configure',{track:'shonan',bots:6,night:true,difficulty:5});await delay(100);a.messages.length=0;a.send('configure',{track:'ridge',bots:2,night:true,difficulty:5});const configured=await a.wait(x=>x.type==='lobby'&&x.bots===2);assert(configured.difficulty===5&&configured.night,'Host selects CPU level and night');
 a.send('ready',{ready:true});b.send('ready',{ready:true});await delay(100);a.send('start');
 const load=await a.wait(x=>x.type==='state'&&x.phase==='loading');await b.wait(x=>x.type==='state'&&x.phase==='loading');
 a.send('loaded',{raceId:'stale'});await delay(250);
 assert(!a.messages.some(x=>x.type==='state'&&x.phase==='countdown'),'Stale load acknowledgement cannot start race');
 a.send('loaded',{raceId:load.raceId});await delay(300);
 assert(!a.messages.some(x=>x.type==='state'&&x.phase==='countdown'),'Race waits for second client loading');
 b.send('loaded',{raceId:load.raceId});
 await a.wait(x=>x.type==='state'&&x.phase==='race');await b.wait(x=>x.type==='state'&&x.phase==='race');
 for(let i=0;i<12;i++){a.send('input',{throttle:1,steer:0,brake:0,assist:1,sensitivity:1,x:100000,lap:99,gauge:1,coins:999,vehicle:"atlas"});await delay(100);}
 let state=await a.wait(x=>x.type==='state'&&x.cars.find(p=>p.id===ja.id)?.speed>4);let me=state.cars.find(p=>p.id===ja.id);assert(state.cars.length===4&&state.cars.every(c=>c.cpuLevel===5&&c.night)&&new Set(state.cars.map(c=>c.gridSlot)).size===4,'Shared CPU settings and randomized unique grid');
 assert(Math.abs(me.x)<50&&me.lap===0,'Client cannot spoof position or lap');
 assert(me.vehicle==='swift'&&me.badge==='winner~right~0.27~0.81~1.2~45~blue~hexagon~bolt'&&me.coins===0&&me.gauge<1,'Server owns vehicle, coins and gauge');
 a.send('special');await delay(100);a.messages.length=0;
 const noSkill=await a.wait(x=>x.type==='state');assert(noSkill.cars.find(p=>p.id===ja.id).specialUses===0,'Uncharged special is rejected');
 assert(noSkill.coins.length===3,'Three shared coin states included in snapshots');const peerState=await b.wait(x=>x.type==='state'&&x.cars.some(c=>c.badge==="winner~right~0.27~0.81~1.2~45~blue~hexagon~bolt"));assert(peerState.cars.some(c=>c.badge==="winner~right~0.27~0.81~1.2~45~blue~hexagon~bolt"),'Custom badge reaches the other player');
 a.messages.length=0;await delay(2500);state=await a.wait(x=>x.type==='state'&&x.cars.find(p=>p.id===ja.id)?.speed<.1);
 assert(state.cars.find(p=>p.id===ja.id).speed<.1,'Stale input is released and braking applied');
 a.ws.close(4000,"Simulated link loss");await b.wait(x=>x.type==='state'&&!x.cars.find(p=>p.id===ja.id).connected);
 await delay(150);a=await connect();a.send('resume',{token:ja.token});const resumed=await a.wait(x=>x.type==='joined');
 assert(resumed.id===ja.id,'Reconnect within 20 seconds preserves player identity');
 a.messages.length=0;const beforeRecovery=await a.wait(x=>x.type==='state');a.send('recover');a.messages.length=0;state=await a.wait(x=>x.type==='state');
 assert(state.cars.find(p=>p.id===ja.id).elapsed-beforeRecovery.cars.find(p=>p.id===ja.id).elapsed<1,'Removed manual recovery cannot force teleport or penalty');
 a.ws.close(4000,"Simulated link loss");b.messages.length=0;b.send('input',{brake:1});await delay(21000);b.send('input',{brake:1});
 state=await b.wait(x=>x.type==='state'&&x.cars.find(p=>p.id===ja.id).dnf);
 assert(state.phase==='race'&&!state.cars.find(p=>p.id===jb.id).dnf,'Last human continues after peer disconnect timeout');
 a=await connect();a.send('resume',{token:ja.token});assert((await a.wait(x=>x.type==='error')).code==='SESSION_EXPIRED','Expired session is rejected');
 a.ws.close();a=null;b.ws.close();b=null;
 fs.writeFileSync('Logs/revision7-protocol-checks.json',JSON.stringify({checks},null,2));
}catch(e){console.error(e);fs.writeFileSync('Logs/protocol-failure.json',JSON.stringify({checks,error:String(e)},null,2));process.exitCode=1;}
finally{a?.ws.close();b?.ws.close();c?.ws.close();}
